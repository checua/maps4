# RADAR: bounded central processing

Development milestone based on `5299f15`. No production SQL, deploy, message 356,
WhatsApp profile, dependency updates or commits are part of this change.

## State and attempt contract

The identity remains `(IdAgent, ChatOrigen, MessageId)` and its existing unique
index. Claims still use `Serializable` plus `UPDLOCK,HOLDLOCK`. A successful claim
consumes one attempt, including the initial claim. Busy requests and replay do
not consume attempts. `COMPLETADO` and `FALLIDO_PERMANENTE` never become claims.
A corrupt completed cache produces HTTP 409 for investigation, not reinterpretation.

`Radar:Processing:MaxAttempts` (`Radar__Processing__MaxAttempts` in environment)
defaults to **5**, range 1–100, and is validated when constructing the repository.
The value is persisted in `MaxIntentosProcesamiento` when the message is created.
Configuration changes do not silently reopen or extend existing messages.
Migration 57 gives existing rows a budget of five without changing their state,
counter, result, workflow ACK or deliveries. An exhausted historical pending row
becomes terminal when next claimed after its lease/retry wait expires.

The last active lease may still complete successfully. Expired leases cannot
complete or fail a replacement owner's attempt. After the final failed attempt,
or a claim of an exhausted abandoned attempt, one transaction writes
`FALLIDO_PERMANENTE`, bounded error detail, `TerminadoUtc`, `ActualizadoUtc`, clears
lease/retry scheduling and appends `PROCESAMIENTO_FALLIDO_PERMANENTE`. Audit failure
rolls the entire transition back. No workflow success ACK or delivery is created.

Processing/downstream recovery explicitly uses READ COMMITTED and
READCOMMITTEDLOCK with READPAST. Isolated tests exposed the inherited SERIALIZABLE
isolation of a pooled claim connection; these hints also support databases using RCSI.
Recovery includes DETECTADO rows, but never permanent failures.

## Error and HTTP behavior

| Claim/outcome | HTTP | X-Radar-Processing | Listener |
|---|---|---|---|
| Acquired and completed | 200 | fresh | Existing downstream flow |
| Completed replay | 200 | replay | Existing idempotent downstream flow |
| Busy | 503 + Retry-After | busy | Retry |
| Timeout/provider/configuration/unknown failure with budget | 503 + Retry-After | timeout/retry/unavailable | Retry |
| Explicit permanent defect or exhausted budget | 422 | permanent-failure | Typed exception; suppress locally, no Delivery or success ACK |
| Corrupt completed result | 409 | invalid-cache | Retry transport; investigate cache; never new central claim |

Permanent responses also require the shared JSON code
`RADAR_PROCESSING_PERMANENT_FAILURE`, `permanent=true`, `retry=false`. An arbitrary
422 or malformed response is not sufficient for terminal suppression. Backend
exceptions are not echoed as free-form HTTP error details.

Only `RadarPermanentMessageException`, reserved for known deterministic input
defects, requests immediate permanent failure. Existing model validation/provider
errors, cancellation, malformed LLM JSON, HTTP errors and unknown exceptions remain
retryable until budget exhaustion. No message-text/emojis heuristic is introduced.
The exception is an explicit extension point; this milestone does not invent a
new rule declaring a real estate request permanently invalid.

Central durable mode disables local fallback even if a local key or fallback
setting is present. Terminal exceptions bypass downstream processing at both
normal capture and processing recovery. Local known-ID suppression is not a
workflow ACK and is rebuilt by querying the durable backend after restart.
Delivery preparation still requires `COMPLETADO`. Delivery keys, send confirmation,
reconciliation and duplicate-send protections are unchanged.

## SQL compatibility and administrative route

Review/apply **57** after schemas **44 and 45**, before the new Web binary. It is
idempotent, checks the processing table, event table, 40-character state column,
ACK column and unique idempotency index. It adds a budget and terminal invariant
checks; there is no old state CHECK to widen in schema 44. State and event names
fit the existing nvarchar(40)/nvarchar(60) columns. No script executes at startup.
Actual production schema drift has not been inspected by this milestone.

**58** is an optional administrative procedure, separately reviewed/installed;
there is no app/Agent endpoint, automatic call or application/public EXECUTE grant.
It requires database CONTROL, explicit consent, a reason and expected rowversion.
Only permanent failures without a result, workflow ACK or any delivery qualify.
It preserves the lifetime counter, sets an additional bounded budget, and writes
`REACTIVACION_ADMINISTRATIVA` including SQL principal, reason and prior counter in
the same transaction. Stale/duplicate invocations fail. Never reactivate message
356 or other real records as part of development or testing.

Deploy order, when separately authorized: SQL 57, Web, compatible Listener.
Older Listeners still treat HTTP 422 as a retry and lack local suppression.
Rolling an old Web back is unsafe: its old claim logic can reopen permanent
states. Keep the fixed Web repository while rolling back unrelated changes, or
pause intake pending a reviewed rollback. Do not remove new SQL state or audit data.

## Isolated verification

`RADAR_LAB_MODE=poison-message-regression` exercises controller and transport with
stubs only. `poison-message-sql-regression` uses a hard-coded local Windows LocalDB
instance, generates/drops a unique `RadarPoisonRegression_*` database and does not
read application connection strings or Agent credentials. It applies schemas
44/45/57/58 twice and tests budgets, concurrency, duplicate scope, expired/stale
leases, replay, terminal recovery exclusion, failure/audit atomicity, delivery/ACK
rejection, versioned administrative reactivation and insufficient privilege.
The legacy mapsMarkers database guard is adjusted in memory only inside this
isolated runner. SQL migration 57 itself requires an explicit test session marker
for the isolated database prefix. No production configuration is accepted.

Validation: solution Release retains the baseline 18 warnings (14 nullable
warnings and four NU1902/NU1903 emissions for the existing Pkcs dependency), no
new warning category. Poison controller/transport and LocalDB integrations pass,
as do existing matching, payment, Cynthia, delivery, actionability, specificity,
private-development, property-data/UI, deep-link and synthetic WhatsApp capture
regressions. `e2e-hard-no-orillas` with rules still fails to retain the preference
"Lo más nuevo posible". Its test, extractor and rule interpreter are unchanged
from the base; the failure is outside this milestone. Lab validation uses an
isolated output under its ignored bin directory because a failed test process
retained a DLL lock in the ordinary Lab output.

## Hito 2: revisión de integración y seguridad

Revisión local sobre `5299f15c88c67034bb1219bbe8a9750f61e8656a`, rama
`fix/radar-poison-message-recovery`. Se revisaron los 17 archivos iniciales,
incluidos los no versionados, y se ampliaron tres archivos adicionales:
`RadarCentralResultReader.cs`, `RadarMessageDeliveryRepository.cs` y
`RadarCentralProcessingService.cs`. No hay commit, push ni operaciones productivas.

### Hallazgos y correcciones realizadas

| Severidad | Defecto confirmado | Corrección local |
|---|---|---|
| Bloqueante | JSON parseable como `{}` podía convertirse por defaults en resultado vacío, llegar a ACK o permitir prepare directo. Identidad de solicitudes discordante también podía parecer ausencia de solicitudes. | Lector estructural compartido para respuesta HTTP, resultado fresco, recovery, prepare y terminal ACK. Rechaza envolvente incompleta, duplicada, listas nulas e identidad discordante. Cache COMPLETADO inválido permanece sin reinterpretar: 409/investigación, sin Delivery ni ACK. |
| Importante | El presupuesto dependía de cancelación cooperativa; Matching no recibe CancellationToken. | Espera acotada con WaitAsync a 75 segundos, observación de excepciones tardías y comprobaciones de cancelación entre etapas. Un resultado tardío nunca se persiste. |
| Importante | Un fallo del almacenamiento de autenticación quedaba fuera del manejo de errores. | Autenticación dentro del try; fallo sanitizado 503 sin consumir claim. |
| Importante | Script 58 separaba validación y CREATE OR ALTER con GO; un cliente que continuara podía ejecutar el segundo batch. | Un único batch con DDL dinámico después de las validaciones. EXECUTE AS CALLER explícito. |
| Importante | Script 57 confiaba demasiado en nombres de índices y constraints existentes. | Exige índice único real, habilitado, no filtrado y claves exactas; verifica definiciones de los CHECK y rechaza drift. |
| Menor | Pruebas negativas podían aprobar por un error SQL diferente al esperado. | Verificación de números de error concretos y pruebas de permisos, drift, rowversion, concurrencia y rollback de auditoría. |

El lector conserva compatibilidad con nombres PascalCase, listas de solicitudes
explícitamente vacías y Accionabilidad histórica ausente. No recalcula scoring ni
introduce filtros comerciales. No demuestra integridad criptográfica ni validez
semántica de toda combinación posible del payload.

Script 58 rechaza motivos vacíos, incluyendo espacios, tabs, saltos de línea y
NBSP. Verifica rowversion real, repite estado y rowversion en UPDATE, exige una
fila afectada y audita login/principal, contador anterior, presupuesto anterior y
nuevo. Dos reactivaciones concurrentes con la misma versión producen un único
éxito. No concede EXECUTE ni implementa una ruta HTTP. Un usuario de aplicación
con CONTROL/db_owner podría invocarlo: no instalar 58 hasta comprobar privilegios
reales y separación administrativa; la autorización no se deduce del nombre de rol.

### Garantías comprobadas

- Reclamar usa bloqueo serializable sobre la identidad única. COMPLETADO y
  FALLIDO_PERMANENTE no se reactivan por duplicados; llamadas Busy no gastan intentos.
- Un lease vencido o reemplazado no completa, registra fallo ni cambia rowversion
  de un terminal. Completar y terminalizar concurrentemente tienen un único ganador.
- Estado permanente y evento se confirman o revierten juntos. Un fallo de INSERT
  de auditoría revierte la transición; recovery nunca selecciona permanentes.
- Configuración ausente, timeout y excepción general registran fallo transitorio;
  sólo un defecto explícito o el presupuesto agotado da 422. Fallo de cleanup no
  fabrica éxito ni estado terminal: el lease expira para recovery.
- Sólo el contrato 422 completo y reconocido dispara la excepción terminal del
  Listener. No se ejecuta fallback local, prepare ni ACK de éxito en esa ruta.
- Se mantienen ClaveEntrega y la idempotencia durable. Pruebas sintéticas verifican
  prepare duplicado, entrega simulada ENVIADO y ACK idempotente, sin envío real.

### Matriz de compatibilidad

| Backend | Listener | Dictamen |
|---|---|---|
| Antiguo | Antiguo | Sin garantía contra poison; no es el estado objetivo. |
| Nuevo + SQL 57 | Antiguo | Sólo transición controlada: 422 se vuelve retry; exigir fallback local=0 y Delivery durable. No ofrece supresión local terminal ni toda la validación de cache del nuevo consumidor. |
| Antiguo | Nuevo | No soportado: el backend no impone presupuesto ni preserva terminales nuevos. |
| Nuevo + SQL 57 | Nuevo | Estado objetivo; contrato terminal reconocido y límites durables. |
| Nuevo sin SQL 57 | Cualquiera | No soportado: falta columna/esquema requerido. |

**Bloqueante operativo pendiente:** rollback al backend antiguo es inseguro.
La regresión SQL reproduce que su UPDATE puede reabrir un permanente. Las CHECK
aditivas no sustituyen la protección del repositorio. No mezclar escritores antiguos
y nuevos ni usar el Web anterior como rollback automático.

### Mensaje histórico 356

No se consultó ni modificó el mensaje real; su estado actual no está demostrado.
Un fixture sintético con 12 intentos y presupuesto 5 prueba el comportamiento
esperado: pendiente elegible pasa a permanente en el siguiente claim, sin intento
adicional ni procesamiento. Lease vigente da Busy. COMPLETADO válido da replay;
COMPLETADO corrupto exige investigación. Un permanente devuelve 422 sin cambio.
Antes de desplegar se requiere inventario autorizado de 356 y de los demás
pendientes: activar recovery puede terminalizar históricos agotados. Este informe
no autoriza dicha acción ni una reactivación administrativa.

### Pruebas reproducibles

Desde este worktree, sin configuración productiva:

```powershell
dotnet build .\maps4.sln --configuration Release --nologo
$reviewBin = Join-Path $PWD ('RSMaps.Radar.Lab/bin/review-' + [guid]::NewGuid().ToString('N'))
dotnet build RSMaps.Radar.Lab/RSMaps.Radar.Lab.csproj -c Release --no-restore -o $reviewBin
foreach ($mode in @('poison-message-regression', 'poison-message-sql-regression', 'poison-controller-timeout-regression')) {
    $env:RADAR_LAB_MODE = $mode
    dotnet (Join-Path $reviewBin 'RSMaps.Radar.Lab.dll')
    if ($LASTEXITCODE -ne 0) { throw "Regression failed: $mode" }
}
Remove-Item Env:RADAR_LAB_MODE
```

SQL requiere LocalDB instalado; el runner crea y elimina exclusivamente una base
con nombre aleatorio RadarPoisonRegression_*. La prueba de timeout tarda 75 segundos
y usa una dependencia sintética que ignora cancelación; completa su Task después
del retorno del controlador y comprueba que no escribe resultado tardío.

También pasan 14 modos aislados: poison-message, poison-message-sql, matching,
payment-hard-constraints, cynthia-recommendation-baseline, delivery-flow,
solicitud-accionable, solicitud-accionable-integration,
auto-recomendacion-especificidad, fracc-privado, tipo-fraccionamiento-data,
tipo-fraccionamiento-integration, deep-links y whatsapp-capture (sufijo -regression
salvo cynthia-recommendation-baseline). WhatsApp usa DOM sintético headless, nunca
el perfil real. No se ejecutó el Listener productivo.

`e2e-hard-no-orillas` falla de forma idéntica en base 5299f15 y en esta revisión:
preferencia no conservada. Se compiló una instantánea temporal de fuentes de la
base obtenida por git archive, sin crear/modificar otro worktree, y un harness de
reflexión aislado invocó el mismo test con RuleBasedRadarInterpreter y live AI=0.
Se atrapó el fallo esperado sin lanzar los entrypoints de Web/Listener.
Marcadores: BASELINE_5299F15_E2E_PREEXISTING_FAILURE_CONFIRMED y
CURRENT_E2E_PREEXISTING_FAILURE_CONFIRMED. El test sigue FAIL; no es una regresión
introducida por poison y no se corrigió fuera de alcance.

### Secuencia propuesta de despliegue y rollback (NO ejecutada)

1. Verificar READ-ONLY el esquema real, índices/constraints, permisos del principal,
   configuración de intentos, versiones y colas, incluyendo 356 y caches inválidos.
   Coordinar scripts por nombre completo: el número 57 puede colisionar con otra rama.
2. Preparar backups de Web/Listener/configuración/launcher/perfil y manifiestos;
   verificar restauración y disponer de un rollback Web que conserve estas guardas.
3. Suspender de forma controlada todos los escritores/consumidores y drenar o dejar
   expirar leases. No desplegar con procesos o slots viejos todavía escribiendo.
4. Aplicar SQL 57 autorizado, con postchecks de CHECK confiables y presupuesto.
   SQL 58 es opcional y requiere aprobación y permisos administrativos separados.
5. Publicar Web nuevo, comprobar autenticación, 200/replay, 503, 422 y cache inválido
   con casos de prueba autorizados. Ningún script se ejecuta al arrancar.
6. Actualizar Listener compatible preservando configuración, launcher y perfil;
   central durable, fallback=0. Revisar Delivery pendientes antes de habilitar recovery
   para evitar alertas históricas inesperadas. Observar leases, eventos y contadores.
7. Ante fallo, suspender intake. Restaurar sólo un backend compatible con los nuevos
   terminales. Si no existe rollback seguro, mantener procesamiento detenido y escalar;
   no arrancar el Web antiguo ni borrar tabla/columnas/eventos o resetear intentos.

### Riesgos y gates pendientes

No se verificó producción: esquema/permisos reales, backups recuperables, colas y
estado de 356 son gates anteriores al despliegue. No hay autorización de producción.
Una dependencia no cooperativa puede continuar consumiendo recursos después del
timeout, aunque ya no puede confirmar resultados; cancelación propagada a toda la
capa de Matching queda como mejora posterior. El presupuesto acota los intentos
durables, no elimina todo trabajo físico que ignore cancelación. Un fallo al guardar
auditoría requiere recovery tras expirar lease; no se afirma exactamente-una-vez
para cómputo externo. La entrega conserva sus protecciones existentes, sin prometer
exactamente-una-vez a través de WhatsApp. La vulnerabilidad Pkcs preexistente y el
fallo e2e conocido permanecen fuera de alcance.

Resultado final de esta revisión: build exacto de maps4.sln Release y build Lab
aislado PASS, ambos con 0 errores y 18 advertencias preexistentes; 0 nuevas.
Las 14 regresiones listadas pasan. Tras el último ajuste se repitieron poison
controller/transport y SQL: PASS. La prueba adicional no cooperativa de timeout
dio POISON_CONTROLLER_TIMEOUT_REGRESSION_OK elapsed_seconds=75.1. Diff --check
de archivos tracked y nuevos PASS; sólo avisos de conversión LF/CRLF, sin defectos
de whitespace. HEAD sigue 5299f15, index vacío, 20 archivos de trabajo modificados
o nuevos. Producción/listener real/mensaje 356/otros worktrees: sin cambios.
