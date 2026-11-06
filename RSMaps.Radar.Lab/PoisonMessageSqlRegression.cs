using maps4.Models;
using maps4.Repositorios.Implementacion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using System.Text.Json;
using RSMaps.Radar.Listener.Models;
using static PoisonMessageRegression;

internal static class PoisonMessageSqlRegression
{

    public static async Task RunAsync()
    {
        string localMaster = LocalSqlRegression.MasterConnectionString();
        string database = "RadarPoisonRegression_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder(localMaster) { InitialCatalog = database };
        string cs = builder.ConnectionString;
        bool created = false;
        try
        {
            await Execute(localMaster, $"CREATE DATABASE [{database}];");
            created = true;
            await Execute(cs, "CREATE TABLE dbo.RSMAPS_RadarAgentDevice (IdAgent uniqueidentifier NOT NULL PRIMARY KEY);");
            string root = Environment.CurrentDirectory;
            foreach (string file in new[] { "44_radar_message_processing_persistence.sql", "45_radar_terminal_workflow_ack.sql", "57_radar_poison_message_budget.sql", "58_radar_poison_admin_reactivation.sql" })
            {
                string sql = await File.ReadAllTextAsync(Path.Combine(root, "sql", "RSMaps2", file));
                // Only the legacy hard-coded database guard changes in memory, inside the isolated runner.
                sql = sql.Replace("DB_NAME() <> 'mapsMarkers'", $"DB_NAME() <> '{database}'");
                await Script(cs, sql);
                await Script(cs, sql); // Migration idempotence.
            }
            Guid agent = Guid.NewGuid();
            string migration57 = await File.ReadAllTextAsync(Path.Combine(root, "sql", "RSMaps2", "57_radar_poison_message_budget.sql"));
            await MustFail(() => Execute(cs, "EXEC sys.sp_set_session_context @key=N'RadarPoisonTest',@value=NULL;\n" + migration57),
                "Migration rejects isolated database without explicit test session marker", 54700);
            await Execute(cs, "DROP INDEX UX_RSMAPS_RadarMessageProcessing_Idempotencia ON dbo.RSMAPS_RadarMessageProcessing; CREATE UNIQUE INDEX UX_RSMAPS_RadarMessageProcessing_Idempotencia ON dbo.RSMAPS_RadarMessageProcessing(IdRadarMessageProcessing);");
            await MustFail(() => Script(cs, migration57), "Same-name wrong idempotency index rejected", 54701);
            await Execute(cs, "DROP INDEX UX_RSMAPS_RadarMessageProcessing_Idempotencia ON dbo.RSMAPS_RadarMessageProcessing; CREATE UNIQUE INDEX UX_RSMAPS_RadarMessageProcessing_Idempotencia ON dbo.RSMAPS_RadarMessageProcessing(IdAgent,ChatOrigen,MessageId);");
            await Execute(cs, "ALTER TABLE dbo.RSMAPS_RadarMessageProcessing DROP CONSTRAINT CK_RadarProcessing_MaxIntentos; ALTER TABLE dbo.RSMAPS_RadarMessageProcessing ADD CONSTRAINT CK_RadarProcessing_MaxIntentos CHECK (1=1);");
            await MustFail(() => Script(cs, migration57), "Same-name ineffective budget constraint rejected", 54704);
            await Execute(cs, "ALTER TABLE dbo.RSMAPS_RadarMessageProcessing DROP CONSTRAINT CK_RadarProcessing_MaxIntentos; ALTER TABLE dbo.RSMAPS_RadarMessageProcessing WITH CHECK ADD CONSTRAINT CK_RadarProcessing_MaxIntentos CHECK (MaxIntentosProcesamiento>0);");
            await Script(cs, migration57);
            string migration58 = await File.ReadAllTextAsync(Path.Combine(root, "sql", "RSMaps2", "58_radar_poison_admin_reactivation.sql"));
            Check(!Regex.IsMatch(migration58, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase), "Admin installer is one guarded batch");
            DateTime previousProcedureDate = (DateTime)(await Scalar(cs, "SELECT modify_date FROM sys.procedures WHERE name=N'RSMAPS_RadarReactivateProcessing'"))!;
            await Task.Delay(20);
            await using (var guardedConnection = new SqlConnection(cs))
            {
                await guardedConnection.OpenAsync();
                await using var reset = new SqlCommand("EXEC sys.sp_set_session_context @key=N'RadarPoisonTest',@value=NULL;", guardedConnection);
                await reset.ExecuteNonQueryAsync();
                int refusals = 0;
                // Simulate an interactive SQL client that continues after failed batches.
                foreach (string batch in Regex.Split(migration58, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    try { await using var cmd = new SqlCommand(batch, guardedConnection); await cmd.ExecuteNonQueryAsync(); }
                    catch (SqlException ex) when (ex.Number == 54800) { refusals++; }
                }
                Check(refusals == 1, "Admin installer rejects missing test context");
            }
            Check((DateTime)(await Scalar(cs, "SELECT modify_date FROM sys.procedures WHERE name=N'RSMAPS_RadarReactivateProcessing'"))! == previousProcedureDate,
                "Rejected installer cannot create or alter procedure after validation error");
            await Execute(cs, "INSERT dbo.RSMAPS_RadarAgentDevice VALUES (@agent);", new SqlParameter("@agent", agent));
            IConfiguration Config(int? max = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:cadenaSQL"] = cs, ["Radar:Processing:MaxAttempts"] = (max ?? 5).ToString() }).Build();
            var repository = new RadarMessageProcessingRepository(Config());
            var pending = new RadarPendingProcessingRepository(Config());

            var message = Message();
            var claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => repository.ReclamarAsync(agent, message, TimeSpan.FromMinutes(2))));
            Check(claims.Count(c => c.Status == RadarMessageProcessingClaimStatus.Acquired) == 1, "Exactly one concurrent new claim");
            Check(claims.Count(c => c.Status == RadarMessageProcessingClaimStatus.Busy) == 11, "Other concurrent claims are Busy");
            var claim = claims.Single(c => c.Status == RadarMessageProcessingClaimStatus.Acquired);
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                Check(claim.IntentosProcesamiento == attempt, "Total attempt count includes initial claim");
                var failure = await repository.RegistrarFalloAsync(claim.IdRadarMessageProcessing, claim.LeaseToken!.Value,
                    "Synthetic transient failure", TimeSpan.FromSeconds(1));
                Check(failure == (attempt == 5 ? RadarMessageProcessingFailureStatus.PermanentFailure : RadarMessageProcessingFailureStatus.Retryable), "Budget transition");
                if (attempt < 5)
                {
                    await Execute(cs, "UPDATE dbo.RSMAPS_RadarMessageProcessing SET ReintentarDespuesUtc=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE IdRadarMessageProcessing=@id;", new SqlParameter("@id", claim.IdRadarMessageProcessing));
                    claim = await repository.ReclamarAsync(agent, message, TimeSpan.FromMinutes(2));
                }
            }
            var terminalClaims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => repository.ReclamarAsync(agent, message, TimeSpan.FromMinutes(2))));
            Check(terminalClaims.All(c => c.Status == RadarMessageProcessingClaimStatus.PermanentFailure && c.IntentosProcesamiento == 5), "Duplicates cannot reactivate permanent failure");
            Check(!(await pending.ListarAsync(agent, 100)).Any(x => x.MessageId == message.MessageId), "Permanent failure excluded from recovery");
            var terminalVersion = (byte[])(await Scalar(cs, "SELECT VersionFila FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", claim.IdRadarMessageProcessing)))!;
            await MustFail(() => repository.CompletarAsync(claim.IdRadarMessageProcessing, claim.LeaseToken!.Value, "STALE", "{}"), "Terminal result cannot be completed by old lease");
            Check(await repository.RegistrarFalloAsync(claim.IdRadarMessageProcessing, claim.LeaseToken!.Value, "stale after terminal", TimeSpan.FromSeconds(1)) == RadarMessageProcessingFailureStatus.LeaseLost,
                "Terminal failure cannot be changed by stale owner");
            var afterStaleVersion = (byte[])(await Scalar(cs, "SELECT VersionFila FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", claim.IdRadarMessageProcessing)))!;
            Check(terminalVersion.SequenceEqual(afterStaleVersion),
                "Terminal row remains physically unchanged by stale operations");
            Check(!await repository.MarcarTerminadoAsync(agent, message.ChatOrigen, message.MessageId, "SIN_COINCIDENCIA_UTIL"), "No success ACK for failed message");
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageEvent WHERE TipoEvento=N'PROCESAMIENTO_FALLIDO_PERMANENTE' AND IdRadarMessageProcessing=@id", new SqlParameter("@id", claim.IdRadarMessageProcessing))) == 1, "Exactly one terminal audit event");
            var delivery = new RadarMessageDeliveryRepository(Config());
            await MustFail(async () => { await delivery.PrepararAsync(agent, new() { ChatOrigen = message.ChatOrigen, MessageId = message.MessageId, ClaveEntrega = "isolated-lab", SolicitudIndice = 0 }); }, "Failed processing cannot prepare delivery");

            var leaseMessage = Message();
            var old = await repository.ReclamarAsync(agent, leaseMessage, TimeSpan.FromMinutes(2));
            await Expire(cs, old.IdRadarMessageProcessing);
            Check((await pending.ListarAsync(agent, 100)).Any(x => x.MessageId == leaseMessage.MessageId), "Expired lease appears in recovery");
            var newer = await repository.ReclamarAsync(agent, leaseMessage, TimeSpan.FromMinutes(2));
            Check(newer.IntentosProcesamiento == 2 && newer.LeaseToken != old.LeaseToken, "Expired lease replaced once");
            await MustFail(() => repository.CompletarAsync(old.IdRadarMessageProcessing, old.LeaseToken!.Value, "LAB", "{}"), "Stale completion fenced");
            Check(await repository.RegistrarFalloAsync(old.IdRadarMessageProcessing, old.LeaseToken!.Value, "stale", TimeSpan.FromSeconds(1), true) == RadarMessageProcessingFailureStatus.LeaseLost, "Stale failure fenced");
            await repository.CompletarAsync(newer.IdRadarMessageProcessing, newer.LeaseToken!.Value, "LAB", "{}");
            var replay = await repository.ReclamarAsync(agent, leaseMessage, TimeSpan.FromMinutes(2));
            Check(replay.Status == RadarMessageProcessingClaimStatus.Completed && replay.IntentosProcesamiento == 2, "Completed replay does not consume budget");
            await Execute(cs, "UPDATE dbo.RSMAPS_RadarMessageProcessing SET ResultadoCentralJson=NULL WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", newer.IdRadarMessageProcessing));
            Check((await repository.ReclamarAsync(agent, leaseMessage, TimeSpan.FromMinutes(2))).Status == RadarMessageProcessingClaimStatus.Completed, "Missing completed JSON cannot reclaim");
            foreach (string? broken in new string?[] { null, "{}", "{invalid", "{\"motor\":\"LAB\",\"solicitudes\":null}" })
            {
                await Execute(cs, "UPDATE dbo.RSMAPS_RadarMessageProcessing SET ResultadoCentralJson=@json WHERE IdRadarMessageProcessing=@id",
                    new("@json", SqlDbType.NVarChar, -1) { Value = (object?)broken ?? DBNull.Value }, new("@id", newer.IdRadarMessageProcessing));
                Check(!await repository.MarcarTerminadoAsync(agent, leaseMessage.ChatOrigen, leaseMessage.MessageId, "SIN_SOLICITUD_ACCIONABLE"),
                    "Corrupt cache cannot receive ACK even through direct endpoint repository");
                await MustFail(async () => { await delivery.PrepararAsync(agent, new() { ChatOrigen = leaseMessage.ChatOrigen, MessageId = leaseMessage.MessageId, ClaveEntrega = "broken-cache", SolicitudIndice = 0 }); },
                    "Corrupt cache cannot prepare Delivery even through direct endpoint repository");
            }

            var deliveryMessage = Message();
            var deliveryClaim = await repository.ReclamarAsync(agent, deliveryMessage, TimeSpan.FromMinutes(2));
            string deliveryJson = JsonSerializer.Serialize(new RadarInterpretationResult
            {
                Motor = "LAB", Solicitudes = [new() { MessageId = deliveryMessage.MessageId, ChatOrigen = deliveryMessage.ChatOrigen,
                    Operacion = "Venta", TiposPropiedad = ["Casa"], TieneRecomendacionAutomatica = true, IdInmuebleCoincidente = 17, MejorCoincidencia = 90 }]
            });
            await repository.CompletarAsync(deliveryClaim.IdRadarMessageProcessing, deliveryClaim.LeaseToken!.Value, "LAB", deliveryJson);
            RadarDeliveryPrepareRequest prepare = new() { ChatOrigen = deliveryMessage.ChatOrigen, MessageId = deliveryMessage.MessageId,
                ClaveEntrega = "synthetic-valid-delivery", SolicitudIndice = 0, IdInmueble = 17, Puntuacion = 90, PayloadAlerta = "Synthetic isolated alert" };
            var firstDelivery = await delivery.PrepararAsync(agent, prepare);
            var duplicateDelivery = await delivery.PrepararAsync(agent, prepare);
            Check(firstDelivery.IdRadarMessageDelivery == duplicateDelivery.IdRadarMessageDelivery, "Duplicate prepare preserves durable delivery identity");
            await delivery.CompletarAsync(agent, new() { IdRadarMessageDelivery = firstDelivery.IdRadarMessageDelivery, Enviada = true });
            Check((await delivery.PrepararAsync(agent, prepare)).YaEnviado, "Sent delivery cannot be resent by duplicate prepare");
            Check(await repository.MarcarTerminadoAsync(agent, deliveryMessage.ChatOrigen, deliveryMessage.MessageId, "ALERTA_ENTREGADA"), "Valid cache can ACK");
            Check(await repository.MarcarTerminadoAsync(agent, deliveryMessage.ChatOrigen, deliveryMessage.MessageId, "ALERTA_ENTREGADA"), "Valid ACK replay stays idempotent");
            Console.WriteLine("SYNTHETIC_DELIVERY_IDEMPOTENCY_OK");

            var one = new RadarMessageProcessingRepository(Config(1));
            var abandonedMessage = Message();
            var abandoned = await one.ReclamarAsync(agent, abandonedMessage, TimeSpan.FromMinutes(2));
            Check((await one.ReclamarAsync(agent, abandonedMessage, TimeSpan.FromMinutes(2))).Status == RadarMessageProcessingClaimStatus.Busy, "Last live lease not stolen");
            await Expire(cs, abandoned.IdRadarMessageProcessing);
            Check((await one.ReclamarAsync(agent, abandonedMessage, TimeSpan.FromMinutes(2))).Status == RadarMessageProcessingClaimStatus.PermanentFailure, "Abandoned last attempt terminalizes without sixth claim");
            var lastSuccessMessage = Message();
            var lastSuccess = await one.ReclamarAsync(agent, lastSuccessMessage, TimeSpan.FromMinutes(2));
            await one.CompletarAsync(lastSuccess.IdRadarMessageProcessing, lastSuccess.LeaseToken!.Value, "LAB", "{}");
            Check((await one.ReclamarAsync(agent, lastSuccessMessage, TimeSpan.FromMinutes(2))).Status == RadarMessageProcessingClaimStatus.Completed, "Last allowed attempt can succeed");

            var atomic = await one.ReclamarAsync(agent, Message(), TimeSpan.FromMinutes(2));
            await Execute(cs, "CREATE TRIGGER dbo.FailAudit ON dbo.RSMAPS_RadarMessageEvent AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE TipoEvento=N'PROCESAMIENTO_FALLIDO_PERMANENTE') THROW 54900, 'Synthetic audit failure', 1; END;");
            await MustFail(async () => { await one.RegistrarFalloAsync(atomic.IdRadarMessageProcessing, atomic.LeaseToken!.Value, "atomic", TimeSpan.FromSeconds(1)); }, "Failed audit rolls back state transition");
            Check((string?)await Scalar(cs, "SELECT Estado FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", atomic.IdRadarMessageProcessing)) == "PROCESANDO", "Terminal state not committed without audit");
            await Execute(cs, "DROP TRIGGER dbo.FailAudit;");
            Check(await one.RegistrarFalloAsync(atomic.IdRadarMessageProcessing, atomic.LeaseToken!.Value, "atomic", TimeSpan.FromSeconds(1)) == RadarMessageProcessingFailureStatus.PermanentFailure, "Atomic finalization succeeds with audit");

            var version = (byte[])(await Scalar(cs, "SELECT VersionFila FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", claim.IdRadarMessageProcessing)))!;
            async Task Reactivate(bool authorize, byte[] expected, string? reason = "Isolated regression") => await Execute(cs,
                "EXEC dbo.RSMAPS_RadarReactivateProcessing @id,@version,2,@reason,@authorized;",
                new("@id", claim.IdRadarMessageProcessing), new("@version", SqlDbType.Binary, 8) { Value = expected },
                new("@reason", SqlDbType.NVarChar, 1000) { Value = (object?)reason ?? DBNull.Value }, new("@authorized", authorize));
            await MustFail(() => Reactivate(false, version), "Administrative route requires explicit consent", 54802);
            await MustFail(() => Reactivate(true, new byte[8]), "Administrative route fences stale version", 54804);
            foreach (string? blank in new string?[] { null, "", " \t\r\n\u00a0 " })
                await MustFail(() => Reactivate(true, version, blank), "Administrative reason cannot be blank", 54803);
            await Reactivate(true, version);
            await MustFail(() => Reactivate(true, version), "Administrative reactivation is not replayable", 54804);
            var reactivated = await repository.ReclamarAsync(agent, message, TimeSpan.FromMinutes(2));
            Check(reactivated.IntentosProcesamiento == 6 && reactivated.Status == RadarMessageProcessingClaimStatus.Acquired,
                $"Explicit reactivation preserves lifetime counter (status={reactivated.Status}, attempts={reactivated.IntentosProcesamiento})");
            var explicitMessage = Message();
            var explicitClaim = await repository.ReclamarAsync(agent, explicitMessage, TimeSpan.FromMinutes(2));
            Check(await repository.RegistrarFalloAsync(explicitClaim.IdRadarMessageProcessing, explicitClaim.LeaseToken!.Value,
                "Synthetic deterministic defect", TimeSpan.FromSeconds(1), true) == RadarMessageProcessingFailureStatus.PermanentFailure,
                "Explicit permanent defect terminates before budget exhaustion");
            Guid otherAgent = Guid.NewGuid();
            await Execute(cs, "INSERT dbo.RSMAPS_RadarAgentDevice VALUES (@agent);", new SqlParameter("@agent", otherAgent));
            Check((await repository.ReclamarAsync(otherAgent, explicitMessage, TimeSpan.FromMinutes(2))).Status == RadarMessageProcessingClaimStatus.Acquired,
                "Identical MessageId from another Agent has independent scope");
            await Execute(cs, "CREATE USER PoisonUnprivileged WITHOUT LOGIN; GRANT EXECUTE ON dbo.RSMAPS_RadarReactivateProcessing TO PoisonUnprivileged;");
            await MustFail(() => Execute(cs, "EXECUTE AS USER=N'PoisonUnprivileged'; BEGIN TRY EXEC dbo.RSMAPS_RadarReactivateProcessing @id,@version,1,N'Unauthorized test',1; REVERT; END TRY BEGIN CATCH REVERT; THROW; END CATCH;",
                new("@id", explicitClaim.IdRadarMessageProcessing), new("@version", SqlDbType.Binary, 8) { Value = new byte[8] }),
                "Execute permission alone does not authorize administrative reactivation", 54802);
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageEvent WHERE TipoEvento=N'REACTIVACION_ADMINISTRATIVA' AND IdRadarMessageProcessing=@id", new SqlParameter("@id", claim.IdRadarMessageProcessing))) == 1, "Administrative reactivation audited");

            var historicalMessage = Message();
            var historical = await repository.ReclamarAsync(agent, historicalMessage, TimeSpan.FromMinutes(2));
            await Execute(cs, "UPDATE dbo.RSMAPS_RadarMessageProcessing SET Estado=N'FALLIDO_REINTENTABLE', IntentosProcesamiento=12, LeaseToken=NULL, LeaseHastaUtc=NULL, ReintentarDespuesUtc=NULL WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", historical.IdRadarMessageProcessing));
            var historicalResult = await repository.ReclamarAsync(agent, historicalMessage, TimeSpan.FromMinutes(2));
            Check(historicalResult.Status == RadarMessageProcessingClaimStatus.PermanentFailure && historicalResult.IntentosProcesamiento == 12,
                "Synthetic historical over-budget row terminates without another processing attempt");

            var raceMessage = Message();
            var race = await repository.ReclamarAsync(agent, raceMessage, TimeSpan.FromMinutes(2));
            async Task CompleteRace()
            {
                try { await repository.CompletarAsync(race.IdRadarMessageProcessing, race.LeaseToken!.Value, "LAB", "{}"); }
                catch (InvalidOperationException) { /* The failure transition won the same row lock. */ }
            }
            await Task.WhenAll(CompleteRace(), repository.RegistrarFalloAsync(race.IdRadarMessageProcessing, race.LeaseToken!.Value,
                "Synthetic terminal race", TimeSpan.FromSeconds(1), true));
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageEvent WHERE IdRadarMessageProcessing=@id AND TipoEvento IN (N'PROCESAMIENTO_COMPLETADO',N'PROCESAMIENTO_FALLIDO_PERMANENTE')", new SqlParameter("@id", race.IdRadarMessageProcessing))) == 1,
                "Completion and permanent failure cannot both commit");

            var adminRace = await repository.ReclamarAsync(agent, Message(), TimeSpan.FromMinutes(2));
            await repository.RegistrarFalloAsync(adminRace.IdRadarMessageProcessing, adminRace.LeaseToken!.Value, "Synthetic admin race", TimeSpan.FromSeconds(1), true);
            var adminVersion = (byte[])(await Scalar(cs, "SELECT VersionFila FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", adminRace.IdRadarMessageProcessing)))!;
            async Task<int> AdminRace()
            {
                try
                {
                    await Execute(cs, "EXEC dbo.RSMAPS_RadarReactivateProcessing @id,@version,2,N'Concurrent isolated admin',1;",
                        new("@id", adminRace.IdRadarMessageProcessing), new("@version", SqlDbType.Binary, 8) { Value = adminVersion });
                    return 1;
                }
                catch (SqlException ex) when (ex.Number == 54804) { return 0; }
            }
            Check((await Task.WhenAll(AdminRace(), AdminRace())).Sum() == 1, "Only one concurrent administrative reactivation commits");
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageEvent WHERE IdRadarMessageProcessing=@id AND TipoEvento=N'REACTIVACION_ADMINISTRATIVA'", new SqlParameter("@id", adminRace.IdRadarMessageProcessing))) == 1,
                "Concurrent administration produces exactly one event");
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageDelivery")) == 1,
                "Only the explicitly simulated valid delivery fixture exists; no failed message produced Delivery");
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM sys.check_constraints WHERE name IN (N'CK_RadarProcessing_MaxIntentos',N'CK_RadarProcessing_PermanentFailure') AND is_disabled=0 AND is_not_trusted=0")) == 2,
                "Migration checks enabled and trusted");
            Check(Convert.ToInt32(await Scalar(cs, "SELECT COUNT(*) FROM dbo.RSMAPS_RadarMessageProcessing WHERE Estado=N'FALLIDO_PERMANENTE' AND (TerminadoUtc IS NULL OR UltimoError IS NULL OR DownstreamAckUtc IS NOT NULL OR LeaseToken IS NOT NULL)")) == 0, "Terminal invariants");
            // Demonstration of the rollback incompatibility: the legacy backend's unguarded
            // state/counter update is not prevented by the additive schema alone.
            await Execute(cs, "UPDATE dbo.RSMAPS_RadarMessageProcessing SET Estado=N'PROCESANDO',IntentosProcesamiento=IntentosProcesamiento+1,TerminadoUtc=NULL,UltimoError=NULL,LeaseToken=NEWID(),LeaseHastaUtc=DATEADD(SECOND,60,SYSUTCDATETIME()) WHERE IdRadarMessageProcessing=@id;",
                new SqlParameter("@id", historical.IdRadarMessageProcessing));
            Check((string?)await Scalar(cs, "SELECT Estado FROM dbo.RSMAPS_RadarMessageProcessing WHERE IdRadarMessageProcessing=@id", new SqlParameter("@id", historical.IdRadarMessageProcessing)) == "PROCESANDO",
                "Legacy rollback is unsafe; fixed backend must not coexist with an old writer");
            Console.WriteLine("LEGACY_BACKEND_ROLLBACK_UNSAFE_CONFIRMED");
            Console.WriteLine("POISON_MESSAGE_SQL_REGRESSION_OK");
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (created) await Execute(localMaster, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];");
        }
    }

    private static Task Expire(string cs, long id) => Execute(cs,
        "UPDATE dbo.RSMAPS_RadarMessageProcessing SET LeaseHastaUtc=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE IdRadarMessageProcessing=@id;", new SqlParameter("@id", id));
    private static async Task Script(string cs, string sql)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        foreach (string batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            await using var cmd = new SqlCommand("EXEC sys.sp_set_session_context @key=N'RadarPoisonTest',@value=1;\n" + batch, connection);
            // CREATE PROCEDURE must start its own batch.
            if (batch.TrimStart().StartsWith("CREATE OR ALTER", StringComparison.OrdinalIgnoreCase)) cmd.CommandText = batch;
            await cmd.ExecuteNonQueryAsync();
        }
    }
    private static async Task Execute(string cs, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters);
        await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<object?> Scalar(string cs, string sql, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters);
        return await cmd.ExecuteScalarAsync();
    }
    private static async Task MustFail(Func<Task> action, string name, int? expectedSqlError = null)
    {
        bool failed = false;
        try { await action(); }
        catch (SqlException ex) when (!expectedSqlError.HasValue || ex.Errors.Cast<SqlError>().Any(e => e.Number == expectedSqlError)) { failed = true; }
        catch (InvalidOperationException) when (!expectedSqlError.HasValue) { failed = true; }
        Check(failed, name);
    }
}
