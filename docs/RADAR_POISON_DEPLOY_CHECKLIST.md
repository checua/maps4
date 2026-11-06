# Poison messages — checklist operativo

Hito 4F añade diagnóstico sql-identity-diagnostic en Lab y su prueba aislada.
Antes de aceptar su resultado para Azure, acreditar equivalencia de proveedores,
slot y autenticación con el worker real. Kudu y SSMS por sí solos no acreditan esa
equivalencia. Seguir el procedimiento documentado en el reporte Hito 4F.

Decisión Hito 4E del 2026-11-05: excluir SQL58 del primer despliegue. Su instalación
y cualquier reactivación requieren un bloque administrativo independiente. Esta
decisión no libera los gates de identidad efectiva, mínimo privilegio y recuperación.
Usar la sección Hito 4E del preflight SQL para evidencia de permisos de la sesión real.

Hito 4D mantiene READY_TO_DEPLOY=NO. Los conteos aportados son 414 COMPLETADO,
356 con 124 intentos y 32 deliveries enviados. SQL57 los preserva sin reapertura.
Antes de habilitar recovery confirmar Estado/lease/retry/ACK/JSON de 356 y aprobar
su eventual terminalización automática. Confirmar además que no haya Delivery
pendiente no reflejado por el conteo de enviados.

- [ ] Evidencia del principal SQL efectivo de la aplicación y permisos.
- [ ] Retención real y ventana PITR comprobadas por lectura autorizada.
- [ ] Restauración de backup productivo ensayada en destino aislado autorizado.
- [ ] Manifiesto de publicación Azure actual comparado con el paquete compatible.
- [ ] Configuración externa restaurable verificada por nombres y presencia, sin valores.

Configuración a contrastar con el proveedor efectivo: ConnectionStrings:cadenaSQL,
OPENAI_API_KEY, RSMaps:ImageStorageProvider, RSMaps:ImageStorageContainer,
ConnectionStrings:RSMapsImages cuando corresponda AzureBlob, RSMaps:PublicBaseUrl,
RSMaps:PublicAssetBaseUrl, AllowedHosts, Logging y Radar:Processing:MaxAttempts.
Inventariar overrides de entorno, configuración de slot y perfil de ejecución.
La configuración Local/loopback del ensayo no debe sustituir la configuración
productiva. No inferir presencia actual de claves a partir de revisiones históricas.

Actualización Hito 4B: publicación Web completa, extracción y hashes de 345 archivos,
arranque local y regresiones con DLL restauradas PASS. El script reproducible es
scripts/Test-PoisonRollback.ps1. El gate de rollback local queda verificado para
configuración aislada. Mantener pendientes configuración/backup productivos,
permisos SQL, inventario de colas y autorización de mantenimiento.

Hito 4 comprobó build, regresiones y restauración por hashes de un overlay binario.
Mantener pendientes los gates de SQL productivo, tarea/perfil efectivo y rollback
integral. Consultar evidencia y ruta temporal en RADAR_POISON_PREFLIGHT_REPORT.md.

Preflight local del 2026-11-04. La ejecución productiva requiere autorización posterior.

- [ ] Revisar y versionar el candidato autorizado. Registrar SHA, hashes y resultados de pruebas. Actualmente HEAD 5299f15 contiene cambios locales pendientes.
- [ ] Ejecutar preflight_radar_poison_readonly.sql mediante conexión autorizada a mapsMarkers. Registrar contexto, UTC y permisos en ubicación privada.
- [ ] Confirmar dependencias 44/45, índice único habilitado no filtrado con claves IdAgent/ChatOrigen/MessageId, Estado nvarchar(40), VersionFila rowversion, eventos y Delivery.
- [ ] Comparar checks existentes con script 57 completo. Comprobar presupuesto existente por SELECT separado si la columna ya existe. Revisar estados desconocidos y terminales que incumplan invariantes.
- [ ] Verificar elegibilidad exacta del repositorio además del cribado SQL. Revisar resultados corruptos, identidades y listas con el lector compartido en un entorno aislado autorizado.
- [ ] Revisar 356 con la consulta por PK numérica. Confirmar que ésa sea la identidad histórica prevista. Resolver cualquier ambigüedad antes de actuar.
- [ ] Identificar principal de migración, principal administrativo y principal de aplicación mediante sesiones autorizadas. Script 57 requiere ALTER de Processing y visibilidad de metadatos. Instalar 58 requiere CREATE PROCEDURE y ALTER del esquema para creación, o ALTER del procedimiento existente. Su ejecución exige CONTROL y autorización explícita.
- [ ] Resolver si aplicación tiene CONTROL o db_owner. Omitir 58 hasta separar permisos. No conceder CONTROL a la aplicación para habilitar reactivación.
- [ ] Aprobar inventario de pendientes agotados. Recovery puede terminalizarlos automáticamente al volver a reclamar. No reactivar 356 como prueba.
- [ ] Completar y ensayar los respaldos y rollback compatibles descritos en RADAR_POISON_ROLLBACK_PLAN.md.
- [ ] Inventariar tarea, launcher, identidad Windows, permisos de carpeta, procesos hijos y perfil realmente usado. Registrar sólo metadatos permitidos.
- [ ] Acordar mantenimiento que suspenda todos los escritores antiguos y deje resolver o expirar leases. Verificar ausencia de árboles duplicados.
- [ ] Aplicar únicamente el script 57 identificado por nombre completo y hash. Verificar columna, defaults, checks habilitados y confiables. Coordinar numeración con otras ramas.
- [ ] Instalar Web compatible, verificar autenticación y respuestas mediante fixtures autorizados aislados. Script 58 queda opcional y separado.
- [ ] Reemplazar Listener mediante allowlist de artefactos auditados. Preservar launcher, configuración, credenciales, logs y perfil. Prohibido copiar todo publish encima de app o usar limpieza de directorio.
- [ ] Arrancar una sola instancia mediante su mecanismo habitual, únicamente con autorización de ejecución. Confirmar CENTRAL, fallback=0 y Delivery durable.
- [ ] Observar pasivamente un barrido real, leases, contadores y auditoría. Consultar colas con SELECT. Evitar GET Agent si actualiza UltimoUsoUtc fuera de lo autorizado.
- [ ] Verificar 422 por pruebas aisladas y evidencia existente. No inyectar mensajes WhatsApp, no prepare/complete/ACK manual, no reproducir solicitudes reales ni resetear claves de idempotencia.
- [ ] Conservar evidencia de Delivery antes y después por identificadores técnicos. Detener intake ante nuevos duplicados, terminal reabierto, audit ausente, cache corrupto procesado o error repetitivo.

Cancelar antes de cambios ante drift, permisos insuficientes, backup sin restauración
ensayada, ausencia de rollback compatible, candidato distinto, mezcla de escritores
o configuración no identificada. Cancelar durante ejecución ante fallos SQL/Web,
timeout de inicio, QR inesperado, instancia duplicada o pérdida de estado durable.
