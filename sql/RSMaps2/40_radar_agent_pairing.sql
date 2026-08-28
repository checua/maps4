/* ============================================================
   RSMaps 2.0 - Paso 40
   RADAR AGENT: VINCULACIÓN SEGURA DE DISPOSITIVOS

   Objetivo:
   - Vincular un RADAR Agent a un usuario/cuenta autenticados.
   - Usar códigos de un solo uso con expiración.
   - Guardar únicamente hashes de códigos y tokens.
   - Evitar que el Agent pueda autoasignarse una cuenta editando JSON.
   ============================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> 'mapsMarkers'
BEGIN
    THROW 54000, 'Este script debe ejecutarse en la base mapsMarkers.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarAgentDevice
        (
            IdAgent          uniqueidentifier NOT NULL,
            IdAsesor         int NOT NULL,
            IdCuenta         int NOT NULL,
            NombreAgent      nvarchar(120) NOT NULL,
            EquipoNombre     nvarchar(200) NULL,
            TokenHash        char(64) NOT NULL,
            Activo           bit NOT NULL CONSTRAINT DF_RSMAPS_RadarAgentDevice_Activo DEFAULT (1),
            FechaAltaUtc     datetime2(0) NOT NULL CONSTRAINT DF_RSMAPS_RadarAgentDevice_FechaAltaUtc DEFAULT (SYSUTCDATETIME()),
            UltimoUsoUtc     datetime2(0) NULL,
            RevocadoUtc      datetime2(0) NULL,

            CONSTRAINT PK_RSMAPS_RadarAgentDevice PRIMARY KEY (IdAgent),
            CONSTRAINT FK_RSMAPS_RadarAgentDevice_Usuario
                FOREIGN KEY (IdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
            CONSTRAINT FK_RSMAPS_RadarAgentDevice_Cuenta
                FOREIGN KEY (IdCuenta) REFERENCES dbo.RSMAPS_Cuenta(IdCuenta)
        );

        CREATE UNIQUE INDEX UX_RSMAPS_RadarAgentDevice_TokenHash
            ON dbo.RSMAPS_RadarAgentDevice(TokenHash);

        CREATE INDEX IX_RSMAPS_RadarAgentDevice_UsuarioCuenta
            ON dbo.RSMAPS_RadarAgentDevice(IdAsesor, IdCuenta, Activo);
    END;

    IF OBJECT_ID(N'dbo.RSMAPS_RadarAgentPairing', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RSMAPS_RadarAgentPairing
        (
            IdPairing        bigint IDENTITY(1,1) NOT NULL,
            CodigoHash       char(64) NOT NULL,
            IdAsesor         int NOT NULL,
            IdCuenta         int NOT NULL,
            NombreAgent      nvarchar(120) NOT NULL,
            CreadoUtc        datetime2(0) NOT NULL CONSTRAINT DF_RSMAPS_RadarAgentPairing_CreadoUtc DEFAULT (SYSUTCDATETIME()),
            ExpiraUtc        datetime2(0) NOT NULL,
            ConsumidoUtc     datetime2(0) NULL,
            IdAgentCreado    uniqueidentifier NULL,

            CONSTRAINT PK_RSMAPS_RadarAgentPairing PRIMARY KEY (IdPairing),
            CONSTRAINT FK_RSMAPS_RadarAgentPairing_Usuario
                FOREIGN KEY (IdAsesor) REFERENCES dbo.RSMAPS_Usuario(idAsesor),
            CONSTRAINT FK_RSMAPS_RadarAgentPairing_Cuenta
                FOREIGN KEY (IdCuenta) REFERENCES dbo.RSMAPS_Cuenta(IdCuenta),
            CONSTRAINT FK_RSMAPS_RadarAgentPairing_Agent
                FOREIGN KEY (IdAgentCreado) REFERENCES dbo.RSMAPS_RadarAgentDevice(IdAgent)
        );

        CREATE UNIQUE INDEX UX_RSMAPS_RadarAgentPairing_CodigoHash
            ON dbo.RSMAPS_RadarAgentPairing(CodigoHash);

        CREATE INDEX IX_RSMAPS_RadarAgentPairing_UsuarioCuenta
            ON dbo.RSMAPS_RadarAgentPairing(IdAsesor, IdCuenta, ConsumidoUtc, ExpiraUtc);
    END;

    COMMIT TRANSACTION;

    SELECT
        OBJECT_ID(N'dbo.RSMAPS_RadarAgentDevice', N'U') AS RadarAgentDeviceObjectId,
        OBJECT_ID(N'dbo.RSMAPS_RadarAgentPairing', N'U') AS RadarAgentPairingObjectId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
