# RADAR — Hito 5H.2, preflight PITR detenido

Fecha 2026-11-06, DESKTOP-E32803V. Rama fix/radar-poison-message-recovery.
Base documental 16b2172764d67632b6fe75351e13cad60037210b.
RESTORE_AUTHORIZED=YES_CONDITIONAL. PREFLIGHT=BLOCKED. READY_TO_DEPLOY=NO.

El propietario autorizó una única copia PITR y consultas de lectura sobre ella,
condicionadas a comprobar aislamiento antes de crear. No autorizó eliminación,
cambios de firewall, permisos, producción ni reintentos que creen recursos adicionales.
No se envió Restore: resultado NO INICIADO, no una restauración fallida.
No existe identificador de operación ni recurso temporal creado por este ensayo.

## Evidencia revalidada

| Comprobación | Resultado |
| --- | --- |
| Equipo, rama y Git | Equipo esperado; árbol limpio, Start aprobado, HEAD igual a origin antes de editar |
| Contexto Azure | Suscripción seleccionada Enabled; servidor pertenece a ella y el ID de origen corresponde a mapsMarkers; identificadores privados omitidos |
| Origen | mapsMarkers, dpa6ecad46, Default-SQL-NorthCentralUS; Online, northcentralus |
| Nivel | Basic, 5 DTU, 2 GiB; catálogo regional devuelve Basic Available y almacenamiento LRS Available |
| Retención | 7 días; earliestRestoreDate observado 2026-10-30T16:51:31.722340Z |
| Instante de observación de origen/destino | 2026-11-06T16:51:33.9709822Z |
| Punto propuesto | 2026-11-06T16:00:00Z, dentro de la ventana observada; no se utilizó en una operación |
| Destino | mapsMarkers-pitr-radar-20261106-1600-7c9e2a, ausente al consultar |
| Precio público | Retail API: SQL Database Single Basic / B / B DTU, northcentralus, USD 0.161 por día |
| Conexiones y ajustes de rsmap | Una entrada de conexión y seis ajustes consultados privadamente; ninguna contiene literalmente el nombre temporal |
| Archivos locales | Búsqueda del nombre exacto fuera de documentación, bin y obj sin coincidencias |
| Red | PublicNetworkAccess Enabled; 55 reglas de servidor, incluida AllowAzureServices |
| Acceso SQL y automatizaciones | INCONCLUSO; no se acreditaron todos los principales autorizados, una ruta de validación sin cambiar firewall ni el inventario efectivo de Listener/tareas externas |

Se inspeccionaron valores de configuración sólo en memoria para emitir booleanos;
no se publicaron cadenas, tokens, IPs, suscripción, principales ni registros productivos.
La ausencia literal del nombre en esos ajustes no demuestra la configuración efectiva
del worker ni excluye descubrimiento automático de bases por otros servicios.
El resultado previo 5G.1 de identidad runtime sigue INCONCLUSO; no se repitió SQL
productivo ni se usó una conexión alternativa como prueba de identidad del Web.

## Motivo de detención y próximo paso

No se puede acreditar que la copia tenga únicamente los accesos autorizados ni que
ningún servicio o tarea la use. Restaurar en el mismo servidor comparte sus controles
de red y recupera usuarios/permisos de base. La regla de servicios Azure por sí sola
no concede autenticación SQL y no demuestra una exposición efectiva; el bloqueo es
la falta de evidencia suficiente sobre el conjunto de accesos y automatizaciones.

Antes de reintentar el preflight, acreditar privadamente los principales que podrán
acceder, su correspondencia con los operadores autorizados y una ruta de consulta
permitida sin cambios de firewall. Completar el inventario de Web, Listener y tareas,
incluyendo descubrimiento automático. Resolver la evidencia runtime pendiente cuando
sea necesaria para esa comprobación. Si el diseño exige modificar permisos o controles,
preparar ese cambio y obtener autorización separada antes de ejecutarlo. No se propone
ampliar accesos ni transferir herramientas como alternativa automática.

La creación ya tiene autorización condicional; no falta una segunda aprobación del
mismo Restore. Falta satisfacer su condición técnica. Revalidar fecha, destino y tarifa
al retomarlo; no sustituir el punto UTC ni intentar otra base automáticamente.

## Cierre y verificaciones no ejecutadas

No hay duración de restauración, estado Online del destino, DB_NAME(), estructura,
índices, constraints, conteos ni controles de cifrado/acceso de la copia que informar:
todas esas validaciones quedan NO EJECUTADAS porque la copia no se creó.
Costo incremental estimado de este ensayo: USD 0 por restauración/base temporal.
Estimación si se completa posteriormente: USD 0.161/24 h antes de impuestos,
aproximadamente USD 0.006708/h; sujeto a contrato y redondeo. La facturación de la
base restaurada comienza al completarse según Microsoft. No hay copia lista para borrar.

El procedimiento de creación, consultas con guardia DB_NAME y borrado exacto preparado
en RADAR_PITR_AUTHORIZATION.md sigue siendo sólo documentación. No ejecutar DELETE;
una eliminación futura requiere autorización independiente y verificar el ID real.
No hubo cambios Azure, SQL, despliegues, migraciones ni arranque del Listener.

Validación documental: revisión de diff, git diff --check, escaneo de secretos y
verificación de sincronización. No se repiten regresiones de aplicación por este
cambio exclusivamente documental. El SHA de publicación se identifica con git log
sobre este archivo y se entrega en el informe, evitando referencias autorreferentes.

Fuentes reconsultadas el 2026-11-06:
- https://prices.azure.com/api/retail/prices (filtro SQL Database, northcentralus, Consumption).
- https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups?view=azuresql
- https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/
