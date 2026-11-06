# RADAR — Hito 5J: prototipo local de identidad Web

2026-11-06, DESKTOP-E32803V. Rama diagnostic/radar-web-sql-identity.
Base 6d737f7491238e71cdba4bbd4f844bf3022bb27c; rama poison preservada sin integrar.
IMPLEMENTED_LOCAL=YES. PRODUCTION_PACKAGE_READY=NO. READY_TO_DEPLOY=NO.
El SHA publicado se obtiene del historial de este archivo y del informe de entrega.

## Base productiva: evidencia y faltantes

Sólo se reutilizó evidencia de 5G/5H/5I. No se descargaron archivos del Web, no se
consultó Azure ni se transfirió nada. Se conoce hosting inprocess y el registro de
publicación de septiembre, pero no un manifiesto/hash del binario activo ni su fuente
exacta. El rollback histórico de 345 archivos y su SHA no prueban correspondencia con
el despliegue actual. La copia PITR quedó eliminada en 5H.4.

Faltan: inventario/hash de paquete desplegado obtenido por un operador autorizado,
fuente/commit y procedimiento de build reproducible, versión SDK/dependencias usadas,
configuración de hosting y proveedores efectivos sin valores secretos, instancias
activas y paquete de rollback íntegro verificado. La rama actual contiene poison y
NO se presume base de producción. El prototipo no debe publicarse en Azure tal cual.

## Archivos y comportamiento

- Diagnostics/WebSqlIdentityHostedService.cs: BackgroundService tras ApplicationStarted.
  Sólo intenta activar en w3wp, sitio rsmap, slot Production, instancia válida y con
  manifiesto privado preexistente. No endpoint ni polling/activación por petición.
- Diagnostics/WebSqlIdentityRunner.cs: manifiesto estricto, caducidad <=15 minutos,
  hash SHA-256 del assembly maps4 exacto, sitio/slot e instancias explícitas. Marcador
  CreateNew persistente antes de SQL, una tentativa por runId/instancia incluso tras
  fallo/reinicio. Global <=25 s; retirada/cambio de manifiesto invalida evidencia.
- Diagnostics/DiagnosticPrivateFiles.cs: Windows, fuera de ContentRoot y WebRoot,
  sin reparse points; propietario/ACEs allow sólo usuario del proceso, SYSTEM y
  Administradores. No cambia ACL; rechaza directorio no privado. Comprueba archivos
  y su ACL antes de escribir bytes del resultado. ACL real App Service aún pendiente.
- Diagnostics/WebSqlIdentityQuery.cs: sólo SELECT fijos; DB_NAME guardia, identidad,
  sesión, indicadores administrativos y 27 permisos de objetos. No consultas de datos,
  DML, DDL, procedimientos ni SQL recibido desde manifiesto. NULL se preserva.
- Diagnostics/DiagnosticAssemblyInfo.cs: acceso interno al Lab para pruebas.
- RadarMessageProcessingRepository: dos métodos internos nuevos usan _cadenaSQL,
  el mismo campo capturado por el constructor y usado por métodos de negocio.
  Comparación exacta con IConfiguration antes y después; sin reemplazar autenticación,
  servidor, usuario ni base. Sólo conexión diagnóstica: timeout 10 s, retry 0, pooling
  false y PersistSecurityInfo false. No se modifica ningún método de negocio.
- Program.cs: registra el servicio. Sin manifiesto válido queda inactivo; no crea
  directorios, no abre conexión y no emite salida diagnóstica.
- RSMaps.Radar.Lab/Program.cs, WebSqlIdentityRegression.cs y WebSqlIdentityIisHarness.cs:
  modos exclusivamente sintéticos; harness sólo acepta proceso iisexpress y raíz
  temporal radar-iis-*. No forman parte de la publicación Web.
- tools/Test-RadarWebIdentityIis.ps1: prueba reproducible IIS Express, configuración
  copiada a temporal, ANCM existente, puerto localhost, sin cambios de IIS compartido.

El validador productivo exige servidor/base esperados, Encrypt no Optional y
TrustServerCertificate=false. No corrige automáticamente opciones de conexión.
La lectura ARM histórica 5H.3 mostró TrustServerCertificate=true con el analizador
usado entonces; no prueba el valor efectivo del worker, pero anticipa un posible
bloqueo real de esta guardia. Debe resolverse en la revisión autorizada, no omitir la
comprobación ni usar la conexión ajustada desde oficina como sustituto.

## Manifiesto y evidencia privada (diseño operativo, NO activar)

Ruta fija: %HOME%/data/radar-sql-identity/manifest.json, directorio fuera del sitio.
No se distribuye un manifiesto activo. Forma JSON con nombres exactos:

```json
{
  "RunId": "identificador-unico",
  "Site": "rsmap",
  "Slot": "Production",
  "AssemblySha256": "REEMPLAZAR_CON_HASH_VERIFICADO_NO_ES_UN_MANIFIESTO_VALIDO",
  "Instances": ["INSTANCIA_VERIFICADA"],
  "IssuedUtc": "2026-01-01T00:00:00Z",
  "ExpiresUtc": "2026-01-01T00:10:00Z"
}
```

El ejemplo es inválido/expirado intencionalmente. No admite SQL, credenciales, ruta de
salida ni destino alternativo. Resultado: {runId}-{instance}.json, escritura atómica,
<=32 KiB, lista explícita de campos; marcador .consumed persistente, temporal .pending.
Sólo proceso Web y operadores SCM ya autorizados leen salida privada. No stdout,
ILogger, telemetría o errores libres. Status OBSERVED significa lectura efectuada,
no permisos suficientes ni autorización de despliegue. Revisión privada correlacionará
PID/inicio/instancia/hash con worker no-SCM y clasificará los permisos 1/0/NULL.
Ninguna muestra privada ni principal real se publica en Git.

## Pruebas ejecutadas y resultados

| Verificación | Resultado |
| --- | --- |
| Restore y build Release Web/Lab | Aprobado, cero errores; 14 advertencias de nulabilidad preexistentes en recompilación Web, ninguna nueva de diagnóstico |
| Precedencia JSON/SQLCONNSTR/argumento | Ganador esperado; conexión capturada conserva servidor/base/autenticación; cambio posterior rechazado |
| LocalDB RADAROfficeTests | Identidad db_owner y contexto de usuario restringido comprobados en bases nuevas sintéticas; INSERT denegado, ausentes/invisibles 0 o NULL según SQL; NULL preservado en evidencia |
| Sólo lectura | Guardias de DB_NAME y contrato SELECT fijo; filas de tablas sintéticas siguen en cero; DDL/EXECUTE AS sólo en preparación/retirada de fixtures del Lab |
| Manifiesto | Ausente, expirado, futuro, TTL largo, hash/sitio/slot/instancia/runId inválidos: cero lecturas |
| Carrera/reinicio | 8 contendientes: una ejecución; segundo runner consumido; nuevo proceso IIS Express también CONSUMED |
| Cancelación y timeout | Cancelación previa, durante ejecución, timeout y apertura SQL real contra puerto loopback inerte: sin reintentos |
| Privacidad | Rechaza carpeta pública/ACL Everyone; claves desconocidas y errores con secreto señuelo sin filtración; consola diagnóstica vacía |
| HTTP | Kestrel loopback e IIS Express devuelven 404 para rutas privadas; no endpoint diagnóstico añadido |
| IIS inprocess real local | OBSERVED en iisexpress; consulta desde repositorio a base sintética, retirada confirmada. Nuevo proceso: CONSUMED. No se demuestra todavía w3wp de Azure |
| Regresiones existentes | poison-message, poison-message-sql, sql-identity, delivery-flow, poison-controller-timeout: aprobadas; timeout 75.1 s |
| NuGet | Web, Lab, Listener e Intelligence sin vulnerabilidades reportadas, incluyendo transitivas; ninguna dependencia cambiada |
| Revisión | Diff/check y escaneo dedicado de secretos antes de publicar |

Los errores iniciales del ensayo fueron corregidos: compilación por Span/await,
expectativa demasiado estricta de NULL en visibilidad SQL, y configuración temporal
IIS sin registro ANCM (500.19/500.21). Se resolvieron localmente sin instalar componentes.
No se usaron identidades de desarrollo como prueba de producción: todos los casos
anteriores son evidencia sintética del mecanismo. El servicio productivo mantiene
allowlist fija; el harness local ejecuta componentes internos con base sintética y no
salta esas guardias en el Web publicado. Prueba end-to-end en rsmap aún NO EJECUTADA.

Reproducción desde raíz, sólo entorno local aprobado:

```powershell
dotnet restore RSMaps.Radar.Lab/RSMaps.Radar.Lab.csproj -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet build RSMaps.Radar.Lab/RSMaps.Radar.Lab.csproj -c Release --no-restore
$env:RADAR_SQL_TEST_INSTANCE = 'RADAROfficeTests'
$env:RADAR_LAB_MODE = 'web-sql-identity-regression'
dotnet RSMaps.Radar.Lab/bin/Release/net10.0/RSMaps.Radar.Lab.dll
pwsh -NoProfile -File tools/Test-RadarWebIdentityIis.ps1
```

## Paquete propuesto y retirada verificable

Entrega actual: código y pruebas en rama independiente, no paquete de despliegue.
Un diff de esta rama sirve para revisión local. No incluir binarios de la rama poison,
appsettings, secretos, manifiesto activo, evidencia privada ni rollback histórico en
un ZIP público. La futura publicación se generará desde fuente productiva acreditada,
portando únicamente Diagnostics, el hook de conexión equivalente y registro del
servicio. El Lab, scripts de pruebas y sus fixtures se excluyen del paquete Web.
Inventario y SHA-256 de todos los archivos de ambos paquetes (original/diagnóstico),
comparación de dependencias y ensayo de rollback son requisitos antes de autorizar.

Retirada local ensayada: desarmar manifest.json => DISABLED; conservar .consumed evita
repetición al rearmar el mismo runId. Reversión del parche verificada en checkout
local aislado mediante git apply --check/apply/reverse y árbol limpio (ISOLATED_PATCH_APPLY_REVERSE_CLEAN_OK). Esto prueba
retirada de código, no la restauración de un paquete productivo todavía desconocido.

Retirada remota futura: con autorización, desarmar archivo exacto, esperar <=25 s,
restaurar paquete original íntegro por mecanismo revisado, verificar hashes y salud,
comprobar que no aparece nueva evidencia; retirar sólo los archivos del runId enumerado
según retención aprobada. No borrar SQL ni modificar permisos. Ante fallo no reintentar
activación automáticamente. Marcador consumido no se elimina para repetir una prueba.

## Pendientes, riesgos y autorización productiva

- Acreditar fuente/binarios reales; portado y build sobre esa base. No mezclar este
  prototipo con despliegue funcional poison ni integrarlo a su rama automáticamente.
- Verificar TLS efectivo, ACL reales y ausencia de mapeos privados en hosting efectivo.
  Si no pasan, el diagnóstico se detiene; corregirlos requerirá autorización separada.
- Correlacionar PID/hash/instancia con Web no-SCM; evaluar todos los permisos runtime
  de la matriz 5I sin exigir ALTER/CONTROL para DML. No ejecutar SQL 57/58.
- Observar comportamiento real de parada/recycle y entrega de evidencia en App Service;
  el ensayo IIS Express no acredita identidad, permisos ni publicación Azure.
- Límites de confianza: administradores del sitio pueden leer archivos/alterar binarios.
  No protección contra operador privilegiado hostil. Una carpeta privada inaccesible
  bloquea el resultado; no se abren permisos automáticamente.
- La nueva autorización deberá identificar hashes de paquetes revisados, ventana de
  despliegue/reciclado, escritura privada del manifiesto y un SELECT dentro del worker
  por instancia, lectura privada y retirada/rollback. La preparación local no autoriza
  ninguno de esos actos productivos. Si se requieren ajustes TLS/ACL, aprobación aparte.

Sin descargas privadas, uploads, deploy, SQL productivo, cambios Azure, migraciones,
reinicios productivos o Listener. READY_TO_DEPLOY=NO.
