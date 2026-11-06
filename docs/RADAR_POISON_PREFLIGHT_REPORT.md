# Hito 3 — reporte de preflight

## Hito 4F — herramienta de identidad SQL

Se añadió SqlIdentityDiagnostic.cs al Lab con modos sql-identity-diagnostic y
sql-identity-regression. El modo diagnóstico requiere RADAR_SQL_IDENTITY_AUTHORIZED=1
y exactamente un alias de entorno reconocido para cadenaSQL. Sólo acepta catálogo
mapsMarkers, no recibe cadenas por argumentos ni escribe archivos. Ejecuta un SELECT
fijo con identidad y HAS_PERMS_BY_NAME. Emite JSON con 17 campos permitidos o el
error constante SQL_IDENTITY_FAILED, nunca excepción ni propiedades de conexión.
Una respuesta NULL de permiso significa indeterminado/objeto no visible, no permiso cero.

Web usa WebApplication.CreateBuilder(args), sin proveedores personalizados añadidos
en Program.cs. Prioridad habitual, de mayor a menor: argumentos, variables de entorno,
user-secrets en Development, appsettings del entorno, appsettings.json y configuración
host de respaldo. Azure SQLServer expone SQLCONNSTR_cadenaSQL, normalizado por .NET
a ConnectionStrings:cadenaSQL. SQLAZURECONNSTR_, CUSTOMCONNSTR_ y la clave directa
ConnectionStrings__cadenaSQL también pueden producir esa clave. Evitar duplicados:
su coexistencia no acredita qué valor gana en la enumeración del proveedor.
Una cadena con otro nombre no reemplaza cadenaSQL salvo código que la mapee.
Referencias oficiales verificadas el 2026-11-05:
https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0
https://learn.microsoft.com/azure/app-service/configure-common

La herramienta deliberadamente consume sólo entorno autorizado. No pretende replicar
JSON, argumentos, user-secrets ni identidad de proceso distinta. No enumera todos los
valores de configuración. Se deben descartar overrides antes de atribuir su resultado
a la aplicación. Los strings gestionados existen temporalmente en memoria; no se
promete borrado criptográfico de memoria. No capturar dumps, trazas SqlClient ni entorno.

Procedimiento Azure preparado, sin ejecutar:

1. Verificar autorización de ejecución diagnóstica y disponibilidad de herramienta
   revisada en el contexto del slot/worker concreto. El artefacto actual existe sólo
   localmente. Subirlo o cambiar configuración de App Service requiere otro alcance.
2. Identificar por nombres los proveedores y overrides del worker (web.config,
   argumentos y entorno). No volcar conexiones ni usar GetDebugView. Confirmar único
   SQLCONNSTR_cadenaSQL efectivo, catálogo mapsMarkers y contexto de autenticación.
3. Una consola Kudu autorizada puede disponer de configuración del sitio, pero no
   demuestra igualdad con variables inyectadas por web.config ni con la identidad
   del worker, especialmente con autenticación integrada/administrada. Si esa
   equivalencia no puede acreditarse, dejar APPLICATION_IDENTITY_VERIFIED=NO.
4. Sólo en proceso/consola equivalentes y autorizados establecer variables de proceso
   RADAR_LAB_MODE=sql-identity-diagnostic y RADAR_SQL_IDENTITY_AUTHORIZED=1. Ejecutar
   dotnet RSMaps.Radar.Lab.dll desde el bundle diagnóstico revisado. No proporcionar
   cadenas por CLI ni copiar una conexión desde el portal. El modo retorna antes de
   cualquier otro flujo Lab/Listener. Limpiar ambas variables al terminar.
5. Registrar salida limitada, UTC, slot y evidencia de equivalencia del contexto.
   Comparar ORIGINAL_LOGIN/SUSER_SNAME/USER_NAME con la conexión administrativa.
   misiones/dbo/db_owner=1 sólo acredita la sesión SSMS reportada por el usuario.
6. Si DatabaseControl/DbOwner/AlterSchema son 1 en la aplicación, mínimo privilegio
   no está demostrado. Revisar DML requerido y separar principal de migración.
   SQL58 continúa excluido. No cambiar permisos en este bloque.

No existe acceso autorizado identificado al contexto real de App Service en este
bloque. No se ejecutó la herramienta contra producción. La prueba local usa LocalDB
efímera propia y EXECUTE AS USER sobre un usuario sintético sin login, con REVERT.
Demuestra diferencia de usuario/permisos respecto a dbo, no dos logins de red distintos.
También verifica lista cerrada de salida y error sanitizado con entrada inválida
sintética. No reutiliza credenciales productivas.

APPLICATION_IDENTITY_VERIFIED=NO
READY_TO_DEPLOY=NO

## Actualización Hito 4E — 2026-11-05

Resultado nuevo: se añadió una sección SELECT de identidad y permisos efectivos
al preflight. Devuelve contexto de sesión, autenticación del usuario de base y
fn_my_permissions de base, dbo y tres tablas RADAR. Debe ejecutarse por la conexión
autorizada de la aplicación. Una sesión de administrador o rol mostrado en el portal
no demuestra la identidad efectiva de SQL utilizada por Web.

El código inspeccionado crea SqlConnection desde GetConnectionString("cadenaSQL").
No asigna AccessToken en ese repositorio. Esto permite que el mecanismo dependa de
la cadena de conexión efectiva, sin demostrar si usa contraseña, identidad administrada
u otro modo soportado. No se leyeron valores ni se abrió una conexión nueva.

En el contexto disponible no existen az ni comandos Az, variables SQLCMD/preflight
identificadas, ni directorio C:\Users\jenny\.azure o azureProfile.json. sqlcmd existe
pero no acredita sesión autorizada. No hay herramienta Azure conectada expuesta.
Se mantienen pendientes identidad efectiva y mínimo privilegio. No se atribuye
ausencia de acceso a todas las sesiones gráficas del equipo, que no fueron auditadas.

Decisión de alcance: EXCLUIR SQL58 DEL PRIMER DESPLIEGUE. Su instalación es opcional
y el procesamiento poison funciona con SQL57. Se requerirá un bloque separado con
identidad administrativa, permisos efectivos y autorización auditados antes de
instalar o invocar la reactivación. No se otorga CONTROL/db_owner a la aplicación.
La exclusión reduce superficie pero no resuelve permisos excesivos ya existentes.

Comprobaciones pendientes mediante portal Azure con sesión autorizada:

1. App Service rsmap, configuración/variables y slot: confirmar referencia efectiva
   cadenaSQL y mecanismo de autenticación mediante metadatos autorizados, evitando
   revelar valores. Identidad administrada habilitada por sí sola no prueba su uso SQL.
2. SQL database mapsMarkers, Overview/JSON View: verificar recurso, servidor, estado,
   tier y earliestRestoreDate si se expone. Registrar UTC y evidencia sin secretos.
3. SQL server, Backups/Retention policies: consultar política short-term efectiva,
   retención en días, redundancia y política LTR si está configurada, sin guardar cambios.
4. mapsMarkers, Restore: inspeccionar ventana de fechas restaurables y destino posible,
   sin pulsar Create/Review+create ni iniciar restore. Registrar earliest restore point.
5. Activity log y bases aisladas existentes: localizar evidencia de restauración exitosa,
   recurso origen/destino, fecha UTC y resultado. Un backup listado o ventana visible
   demuestra disponibilidad anunciada, no recuperación ensayada.
6. IAM del destino: identificar permisos para restaurar y un entorno aislado previamente
   autorizado. No existe evidencia de tal destino en este bloque. Una nueva base implica
   escritura y posibles costos y queda fuera de la comprobación de lectura actual.

PITR requiere una cadena de backups y permite recuperar dentro de la ventana disponible.
No se asigna retención por defecto a mapsMarkers ni se afirma un restore exitoso.
Referencia oficial consultada el 2026-11-05:
https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups?view=azuresql

No se repitieron builds ni regresiones ya aprobados. La nueva sección SQL recibió
revisión estática, sin ejecución productiva. Los dos gates críticos permanecen abiertos.
SQL58_FIRST_DEPLOY=EXCLUDED
APPLICATION_IDENTITY_VERIFIED=NO
LEAST_PRIVILEGE_VERIFIED=NO
PITR_RETENTION_VERIFIED=NO
PRODUCTION_RESTORE_VERIFIED=NO
READY_TO_DEPLOY=NO

## Dictamen vigente — Hito 4D

Revisión local de requisitos operativos, 2026-11-04. READY_TO_DEPLOY=NO.

El usuario aporta el resultado productivo de 414 procesamientos COMPLETADO,
un poison con IdRadarMessageProcessing=356 e IntentosProcesamiento=124 y
32 deliveries enviados. Se registra como evidencia comunicada por el usuario.
No se dispone aquí de salida original con UTC, identidad de sesión, Estado exacto
de 356, leases, ACK, JSON o colas actuales. Los 32 enviados no demuestran ausencia
de otras entregas pendientes. No se reconsultó ni modificó producción.

Verificación de código realizada: SQL57 añade MaxIntentosProcesamiento=5 a filas
existentes y comprueba presupuesto positivo. No limita IntentosProcesamiento a 5,
no resetea contadores, no modifica Estado/JSON/ACK ni escribe Delivery. La condición
de fallo permanente del CHECK sólo aplica a FALLIDO_PERMANENTE. Por tanto los
414 COMPLETADO sobreviven aun con contador superior a 5. ReclamarAsync devuelve
Completed antes de evaluar presupuesto. Cache inválido exige investigación.

SQL57 por sí mismo no reactiva ni terminaliza 356. Si su estado real es pendiente
elegible, al vencer lease/retry el nuevo claim detecta 124>=5, terminaliza con evento
en una transacción y conserva 124 sin ejecutar Intelligence. Si ya es permanente,
retorna 422 sin reapertura. Si es COMPLETADO, corresponde replay/investigación.
No basta el número de intentos para deducir el estado. Esta terminalización futura
es un efecto productivo que debe incluir expresamente la autorización de despliegue.

La identidad SQL efectiva continúa sin demostrar. El repositorio toma
ConnectionStrings:cadenaSQL, pero el nombre de configuración no identifica login,
usuario ni permisos. Azure CLI no está disponible en PATH ni en sus dos rutas
habituales. No hay módulos Az.Accounts/Az.Sql/Az.Websites instalados ni herramientas
Azure conectadas expuestas en esta sesión. No se solicitaron credenciales ni se
extrajeron secretos. Properties/PublishProfiles no existe en este worktree. La
documentación histórica confirma despliegues anteriores, sin acreditar un mecanismo
actual autenticado ni una identidad SQL vigente.

Retención, earliest restore point, último backup restaurable y permisos de restore
productivos siguen sin evidencia accesible. No existe en el alcance un destino
Azure aislado autorizado con costo y configuración definidos. LocalDB de regresión
prueba esquema y lógica, pero no acredita recuperar un backup real de mapsMarkers.
No se creó recurso ni se inició restauración productiva.

Se recalculó SHA256 del ZIP integral y coincide exactamente con Hito 4B:
240D0443DAC9CB94B5FFEDE42EA0780661A9BAFBC0EB01C6D48D30C34D7AF17D.
Se conservan sus pruebas aprobadas de SQL57 y arranque aislado. No se repitieron.
Compatibilidad con la publicación ACTUAL de Azure sigue pendiente de un manifiesto
remoto autorizado y comparación de runtime, dependencias, assets y configuración.

Antes de autorizar despliegue se necesita evidencia de identidad de aplicación y
permisos efectivos, historial y ventana PITR de la base real, destino de restore
aislado aprobado y ensayo recuperable, configuración externa respaldada/restaurable,
inventario fresco de 356 y colas, y manifiesto productivo comparado con candidato.
SQL58 debe permanecer fuera del despliegue si la aplicación tiene CONTROL/db_owner
o no puede demostrarse su separación del principal administrativo.

PRODUCTION_CHANGED=NO
SQL57_EXECUTED_IN_PRODUCTION=NO
SQL58_EXECUTED_IN_PRODUCTION=NO
APPLICATION_SQL_IDENTITY_VERIFIED=NO
PRODUCTION_RECOVERY_VERIFIED=NO
LOCAL_ROLLBACK_HASH_VERIFIED=YES
READY_TO_DEPLOY=NO

## Resultado más reciente — Hito 4B

Publicación Web completa y restauración aislada ejecutadas con éxito. Evidencia en
RSMaps.Radar.Lab/bin/rollback-59761964abdd40b1962174abf80848fc/result.json,
manifest.csv, logs de publicación/build/pruebas y arranque. 345 archivos,
31163472 bytes. ZIP SHA256
240D0443DAC9CB94B5FFEDE42EA0780661A9BAFBC0EB01C6D48D30C34D7AF17D.

Poison controller/HTTP, SQL y Delivery PASS usando exactamente maps4.dll e
Intelligence.dll extraídas del ZIP. SQL57 compatible en base LocalDB aislada.
Terminales no reabiertos, leases obsoletos rechazados y auditoría transaccional
verificados por la regresión SQL. Login y JS del backend restaurado HTTP200 en
loopback. El proceso local terminó al cerrar el ensayo.

Configuración original excluida por target MSBuild antes de publicación. Se creó
configuración local no secreta. Entorno del proceso Web limpiado para impedir
heredar conexiones o claves. Se usó RollbackValidation para evitar user-secrets de
Development. La lectura HTTP de JS no ejecuta Google Maps ni abre un navegador.

Un primer escaneo falló por lectura de password desde formulario en RegisterScript.js.
Se confirmó falso positivo y se corrigió el patrón de literales. El segundo ensayo
pasó. Una creación inicial de directorio fue bloqueada por sandbox y se completó
por el mecanismo autorizado. No se modificaron fuentes funcionales del backend.

LOCAL_FULL_PUBLICATION_RESTORE=PASS
LOCAL_RESTORED_WEB_BOOT=PASS
SQL57_ISOLATED_COMPATIBILITY=PASS
PRODUCTION_CONFIGURATION_RESTORE=PENDING
PRODUCTION_BACKUP_RECOVERABILITY=PENDING
PRODUCTION_SQL_PERMISSIONS_AND_DATA=PENDING
READY_TO_DEPLOY=NO

Los resultados parciales de Hito 4 que siguen abajo se conservan como historial.

Fecha de revisión local 2026-11-04. Rama fix/radar-poison-message-recovery.

## Evidencia directa

Se leyeron los scripts 57/58 y el repositorio de recovery. El script de diagnóstico
preparado contiene sólo SELECT y comentarios, sin ejecución de procedimientos.
El cribado JSON protege funciones JSON frente a documentos inválidos y expone
banderas, sin texto de mensajes, errores libres ni resultados centrales.

La inspección de nombres de archivos confirmó la instalación en
C:\Users\jenny\AppData\Local\RSMaps\RadarAgent\AgentProduccion con app, Backups,
logs, reports, launcher y WhatsAppProfile. app contiene los ensamblados Listener e
Intelligence, deps y runtimeconfig. Es factible preparar reemplazo por allowlist.
La validación de permisos de reemplazo, locks y perfil efectivo requiere el gate
operativo previo. No se probó escritura en esa instalación.

La consulta de la tarea devolvió Ready y la consulta por nombre de proceso no
devolvió instancias RSMaps.Radar.Listener. Se trata de una observación puntual.
No se cambió el estado de la tarea ni se inició el Listener.

## Pendientes y riesgos

- Conexión SQL productiva autorizada disponible no establecida en este bloque.
  Diagnóstico SQL preparado, sin ejecución contra mapsMarkers. La existencia de
  sqlcmd local no acredita una sesión autorizada.
- Principal administrativo real pendiente de identificación. ORIGINAL_LOGIN y
  USER_NAME del preflight identificarán la sesión que se utilice, sin demostrar
  por sí mismos qué principal utiliza Web. Repetir auditoría bajo identidades
  autorizadas. CONTROL/db_owner de aplicación permite alcanzar la ruta admin.
- Esquema real, estados, presupuesto, colas y mensaje 356 pendientes. La consulta
  específica interpreta 356 como IdRadarMessageProcessing, sin asumir MessageId.
- Los SELECT son observaciones concurrentes. Ejecutar nuevamente con intake
  suspendido en mantenimiento autorizado para obtener decisiones consistentes.
- El diagnóstico de JSON es un cribado. La validación completa del lector C#
  requiere revisar identidad, duplicación de propiedades y listas internas.
- Paquetes, backups recuperables y rollback compatible pendientes de generar y
  ensayar bajo el siguiente alcance autorizado. No se ha publicado artefacto.
- Mantener el gate de rollback porque el backend antiguo puede reabrir terminales.
- Subsisten los riesgos documentados de dependencia no cooperativa, Pkcs y E2E
  preexistente. No se cambiaron dependencias en este bloque.

## Entregables preparados

SQL preflight_radar_poison_readonly.sql, checklist RADAR_POISON_DEPLOY_CHECKLIST.md
y plan RADAR_POISON_ROLLBACK_PLAN.md. Todos permanecen locales para revisión.

PRODUCTION_MODIFIED=NO
PRODUCTION_SQL_EXECUTED=NO
MESSAGE_356_MODIFIED=NO
COMMITS_CREATED=0
PUSH_PERFORMED=NO
DEPLOY_AUTHORIZED=NO
READY_TO_DEPLOY=NO

## Hito 4 — validación operativa local

Revalidación del 2026-11-04 sobre HEAD 5299f15 y cambios locales del candidato.
No se identificó una sesión SQL productiva de lectura previamente autorizada
y disponible. No se extrajeron conexiones de appsettings ni credenciales para
crear una nueva conexión. Esquema, permisos efectivos, colas y 356 siguen pendientes.

Procedimiento SQL preparado para una sesión SSMS ya autorizada y conectada a
mapsMarkers: abrir sql/RSMaps2/preflight_radar_poison_readonly.sql, ejecutar primero
el SELECT de contexto y el inventario de dependencias, confirmar base y permisos,
y ejecutar las demás secciones sólo si existen sus columnas. Guardar únicamente
la salida sanitizada en ubicación restringida. Repetir auditoría con la identidad
de aplicación autorizada. No usar EXECUTE AS ni conceder permisos para completar
este diagnóstico. Los resultados deben vincularse a la identidad y UTC observadas.

Instalación inspeccionada por lectura de metadatos. Listener runtimeconfig exige
Microsoft.NETCore.App 10.0.0 y están instalados 10.0.12/10.0.14. DLL productiva
de 220672 bytes con LastWriteTimeUtc 2026-10-01 10:09:29. Esta fecha/tamaño no
demuestra un SHA de código fuente desplegado. Launcher existente referencia
Listener y las variables RADAR_AGENT_CONFIG_PATH, RADAR_CENTRAL_FALLBACK_LOCAL,
RADAR_INTELLIGENCE_MODE y RSMAPS_BASE_URL. No imprime valores. No contiene una
referencia literal WhatsAppProfile, por lo que falta confirmar el perfil efectivo.
La consulta actual de Task Scheduler fue inaccesible. El Ready observado en Hito 3
es histórico. La consulta por nombre no devolvió proceso Listener.

Build Lab Release y sus proyectos referenciados en salida nueva aislada PASS,
0 errores, 18 advertencias preexistentes. El primer intento fue bloqueado por
escritura sandbox en obj. El reintento autorizado local completó correctamente.
Modos poison-message-regression, poison-message-sql-regression y
delivery-flow-regression PASS. SQL usa sólo LocalDB efímera propia y verificó
presupuesto, concurrencia, leases, auditoría, reactivación e idempotencia.
La prueba confirmó nuevamente LEGACY_BACKEND_ROLLBACK_UNSAFE_CONFIRMED.

Se creó un overlay binario de recuperación con maps4.dll/pdb e Intelligence.dll/pdb,
manifiesto CSV de nombres/tamaños/SHA256, ZIP y restauración aislada. Los cuatro
archivos restaurados coinciden exactamente con los binarios de las pruebas.
Ruta del ZIP:

    C:\Users\jenny\AppData\Local\Packages\sandbox.{78209a3b-98c4-4a9a-b28e-1bcb62d935b2}\AC\Temp\RadarPoisonRollback-8cbf565ac81f4f3d89f0058e2bd89686\compatible-backend-overlay.zip

SHA256 ZIP:

    826DCC9AEE2DA78AACB83794B3384197CFF68ECE03005E1C3A75CCA4EEEE3A3C

RESTORE_HASH_CHECK=PASS. El overlay requiere una publicación Web completa compatible
con sus dependencias y assets. Su extracción no acredita restauración integral de
Web ni configuración productiva. El destino temporal del sandbox debe trasladarse
a almacenamiento duradero autorizado antes de depender de él operacionalmente.
Faltan backup integral recuperable, identidad reproducible del candidato versionado,
compatibilidad de dependencias productivas y ensayo completo de arranque aislado.
No se ejecutó publish ni se inició ningún Listener real.

SQL58_INSTALL_SAFE=PENDING
FULL_ROLLBACK_VERIFIED=NO
READY_TO_DEPLOY=NO
