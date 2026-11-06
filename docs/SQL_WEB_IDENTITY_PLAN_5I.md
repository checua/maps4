# RADAR — Hito 5I: procedimiento mínimo de identidad SQL del Web

2026-11-06, DESKTOP-E32803V. Rama fix/radar-poison-message-recovery.
Base dc2c7b181cb1b1bd1b33c9b90e7adb756670e03c.
Identidad efectiva Web: INCONCLUSA. READY_TO_DEPLOY=NO.
Este hito sólo revisa código/evidencia y diseña el procedimiento; no implementa,
transfiere ni despliega instrumentación y no realiza consultas nuevas a Azure/SQL.

## Recomendación única

Preparar una instrumentación interna de una sola ejecución dentro del worker Web,
sin ruta HTTP, que use la conexión capturada por el repositorio real y ejecute SELECT
de identidad y permisos. Aplicarla exclusivamente como parche diagnóstico a una base
verificada de la versión actualmente desplegada, no desplegar la rama poison para
obtener evidencia. Publicar código revisado no equivale a autorizar su despliegue.

No hay hoy un camino acreditado sin código nuevo: el único SqlIdentityDiagnostic está
en el Lab, no localizado en el entorno remoto revisado. Su resolver de cinco aliases
no reproduce IConfiguration del Web. Ni volver a ejecutar esa consola ni transferir
sólo el Lab resolvería la atribución al proceso real. El controlador debug existente
es local/development, consulta datos inmobiliarios y no acredita identidad: no usarlo,
exponerlo ni habilitar Development. No se propone debugger, dumps, profiling, consultas
de mensajes ni habilitar telemetría productiva como atajo.

Esto no demuestra imposibilidad universal de una solución sin despliegue: si aparece
una capacidad preexistente verificable dentro del worker, reevaluarla. No se ha
identificado tal capacidad y no se repitieron las inspecciones fallidas de 5G.1.

## Evidencia conservada y lo que demuestra

- 5G.1: hosting inprocess; sólo w3wp SCM visible, is_scm_site=true. No hash verificable
  del binario cargado, ni comparación del proveedor ganador. La fecha de deployment
  de septiembre no es un SHA ni prueba suficiente del artefacto activo.
- 5H.3: cadenaSQL configurada en ARM contiene credencial SQL administrativa y apunta
  al origen esperado. La ruta oficina autenticó con TLS validado. La copia PITR mostró
  SQL/db_owner para el validador. Ninguno acredita el principal de una conexión del Web.
- 5H.4: ensayo PITR terminado, copia eliminada y producción Online. Esa evidencia de
  recuperación no satisface la condición de identidad ni autoriza migración/despliegue.
- Revisión local: WebApplication.CreateBuilder(args), repositorios scoped guardan
  GetConnectionString("cadenaSQL") en _cadenaSQL y construyen Microsoft.Data.SqlClient
  SqlConnection con ese valor. No se sustituirá por configuración ARM/SCM/SSMS.

## Observar el worker real (procedimiento, no ejecutado)

Con acceso de lectura existente, enumerar instancias del recurso/slot real y usar
ARM GET por instancia, no confiar en que una enumeración SCM represente al Web:

```text
GET {siteResourceId}/instances?api-version=2025-05-01
GET {siteResourceId}/instances/{instanceId}/processes?api-version=2025-05-01
GET {siteResourceId}/instances/{instanceId}/processes/{pid}?api-version=2025-05-01
```

Confirmar soporte de esa versión API al implementar. Proyectar sólo instanceId,
PID, parent, file_name, start_time, is_scm_site e is_webjob. No volcar todo el JSON:
puede contener entorno, nombres de usuario, argumentos u otras propiedades sensibles.
Para inprocess, buscar w3wp con is_scm_site=false y no WebJob; correlacionar instancia,
PID/hora de inicio y artefacto cargado con la evidencia emitida por la instrumentación.
En out-of-process habría que correlacionar también el hijo dotnet; no asumir ese modelo.
Un PID o WindowsIdentity por sí solos no prueban identidad SQL.

Si sólo aparece SCM, detener esa observación sin reciclar ni cambiar Always On.
No repetir el mismo listado indefinidamente. Un calentamiento mediante petición sólo
será permitido después de revisar una ruta existente sin efectos de negocio y aprobar
expresamente ese arranque. La instrumentación registrará su propio PID e instancia;
si no puede corroborarse su pertenencia al Web, el resultado seguirá INCONCLUSO.

## Contrato de instrumentación a implementar y probar localmente

1. Recuperar privadamente manifiesto/hash del despliegue activo y su fuente exacta.
   Identificar el artefacto original reproducible y un rollback íntegro verificable.
   El ZIP histórico de 345 archivos no se presume igual al despliegue actual por su
   nombre/hash antiguo. Si no se puede demostrar la base, no preparar despliegue.
2. Crear checkout/rama diagnóstica desde esa base, conservando poison intacta. Parche
   limitado a servicio interno, método de diagnóstico en la ruta de conexión del
   repositorio y salida privada. Ninguna migración, dependencia o función de negocio.
3. Servicio de una ejecución después de ApplicationStarted, no bloqueante, scope DI
   propio. Resolver el repositorio del Web y usar su valor capturado _cadenaSQL y la
   misma construcción/autenticación SqlConnection; no invocar Reclamar, Delivery,
   matching, pairing ni métodos que escriban. Si el código desplegado difiere,
   adaptar el punto de instrumentación y revisar su equivalencia antes de compilar.
4. Usar el IConfiguration del host para identificar el proveedor ganador de
   ConnectionStrings:cadenaSQL (Providers en orden inverso/TryGet). Sólo registrar
   tipo/posición, no ToString, DebugView, valores ni hashes de secretos. Comparar en
   memoria el valor actual con el capturado por el repositorio; si difiere, cancelar.
   No forzar cadena, usuario, base, token, autenticación, Application Name o EXECUTE AS.
   Mantener opciones de seguridad originales; si no cumplen TLS requerido, detener
   y tramitar esa corrección separadamente. SQL no se ejecuta antes de validar destino.
5. Validar servidor/base esperados contra configuración efectiva sin cambiarla; tras
   abrir, comprobar DB_NAME()=mapsMarkers. Ejecutar consulta fija, nunca SQL aportado
   por usuario/archivo. Reutilizar SELECT de SqlIdentityDiagnostic.Query (18 campos),
   añadir @@SPID y permisos runtime de la matriz siguiente mediante HAS_PERMS_BY_NAME.
   No SELECT de filas de negocio, DML, DDL, impersonación ni pruebas de escritura.
6. Registrar UTC, runId, instanceId, PID, inicio del proceso, versión/hash del assembly
   Web y del parche, proveedor ganador y resultado de conexión. En evidencia privada:
   ServerName, DatabaseName, ORIGINAL_LOGIN(), SUSER_SNAME(), USER_NAME(), auth_scheme,
   @@SPID y permisos 1/0/NULL. Ninguna credencial o contenido de conexión. Acredita esa
   conexión del worker instrumentado en ese instante, no retroactivamente el proceso
   anterior ni todos los proveedores de datos del sitio. La identidad Windows no se
   necesita para esta prueba de autenticación SQL.
7. Apertura y comando con límites de 10 s, cancelación global 25 s, una tentativa por
   instancia autorizada. Sin bucles, reintentos ni salida de excepciones libres.
   Fallo produce código permitido y estado INCONCLUSO sin detener el Web.

## Activación, privacidad y límites

Sin endpoint, querystring, cookie especial ni configuración enviada por usuarios.
Desactivado por defecto y también en ausencia de manifiesto privado válido.
Propuesta: manifiesto sin secretos en %HOME%/data/radar-sql-identity/{runId}, fuera de
wwwroot, con runId, hash del parche autorizado, sitio/slot, lista exacta de instancias
y caducidad UTC máxima 15 minutos. Sin opciones de SQL ni destino configurables.
Acceso de lectura/escritura sólo a identidad del sitio y operadores SCM ya autorizados;
verificar ACL y que no exista mapeo estático/descarga de esa carpeta. No ampliar RBAC.
Si no es posible asegurar estos controles con accesos existentes, bloquear activación.

Marcador persistente CreateNew por runId/instanceId ANTES de abrir SQL; consumo incluso
si falla. Reinicios no vuelven a ejecutar, una instancia nueva no listada no ejecuta.
Cambios de configuración durante la ejecución invalidan la evidencia. Archivo resultado
con nombre fijo derivado de runId/instanceId, escritura atómica, máximo 32 KiB.
Sin stdout, ILogger, Application Insights, consola o respuesta HTTP para identidades.
Lectura sólo por Kudu autenticado con acceso existente. Los administradores del sitio
siguen siendo parte del límite de confianza; no se promete ocultación frente a ellos.
La autorización debe abarcar cada instancia vigente, no inferir toda la flota de una sola.

## Matriz runtime revisada (dbo salvo indicación)

| Ruta | Objetos | Permisos a comprobar, sin ejercerlos |
| --- | --- | --- |
| Processing/leases/recovery | RSMAPS_RadarMessageProcessing | SELECT, INSERT, UPDATE |
| Auditoría | RSMAPS_RadarMessageEvent | INSERT |
| Delivery/recovery | RSMAPS_RadarMessageDelivery; Processing | SELECT, INSERT, UPDATE en Delivery; SELECT en Processing |
| Pairing/heartbeat/config | RSMAPS_RadarAgentPairing, RSMAPS_RadarAgentDevice, RSMAPS_RadarAgentConfig | SELECT, INSERT, UPDATE |
| Chat discovery | RSMAPS_RadarAgentChatDiscovery | SELECT, INSERT, UPDATE (MERGE); Device SELECT/UPDATE en comandos |
| Contexto de autorización | RSMAPS_Usuario, RSMAPS_CuentaUsuario, RSMAPS_Cuenta | SELECT |
| Matching, ListarAutorizadosAsync | RSMAPS_sp_ListaInmueblesAutorizados | EXECUTE; no ejecutarlo en diagnóstico |
| Complemento estructurado de matching | RSMAPS_Inmueble, RSMAPS_InmuebleAmenidad | SELECT |
| SQL 57, identidad de mantenimiento separada | Processing y metadatos de esquema/índices requeridos | ALTER y visibilidad de metadatos; verificar preflight aparte |
| SQL 58 | Procedimiento de reactivación y CONTROL de base | Fuera del primer despliegue; no permiso runtime ni ejecución diagnóstica |

Fuentes locales: RadarMessageProcessingRepository, RadarMessageDeliveryRepository,
RadarPendingProcessingRepository, RadarPendingDeliveryRepository, RadarAgentPairingRepository,
RadarAgentChatDiscoveryRepository, RadarAgentChatDiscoveryCommandRepository,
RadarMatchingService e InventarioRepository.ListarAutorizadosAsync/CompletarDatosEstructuradosAsync.
La matriz amplía la anterior de poison; no certifica todos los demás endpoints del Web,
procedimientos de inventario no usados por matching ni permisos implícitos de módulos.
Reconciliarla con la base desplegada y los objetos existentes antes de fijar la consulta.

1=CONFIRMADO; 0=NO CONFIRMADO/permiso faltante; NULL=INCONCLUSO. Registrar objeto no
visible/ausente sin confundirlo con DENY. HAS_PERMS_BY_NAME evalúa esa sesión; no
prueba triggers, RLS, ownership chaining, estructura compatible ni éxito de transacciones.
No es necesario db_owner, CONTROL ni ALTER para el DML revisado. Si hay privilegios
administrativos, registrar exceso, no revocarlo. SQL 57 requiere identidad y aprobación
separadas; ausencia de ALTER en runtime no bloquea permisos runtime. SQL 58 sigue excluido.

## Pruebas previas obligatorias (diseñadas, todavía no ejecutadas)

- Configuración: JSON, variable SQLCONNSTR y argumento con valores distintos; demostrar
  que repositorio y diagnóstico usan el mismo ganador. Cambio entre captura/ejecución
  debe cancelar; resolver del Lab no se acepta como sustituto.
- LocalDB aislada: cuenta permitida/restringida, denegaciones y objeto invisible; salida
  1/0/NULL sin DML por el diagnóstico. Verificar identidad de la conexión abierta.
- Hosting local IIS inprocess: PID w3wp del sitio correcto, distinto de SCM/simulador;
  si IIS local no está disponible, preparar entorno de ensayo autorizado antes de Azure.
- Inactivo/expirado/hash o instancia distintos: cero conexiones. Carrera entre workers,
  reinicio y fallo tras marcador: una tentativa por instancia, ninguna repetición.
- Timeout, cancelación y destino incorrecto: no afectar peticiones ni escribir en SQL.
- Secretos señuelo en conexión y errores: ausentes de archivos públicos/logs/telemetría;
  evidencia fuera de wwwroot inaccesible por HTTP, ACL y límites de tamaño probados.
- Restore/build Release y regresiones locales poison, SQL, identidad, Delivery y timeout
  para cualquier cambio compartido; comparar contrato SQL y diff del parche con base.
- Ensayar retirada y rollback de paquete completo, sin tocar datos o migraciones.

## Secuencia ejecutable y autorización adicional exacta

Primero preparar localmente implementación, pruebas, manifiesto del parche, manifiesto
del paquete original y hashes. La preparación local/commit ya está autorizada; no se
realizó aquí porque este hito pide el diseño. Los hashes de despliegue no están disponibles
hoy: este documento no es un paquete listo para autorizar a ciegas.

Una vez que esos artefactos sean concretos y revisables, solicitar una autorización
operativa única y limitada que incluya TODOS estos actos:

A. Transferir y desplegar en rsmap/slot productivo sólo el parche diagnóstico sobre
   la versión desplegada verificada, con posible reciclado breve; ventana acordada.
   No desplegar funcionalidad poison ni SQL 57/58. Sin cambios de conexiones/firewall.
B. Escribir manifiesto privado temporal, activar una sola ejecución por cada instancia
   identificada, y ejecutar dentro del worker el SELECT fijo de identidad y permisos
   de esta matriz. Esta autorización reemplaza, sólo para este método, la condición
   anterior de equivalencia Kudu: el código corre en Web, Kudu sólo controla/lee archivos.
C. Leer la evidencia por SCM privado; aprobar conservación privada máxima 24 horas y
   publicación sólo de estado sanitizado. No habilitar logging de resultados sensibles.
D. Retirar manifiesto/instrumentación y restaurar el paquete original verificado al
   concluir o fallar; autorizar ese segundo despliegue/reciclado y eliminación únicamente
   de archivos diagnósticos enumerados bajo el directorio exacto del runId. Conservar
   la evidencia privada necesaria por el plazo aprobado; nunca borrar datos SQL.

Retirada normal y rollback: desarmar manifiesto, esperar tarea acotada o cancelarla,
restaurar paquete original por el mecanismo de publicación previamente probado,
verificar hash/configuración, salud de ruta sin efectos aprobada y ausencia de nuevas
salidas/consultas diagnósticas. Fallo de salud, exposición, destino errado, incongruencia
de versión o timeout: no reintentar; rollback autorizado, resultado INCONCLUSO y reporte.
La caducidad limita consultas, pero no reemplaza retirada del binario.

Sin esa autorización no transferir, activar, desplegar, reiniciar ni borrar archivos
remotos. No se pide repetir permisos genéricos ya concedidos: los actos nuevos son
parche Web, activación privada, SELECT dentro del worker y retirada/rollback.
Ninguna acción de este procedimiento autoriza conceder permisos SQL o desplegar RADAR.

## Trabajo realizado en 5I y referencias

Revisión Git (Start), evidencia 5G/5H, código y SQL 57/58, documentación oficial;
sólo cambios documentales. Validación: diff/check, escaneo dedicado y sincronización.
No nuevas pruebas runtime ni lecturas de Azure; no se afirma que el diseño esté probado.

- ARM procesos por instancia: https://learn.microsoft.com/en-us/rest/api/appservice/web-apps/list-instance-processes?view=rest-appservice-2025-05-01
- SCM vs Web: https://github.com/projectkudu/kudu/wiki/Process-Threads-list-and-minidump-gcdump-diagsession
- Hosting IIS: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/out-of-process-hosting?view=aspnetcore-10.0
- Proveedores: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0
- Permisos efectivos: https://learn.microsoft.com/en-us/sql/t-sql/functions/has-perms-by-name-transact-sql
