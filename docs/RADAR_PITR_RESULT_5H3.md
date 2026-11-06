> Cierre 5H.4: copia temporal eliminada con autorización expresa el 2026-11-06.
> GET posterior 404; producción Online y conexión de rsmap conservada.
> Ver RADAR_PITR_CLOSEOUT_5H4.md. Los avisos de conservación/borrado pendiente
> siguientes son históricos. READY_TO_DEPLOY=NO.

# RADAR — Hito 5H.3: aislamiento acotado y PITR completado

2026-11-06, DESKTOP-E32803V. Rama fix/radar-poison-message-recovery.
Base documental 6e918391237729f6cc379964a096e6cbc6d33751.
DICTAMEN=APTO para el ensayo acotado autorizado. RESTORE=SUCCEEDED.
RESTORE_AUTHORIZATION_CONSUMED=YES. READY_TO_DEPLOY=NO.

## Evidencia nueva y criterio de aceptación

Se aplicó el criterio solicitado de riesgo acotado, no aislamiento absoluto respecto
al administrador existente. La copia conserva el límite de confianza del servidor:
no se crearon identidades, concesiones ni rutas de red y no se cambiaron conexiones
productivas. La existencia de acceso administrativo no se trata como un bloqueo
indefinido. No se afirma que las credenciales del Web estén técnicamente impedidas
de acceder a la copia; la separación operativa se basa en destino explícito, ausencia
de descubrimiento en el código revisado y tareas conocidas verificadas.

| Evidencia | Resultado y límite |
| --- | --- |
| Configuración ARM de cadenaSQL | Valor no vacío analizado en memoria; servidor y base corresponden al origen, credencial SQL configurada de categoría administrativa; sin publicar principal ni contraseña |
| Identidad del worker | No comprobada por esta prueba; leer configuración no acredita el proceso Web efectivo. 5G.1 continúa INCONCLUSO |
| Autenticación disponible | Administrador SQL configurado; cero administradores Entra enumerados. Consulta de recurso Entra-only devuelve ResourceNotFound: no se interpreta como prueba absoluta de exclusividad de un mecanismo |
| Red | Acceso público, TLS mínimo 1.2 y 55 reglas existentes, incluida AllowAzureServices. No se modificaron |
| Ruta oficina | TCP 1433 alcanzable y apertura/cierre SQL autenticado al origen con Encrypt=true y TrustServerCertificate=false, sin ejecutar comandos SQL; ruta existente comprobada sin cambios de firewall |
| Validador | System.Data.SqlClient disponible localmente; después de Restore sólo se usó Initial Catalog del destino exacto, guardia DB_NAME y SELECT |
| Listener revisado | Transporte HTTP a API RSMaps; no acceso SQL ni descubrimiento de bases encontrado en el código revisado |
| Descubrimiento | git grep sobre archivos versionados encontró sys.databases limitado a database_id=DB_ID() en preflight y ChangeDatabaseAsync en pruebas del Lab; no enumeración automática de bases en Web/Listener. No es prueba sobre software externo |
| Tareas conocidas | Kudu enumera cero continuouswebjobs y cero triggeredwebjobs. Oficina: cero tareas/servicios identificados por nombre o comandos RADAR/RSMaps; no se inspeccionó otra computadora |
| Destino en configuración | Nombre temporal ausente de valores de conexión y ajustes ARM; tras restaurar, cadenaSQL sigue apuntando a mapsMarkers |

Corrección de evidencia 5H.2: Azure CLI entrega conexiones en el campo value, no
connectionString. La comprobación anterior sobre ese segundo campo no era válida.
Se repitió correctamente sobre value no vacío; resultado sin referencia al destino.
También se corrigió la inspección PowerShell usando set_ConnectionString: asignar
la propiedad sobre un constructor que implementa IDictionary produjo una entrada
ConnectionString artificial. Los resultados intermedios vacíos/anidados se descartaron;
no indican una configuración defectuosa de Azure.

Riesgos confirmados: copia con datos productivos bajo el mismo servidor y acceso
administrativo existente, reglas compartidas y conservación facturable hasta borrado.
Mitigaciones: nombre único, configuración productiva conservada, alcance temporal,
TLS validado, consultas sin registros personales, ausencia de jobs conocidos y ninguna
nueva concesión de acceso. Desconocidos: software externo no inventariado y contexto
runtime efectivo. No se exige demostrar ausencia absoluta de acceso; tampoco se
certifica aislamiento por usuario. No se identificó necesidad de cambiar producción.

## Preflight final y única operación

Se volvió a comprobar suscripción Enabled, pertenencia del servidor, origen Online,
destino ausente, northcentralus, Basic Available, punto posterior a earliestRestoreDate
y anterior al momento actual, tarifa pública USD 0.161/día. Retención reconsultada: 7 días.
Un error local de conversión cultural de fecha se corrigió antes de crear; no hubo
solicitud de restauración durante ese error. No se sustituyó el instante autorizado.

- Servidor: dpa6ecad46; grupo: Default-SQL-NorthCentralUS.
- Origen: mapsMarkers.
- Destino: mapsMarkers-pitr-radar-20261106-1600-7c9e2a.
- Punto PITR: 2026-11-06T16:00:00Z.
- Configuración resultante: northcentralus, Basic 5 DTU, 2147483648 bytes, backup Local.
- Solicitud iniciada: 2026-11-06T17:13:25.2066539Z.
- Inicio de operación informado por Azure: 2026-11-06T17:13:27.467Z.
- HTTP 202; request ID: 76d562de-aec6-482a-bd08-449a21acce4c.
- Un único PUT PointInTimeRestore, sin reintentos automáticos; seguimiento sólo GET.
- Succeeded observado: 2026-11-06T17:16:09.3371446Z; destino Online confirmado.
- Tiempo hasta observar éxito: aproximadamente 2 min 44 s desde solicitud. Azure no
  devolvió endTime en el resultado consultado; creationDate no se equipara a fin exacto.
- ID ARM completo y URL de operación conservados en recibo local privado, fuera de Git:
  %LOCALAPPDATA%\Temp\radar-pitr-20261106-7c9e2a-receipt.json.
  No contiene token ni cadena de conexión. No repetir Restore aunque se elimine ese recibo.

## Validación sólo lectura sobre la copia

| Comprobación | Resultado |
| --- | --- |
| DB_NAME | Nombre temporal exacto confirmado antes de cada grupo de consultas |
| Autenticación del validador | SQL; db_owner. No es evidencia de identidad del worker |
| Cifrado | sys.databases.is_encrypted=true y TDE ARM Enabled; TLS/certificado verificados al conectar |
| Estructura | Processing 25 columnas; Event 8; Delivery 15 |
| Índices | 9 habilitados, incluidas PK e índices únicos de idempotencia |
| Clave Processing | IdAgent, ChatOrigen, MessageId, en ese orden |
| Clave Delivery | IdRadarMessageProcessing, ClaveEntrega, en ese orden |
| Restricciones | 4 CHECK y 3 FK habilitadas y trusted |
| Conteos agregados | Processing 425; Event 1992; Delivery 32 |
| Consistencia | Cero claves Processing duplicadas, cero ClaveEntrega duplicadas, cero eventos/deliveries huérfanos, cero Processing sin Agent y cero intentos negativos |
| Principales adicionales | Cero principales no-role con principal_id>4 en sys.database_principals; no excluye dbo, administradores ni permisos de nivel servidor |
| Reglas de firewall de base | Cero; sigue sujeto a reglas del servidor |
| Migración 57 | Columna MaxIntentosProcesamiento ausente. No se ejecutó 57 ni 58 |
| Origen y aplicación | mapsMarkers sigue Online; conexión configurada del Web sigue en mapsMarkers; sin valores con destino temporal |

Un primer batch SELECT falló con error 156 por alias reservado RowCount; se corrigió
como [RowCount] y las lecturas posteriores pasaron. No implicó escrituras ni otra
restauración. No se extrajeron mensajes, usuarios, teléfonos ni payloads. Los conteos
no se comparan con una fotografía fuente al mismo instante: prueban legibilidad y
consistencia básica, no equivalencia exhaustiva ni DBCC CHECKDB. No se ejecutaron
regresiones que escriban en esta copia ni pruebas sobre producción.

## Conservación, costo y borrado pendiente

A las 17:18:28Z la copia permanecía Online. Tarifa estimada USD 0.161/24 h, unos
USD 0.006708/h antes de impuestos y condiciones contractuales. Tiempo transcurrido
desde éxito observado menor de una hora: reservar aproximadamente USD 0.007 por la
primera hora; no es una lectura de factura. La copia seguirá acumulando costo.
Microsoft indica que la facturación comienza al completarse la restauración.

Validación terminada: copia lista para solicitar eliminación. NO ELIMINADA.
Conservación objetivo máxima de 24 h; revisar para eliminar antes del 2026-11-07T17:15Z
como margen conservador. No hay borrado ni alerta automatizados. La autorización de
creación se consumió; borrar, cancelar o crear otra copia no está autorizado.

Operación preparada, NO ejecutada: leer el recibo privado, verificar suscripción,
servidor, grupo y nombre exactos, comparar el ID ARM del destino actual con targetId,
comprobar que no es sourceId ni mapsMarkers y sólo con autorización independiente:

```powershell
# No ejecutar sin autorización explícita de eliminación.
$target = 'mapsMarkers-pitr-radar-20261106-1600-7c9e2a'
az sql db delete --subscription $verifiedSubscription --resource-group Default-SQL-NorthCentralUS `
  --server dpa6ecad46 --name $target --yes
```

No se cambió firewall, permisos, usuarios, credenciales, App Service o producción.
No despliegues, migraciones ni Listener. READY_TO_DEPLOY=NO: la restauración exitosa
no resuelve la identidad runtime pendiente ni autoriza despliegue.

## Validación documental y fuentes

Diff revisado, git diff --check, escaneo dedicado de secretos y sincronización Git.
Sólo documentación publicada; recibos privados fuera del repositorio. La consulta
propuesta a un servicio externo de IP fue rechazada por revisión automática antes de
ejecutarse; se descartó y se verificó la ruta directamente contra Azure SQL, sin terceros.
No se requiere aprobar esa alternativa descartada.

- https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure
- https://learn.microsoft.com/en-us/rest/api/sql/databases/create-or-update?view=rest-sql-2023-08-01
- https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups
- https://prices.azure.com/api/retail/prices
