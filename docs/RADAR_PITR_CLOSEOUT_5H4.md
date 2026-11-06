# RADAR — Hito 5H.4: cierre y eliminación de copia PITR

2026-11-06, DESKTOP-E32803V. Rama fix/radar-poison-message-recovery.
Base d53551edc85fb9f55a3c62f4a51d66de7c5bb825. DELETE=COMPLETED.
PITR_REHEARSAL=CLOSED. READY_TO_DEPLOY=NO.

## Autorización y guardias

El propietario autorizó exclusivamente eliminar
mapsMarkers-pitr-radar-20261106-1600-7c9e2a, servidor dpa6ecad46,
grupo Default-SQL-NorthCentralUS. mapsMarkers permanece protegida.

Antes de borrar se verificaron suscripción Enabled, servidor exacto, existencia de
la copia y coincidencia completa del ID ARM actual con targetId del recibo privado.
El ID también coincidió con el construido a partir de la suscripción seleccionada y
los nombres exactos autorizados; era distinto de sourceId, que coincidía con el ID
de mapsMarkers Online. No se publican identificadores privados de suscripción.
Se volvió a comprobar el ID inmediatamente antes de la eliminación.

Recibo presente: %LOCALAPPDATA%\Temp\radar-pitr-20261106-7c9e2a-receipt.json.
Estado de restauración Succeeded. Registro privado de validación leído: destino
correcto, tres tablas, cifrado activo y cero violaciones registradas. El informe
5H.3 confirma terminación de validación; no había trabajo pendiente sobre la copia.

Conexión cadenaSQL de rsmap dirigida a mapsMarkers; cero referencias al nombre
 temporal en conexiones y ajustes consultados. Kudu enumeró cero WebJobs continuos
y cero programados/disparados. Cero tareas y servicios de oficina identificados como
RADAR/RSMaps. Se conserva el límite de alcance del inventario documentado en 5H.3;
no se afirma haber inspeccionado software desconocido u otras computadoras.

## Operación exacta y resultado

Se ejecutó una sola invocación de Azure CLI, con suscripción verificada explícita,
sin patrones ni comodines:

```powershell
az sql db delete --subscription $verifiedSubscription --resource-group Default-SQL-NorthCentralUS `
  --server dpa6ecad46 --name mapsMarkers-pitr-radar-20261106-1600-7c9e2a --yes --only-show-errors
```

- Inicio: 2026-11-06T17:27:25.8714476Z.
- CLI completada con código 0: 2026-11-06T17:27:44.2097560Z.
- Duración observada de eliminación: aproximadamente 18 segundos.
- GET ARM al ID exacto del recibo: HTTP 404, ResourceNotFound.
- Verificación posterior completada: 2026-11-06T17:28:30.9159936Z.
- mapsMarkers: ID coincide con sourceId y estado Online.
- rsmap: conexión configurada conserva servidor dpa6ecad46 y base mapsMarkers.

No se borraron otras bases, servidor ni grupo. No cambios de firewall, usuarios,
permisos, credenciales o aplicaciones. No consultas SQL, migraciones, despliegues
ni inicio del Listener durante este cierre. La prueba PITR queda cerrada.

## Facturación y evidencia conservada

Desde éxito de restauración observado a eliminación completada transcurrieron
11 min 35 s aproximadamente. El instante exacto de finalización de Restore no fue
informado: considerar aproximadamente 12-13 minutos de existencia facturable, no
un dato de factura. Con la tarifa documentada USD 0.161/24 h, el prorrateo es del
orden de USD 0.0013-0.0015; reservar aproximadamente USD 0.007 si se cobra una hora
completa. Contrato, redondeos e impuestos determinan el importe real. No se consultó
factura ni se verificó retención de backups con este borrado.

Se conserva únicamente evidencia de operación necesaria: recibo privado con ID
exacto, tiempos y estado GET, y registro agregado de validación sin datos personales.
El recibo se actualizó con resultado de eliminación, producción Online y comprobación
de configuración; no contiene token ni cadena de conexión. No se copiaron a Git esos
archivos. Las variables temporales de token/conexión se limpiaron al terminar.
La ausencia del recurso activo no certifica destrucción inmediata de backups retenidos.
No se autorizó ni ejecutó su purga.

## Continuidad

Docs de cierre y handoff sanitizados; revisión de diferencias, git diff --check,
escaneo dedicado de secretos y comprobación de sincronización antes/después de publicar.
No se requieren regresiones de código por un cambio sólo documental.
Identidad efectiva del worker y demás condiciones de despliegue siguen pendientes.
READY_TO_DEPLOY=NO. No volver a crear la copia con la autorización consumida.
