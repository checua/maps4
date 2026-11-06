> Cierre 5H.4: copia temporal eliminada con autorización expresa el 2026-11-06.
> GET posterior 404; producción Online y conexión de rsmap conservada.
> Ver RADAR_PITR_CLOSEOUT_5H4.md. Los avisos de conservación/borrado pendiente
> siguientes son históricos. READY_TO_DEPLOY=NO.

> Actualización 5H.3 (2026-11-06): ensayo APTO bajo riesgo acotado y única copia PITR
> completada/validada. Ver RADAR_PITR_RESULT_5H3.md; los bloqueos PITR siguientes son
> históricos. Identidad runtime pendiente, READY_TO_DEPLOY=NO. Copia conservada;
> eliminación requiere autorización independiente. No repetir Restore.

> Actualización 5H.2, 2026-11-06: el propietario autorizó una única restauración
> condicionada al preflight y lectura de la copia. PREFLIGHT=BLOCKED por aislamiento
> no acreditado; Restore NO INICIADO. Ver RADAR_PITR_PREFLIGHT_5H2.md.
> No autorizó borrado ni modificaciones de acceso. El texto 5H.1 siguiente conserva
> la propuesta histórica; RESTORE_AUTHORIZED=NO describe sólo aquel momento.
# RADAR — Ficha de autorización PITR, Hito 5H.1

Preparada el 2026-11-06 en DESKTOP-E32803V. Sólo se consultaron metadatos Azure,
precios públicos y ayuda CLI. No se creó ningún recurso ni se ejecutaron consultas SQL.
RESTORE_AUTHORIZED=NO. READY_TO_DEPLOY=NO.

## Origen, destino y punto propuesto

| Campo | Valor verificado o propuesta |
| --- | --- |
| Grupo de recursos | Default-SQL-NorthCentralUS |
| Servidor lógico origen y destino | dpa6ecad46 |
| Base origen | mapsMarkers |
| Región origen y destino | northcentralus |
| Estado origen | Online |
| SKU origen y destino propuesto | Basic, 5 DTU, base individual sin elastic pool |
| Tamaño máximo configurado | 2147483648 bytes, 2 GiB; tamaño utilizado no consultado |
| Backup | Local, sin redundancia zonal |
| TDE origen | Enabled |
| Retención PITR | 7 días, diferencial cada 12 horas |
| Primer punto restaurable observado | 2026-10-30T16:39:07.577760Z |
| Hora de consulta | 2026-11-06T16:39:14Z |
| Punto propuesto | 2026-11-06T16:00:00Z, 10:00 en Ciudad de México |
| Base temporal propuesta | mapsMarkers-pitr-radar-20261106-1600-7c9e2a |
| Colisión de nombre | No existente al consultar el servidor |
| Conservación propuesta | 24 horas desde que termine la restauración; eliminación antes si concluye validación |
| Responsable | Propietario del proyecto, con operador expresamente designado |

El punto propuesto está dentro del intervalo de retención observado y precede la
consulta en unos 39 minutos. No se obtuvo un latestRestoreDate independiente ni se
ensayó la restauración. La disponibilidad final debe revalidarse en Azure al autorizar,
y el punto debe seguir siendo mayor que earliestRestoreDate. Si caduca, emitir una
ficha actualizada con un punto reciente; nunca sustituirlo silenciosamente.

PITR de esta modalidad restaura a una base nueva en el mismo servidor. No requiere
un servidor nuevo ni cambio de región. La existencia del origen Basic acredita el
SKU observado, pero cuota, permisos RBAC y disponibilidad de capacidad para el nuevo
recurso se comprobarán antes de crear. La ventana visible no acredita restauración real.

## Estimación de costo

Azure Retail Prices API consultada el 2026-11-06, filtro SQL Database, northcentralus,
Consumption. Medidor SQL Database Single Basic, SKU B, B DTU, USD 0.161 por día,
fecha efectiva 2014-11-01. Equivalente aproximado USD 0.006708 por hora.

- Dos horas facturadas, aproximadamente USD 0.0134.
- Veinticuatro horas facturadas, aproximadamente USD 0.161.
- Margen conservador de 25 horas por límites de facturación, aproximadamente USD 0.168.
- Presupuesto propuesto USD 1 antes de impuestos para este único ensayo; no es un
  límite automático de Azure. Si se requiere otro SKU, detenerse y recalcular.

Supuestos: Basic estable, 2 GiB incluidos, sin pool, sin LTR nuevo, sin exportaciones
ni tráfico de datos significativo. El PITR backup está incluido en el modelo DTU.
Las tarifas normales comienzan tras completar la restauración; Basic se factura por
hora y sigue facturándose aunque no se consulte. El importe real depende del contrato,
redondeos, moneda e impuestos. La fecha de eliminación no está automatizada.
Proponer alerta operativa a las 20 horas y eliminación a las 24, con autorización
expresa de eliminación. Una demora de restauración debe revisarse a las dos horas,
sin interpretar ese plazo como garantía de RTO.

## Protección de datos y condición de aislamiento

La restauración contiene datos productivos y usuarios/permisos restaurados. Usar
una base con nombre distinto no garantiza aislamiento de acceso. El servidor tiene
publicNetworkAccess=Enabled, TLS mínimo 1.2, 55 reglas de firewall y regla que permite
servicios Azure. No se publican IPs ni identidades. No cambiar ese firewall compartido.

Antes de autorizar Restore, documentar el operador, la ruta de acceso ya permitida y
el control de acceso de la copia. La identidad SQL efectiva del Web sigue INCONCLUSA
en 5G.1, por lo que no está demostrado que pueda excluirse de la base restaurada.
Si el requisito es impedir técnicamente cualquier acceso de cuentas runtime, se
necesita resolver esa identidad y aprobar un diseño de permisos exclusivo del destino.
Una denegación a un usuario ordinario no restringe necesariamente a administradores.
No emitir una garantía falsa basada únicamente en el nombre de la copia.

Condiciones obligatorias para que ningún servicio use la copia

1. Conservar las conexiones de Web, Listener, servicios y jobs en su destino actual.
   No renombrar bases ni publicar ajustes que incluyan el nombre temporal.
2. Antes y después, comparar privadamente sólo los destinos resueltos de configuración
   de servicios conocidos, sin imprimir cadenas ni credenciales. Si no puede comprobarse
   el destino de alguno, registrar INCONCLUSO y bloquear la ejecución del ensayo hasta aclararlo.
3. Identificar jobs o automatizaciones que descubran bases automáticamente. Excluir la
   copia requiere autorización separada si implica modificar esos servicios.
4. Validación manual desde un cliente aprobado, dirigido explícitamente a la copia.
   Guardar resultados agregados en evidencia privada; Git sólo recibe conclusiones.
5. Mantener cifrado, TLS y acceso mínimo. Ninguna exportación, BACPAC, backup descargado,
   datos de filas, secretos ni cuentas nuevas forman parte de esta ficha.

El plan queda condicionado al cierre de estos controles. No se propone modificar
producción ni instalar servicios para conseguir aislamiento. Si el servidor compartido
no satisface la protección requerida, detenerse y diseñar otro ensayo autorizado.

## Operaciones que requieren autorización expresa

A. Crear exactamente una base Basic a partir de mapsMarkers en el punto y destino
   indicados, asumiendo el costo aprobado y el acceso previamente revisado.
B. Conectar al destino y ejecutar únicamente las comprobaciones de lectura siguientes.
C. Eliminar exclusivamente la base temporal al concluir o al vencer la conservación.
D. Cancelar una restauración mediante eliminación del destino incompleto, sólo si el
   propietario autoriza esa cancelación. Un reintento requiere autorización o política
   de reintentos explícita, nuevo nombre y revisión de costos.

Cualquier cambio de permisos de la copia, firewall, identidades, SKU o retención necesita
una autorización adicional y una revisión de impacto. SQL 57/58 y despliegues quedan fuera.

## Procedimiento preparado, NO EJECUTADO

Asignar la suscripción aprobada de la sesión privada, no deducirla de otro proyecto.
Revalidar origen Online, retención, nombre libre, cuota, costo y aislamiento antes de A.
No ejecutar el bloque de creación hasta aprobar expresamente la ficha.

```powershell
$subscription = '<id-de-suscripcion-verificado-privadamente>'
$group = 'Default-SQL-NorthCentralUS'
$server = 'dpa6ecad46'
$source = 'mapsMarkers'
$target = 'mapsMarkers-pitr-radar-20261106-1600-7c9e2a'
$point = '2026-11-06T16:00:00Z'

# A — únicamente después de autorización expresa
az sql db restore --subscription $subscription --resource-group $group `
  --server $server --name $source --dest-name $target --time $point `
  --edition Basic --service-objective Basic --backup-storage-redundancy Local
```

Registrar privadamente el ID de operación, instante solicitado, inicio/fin y recurso
creado. Consultar az sql db show del destino hasta Online o fallo. Revisar TDE y SKU.
No aceptar la mera existencia de la base como éxito de la recuperación.

## Validación prevista, sólo sobre el destino autorizado

La sesión debe comprobar nombre antes de cualquier consulta adicional. El siguiente
bloque debe ejecutarse junto, con un cliente que aborte ante error. Todos los resultados
permanecen privados. No reutilizar una conexión con el origen.

```sql
IF DB_NAME() <> N'mapsMarkers-pitr-radar-20261106-1600-7c9e2a'
    THROW 55001, 'Unexpected PITR validation target', 1;

SELECT DB_NAME() AS DatabaseName,
       DATABASEPROPERTYEX(DB_NAME(), 'Status') AS DatabaseStatus,
       DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS Collation;

SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName,
       ty.name AS TypeName, c.max_length, c.is_nullable
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE s.name=N'dbo' AND t.name IN
(N'RSMAPS_RadarMessageProcessing',N'RSMAPS_RadarMessageEvent',N'RSMAPS_RadarMessageDelivery');

SELECT OBJECT_NAME(object_id) AS TableName, name, is_unique, is_disabled
FROM sys.indexes
WHERE object_id IN (OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing'),
                   OBJECT_ID(N'dbo.RSMAPS_RadarMessageDelivery'));

SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.RSMAPS_RadarMessageProcessing');

SELECT N'Processing' AS ObjectName, COUNT_BIG(*) AS RowsTotal
FROM dbo.RSMAPS_RadarMessageProcessing
UNION ALL SELECT N'Event', COUNT_BIG(*) FROM dbo.RSMAPS_RadarMessageEvent
UNION ALL SELECT N'Delivery', COUNT_BIG(*) FROM dbo.RSMAPS_RadarMessageDelivery;

SELECT Estado, COUNT_BIG(*) AS RowsTotal
FROM dbo.RSMAPS_RadarMessageProcessing GROUP BY Estado;
```

Comprobar estructura e índices contra evidencia conocida del origen al punto solicitado.
No exigir columnas de SQL 57 si esa migración no estaba aplicada en ese instante.
Los conteos actuales de producción o los históricos de otro día no son un baseline
exacto del punto PITR. Si falta baseline fechado, declarar recuperación operativa
comprobada y equivalencia de contenido no comprobada; no inventar conteos esperados.
La prueba no implica ejecutar RADAR, Listener, Delivery real ni enviar mensajes.

## Fallo, recuperación y eliminación

Si falla Restore, registrar el estado y código de error sanitizado. Confirmar por
metadatos que el origen permanece Online. No reparar ni cambiar el origen, repetir
solicitudes a ciegas, subir de SKU o cambiar el punto automáticamente. Revisar nombre,
retención, cuotas y capacidad. Una operación incompleta puede consultarse o escalarse
con el ID de operación. Eliminarla para cancelar requiere D.

Con autorización C, validar la suscripción y el ID exacto del recurso destino contra
la ficha, y confirmar que $target es exactamente el nombre aprobado y distinto de
mapsMarkers. Verificar también que ninguna aplicación lo usa. Después, únicamente
sobre ese recurso

```powershell
if ($target -ne 'mapsMarkers-pitr-radar-20261106-1600-7c9e2a' -or $target -eq $source) {
    throw 'Invalid deletion target'
}
# C — únicamente después de autorización expresa y verificación del resource ID
az sql db delete --subscription $subscription --resource-group $group `
  --server $server --name $target --yes
```

Comprobar eliminación mediante inventario y revisar posteriormente facturación.
La eliminación lógica no implica borrado inmediato de todas las copias de backup;
los backups automáticos siguen el ciclo de retención de Azure. La evidencia privada
se conserva sólo el plazo aprobado. Un plazo incumplido no autoriza borrar otro recurso.

## Fuentes y trazabilidad

- Metadatos az sql db show, str-policy show, tde show, server show y list del 2026-11-06.
- https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups
- https://learn.microsoft.com/en-us/cli/azure/sql/db
- https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/
- https://prices.azure.com/api/retail/prices
  Filtro serviceName=SQL Database, armRegionName=northcentralus, priceType=Consumption;
  producto SQL Database Single Basic, skuName=B, meterName=B DTU.