# Respaldo y rollback compatible

Hito 4E: retención y ventana PITR de mapsMarkers siguen pendientes de consulta
autorizada al portal. El reporte de preflight identifica las vistas y evidencia
necesarias. No se ejecutó restore ni se creó un destino aislado. SQL58 se excluye
del primer despliegue y del procedimiento de recuperación operacional inicial.

## Gate de recuperación productiva — Hito 4D

Obtener por acceso autorizado de lectura la política de retención efectiva de
mapsMarkers, ventana restaurable, timestamps UTC disponibles y estado de backups.
Identificar suscripción/base/servidor sin exportar credenciales. Registrar principal
habilitado para restaurar y destino aislado aprobado con capacidad y costo aceptados.
Hasta verificarlo no asignar valores supuestos de retención ni RPO/RTO.

El ensayo futuro debe restaurar a una base nueva expresamente autorizada, sin
sobrescribir mapsMarkers ni cambiar la conexión Web. Verificar esquema, constraints,
conteos y muestra 356 a la fecha restaurada, integridad y posibilidad de conectar
la publicación compatible con configuración aislada. Registrar inicio/fin y RTO
observado. La limpieza del destino requiere autorización y verificación de identidad.
No efectuar envíos WhatsApp ni replay contra la base recuperada. Sólo tras ese
ensayo podrá marcarse PRODUCTION_RECOVERY_VERIFIED=YES.

La política de backup automática, aunque se confirme, no reemplaza evidencia de
restauración. Actualmente retención y ensayo real permanecen pendientes.

## Ensayo integral local Hito 4B

El procedimiento scripts/Test-PoisonRollback.ps1 genera una publicación completa,
excluye appsettings originales antes de copiar, usa configuración no secreta para
aislamiento, comprueba dependencias requeridas, crea manifiesto y ZIP, restaura y
verifica todos los hashes. Después ejecuta regresiones con las DLL restauradas y
arranca esa publicación en loopback con entorno limpio y conexión exclusivamente
LocalDB. Cierra únicamente el proceso que creó. No inicia Listener.

Ensayo ejecutado con 345 archivos y 31163472 bytes publicados. Restauración exacta,
regresiones poison HTTP/SQL/Delivery y arranque local PASS. Login y asset index.js
respondieron HTTP 200. La regresión SQL aplica 44/45/57/58 a una base LocalDB
efímera, verifica terminales, leases, presupuesto y auditoría y elimina sólo esa base.
No se ejecutó una petición HTTP autenticada contra la base productiva.

Paquete y evidencias bajo RSMaps.Radar.Lab/bin/rollback-59761964abdd40b1962174abf80848fc.
ZIP web-compatible-rollback.zip, SHA256
240D0443DAC9CB94B5FFEDE42EA0780661A9BAFBC0EB01C6D48D30C34D7AF17D.
Contiene deps/runtimeconfig, web.config, assets, conocimiento y bibliotecas de
publicación. La configuración incluida usa Local y hosts loopback para el ensayo.
Antes de usarlo operacionalmente restaurar configuración externa autorizada de SQL,
OpenAI, almacenamiento, URLs y hosts. No desplegar esta configuración de prueba.
Las claves públicas cliente preexistentes en JS no se utilizaron mediante navegador
ni se reconfiguraron. El escáner es heurístico y no certifica ausencia universal de
secretos binarios. Conserva el gate de auditoría de seguridad del paquete autorizado.

El resultado sustituye el ensayo anterior de overlay parcial. La publicación es
reconstruible mediante el script y manifiesto de fuentes, pero no se promete ZIP
bit a bit idéntico porque incluye timestamps. El candidato sigue sin commit.

## Reemplazo Web bajo autorización futura

Suspender todos los consumidores y bloquear nuevas peticiones Agent mediante el
mecanismo operativo aprobado. Drenar solicitudes existentes y esperar leases.
Detener escritores en todas las instancias/slots antes de intercambiar archivos.
En App Service Windows, una ventana controlada con app_offline.htm o parada de
la aplicación requiere autorización explícita y verificación de workers cerrados.
Una migración a slot exige comprobar configuración y ausencia de workers viejos
conectados a la misma base. No confiar sólo en el cambio de tráfico.

Respaldar paquete y configuración externa recuperables, verificar manifest contra
el artefacto aprobado, aplicar SQL57 autorizado y activar Web compatible antes de
Listener. Comprobar HTTP/login, contrato 422, logs y colas sin reenviar mensajes
reales. Cancelar ante proceso antiguo escritor o cualquier terminal reabierto.
Si se revierte, restaurar este backend compatible con configuración autorizada,
conservar esquema y auditoría y validar nuevamente antes de reanudar consumidores.

Hito 4 produjo y restauró en aislamiento un overlay de cuatro binarios del backend
compatible. Sus hashes coinciden con el build probado. El manifiesto y ZIP están
referenciados en RADAR_POISON_PREFLIGHT_REPORT.md. Debe combinarse con un paquete
completo compatible y probar su arranque antes de habilitar rollback operativo.
No se considera completada la recuperabilidad integral por esta prueba parcial.

Procedimiento preparado para autorización posterior. Conservar evidencias con UTC,
SHA del candidato y hashes SHA256. Usar destinos nuevos, acceso restringido y espacio
verificado. Nunca sobrescribir respaldos existentes.

## Recuperabilidad

1. Identificar Web activo y su configuración mediante mecanismos autorizados. Obtener
   backup soportado de aplicación y respaldo cifrado de configuración por separado.
   Registrar tamaño/hash del paquete. Probar extracción en destino aislado y comparar
   manifiesto completo. Verificar web.config, ensamblados, assets y RadarKnowledge.
2. Inventariar Listener, launcher y tarea. Registrar acciones, identidad, working
   directory y variables por nombre, sin valores secretos. Confirmar perfil efectivo
   antes de preparar cualquier reemplazo.
3. En mantenimiento autorizado, con todos los procesos detenidos, respaldar binarios
   y configuración en destino nuevo restringido. Preservar perfil y credenciales en
   sitio. Si se autoriza backup del perfil, hacerlo sólo cerrado y cifrado. Registrar
   dependencia de DPAPI respecto de cuenta y equipo. No probar credenciales ni abrir
   WhatsApp para validar el respaldo.
4. Ensayar restauración de binarios en un directorio aislado y verificar igualdad de
   hashes y archivos requeridos. Ejecutar regresiones offline con configuración
   sintética. Una extracción correcta por sí sola no valida credenciales DPAPI ni
   autenticación Web. Registrar esas comprobaciones pendientes por separado.
5. Confirmar política de recuperación SQL/PITR con el administrador antes de migrar.
   La prueba de restauración requiere una base aislada expresamente autorizada y
   control de costo. No restaurar una base histórica sobre la base viva como rollback
   ordinario porque se perderían estados y auditorías posteriores.

## Artefacto de rollback obligatorio

El Web anterior puede reabrir FALLIDO_PERMANENTE. Su backup sirve para recuperación
forense, pero no habilita reanudar intake con ese código. Preparar y probar un artefacto
alternativo que conserve las guardas de claim, presupuesto, leases, auditoría y cache,
además del contrato 422. Verificarlo con las regresiones de poison y SQL aisladas.
Hasta disponer de ese artefacto recuperable, el despliegue queda bloqueado.

Para Listener conservar una versión que reconozca 422 y deshabilite fallback central.
Volver al Listener antiguo sólo es una contingencia explícitamente evaluada, con
fallback=0 y Delivery durable, y deja retry local de terminales. Preferir mantener
intake suspendido hasta restaurar una pareja Web/Listener compatible.

## Ejecución ante fallo

1. Suspender nuevos claims y consumidores bajo autorización operacional. Capturar
   evidencia sanitizada y permitir finalizar o expirar leases sin resetearlos.
2. Restaurar Web compatible y verificar hashes, configuración y salud antes de
   habilitar consumo. Si falta artefacto compatible, mantener intake detenido.
3. Restaurar sólo archivos Listener incluidos en manifiesto, con procesos cerrados.
   Conservar logs, credenciales, launcher y perfil. No borrar archivos desconocidos.
4. Mantener SQL 57, presupuestos, terminales y eventos. Dejar 58 sin invocar. No DROP,
   reset de intentos, reactivación automática, backfill ni replay de mensajes reales.
5. Verificar una sola instancia y observar procesamiento normal. Si falla la
   compatibilidad o reaparece un terminal, suspender nuevamente y escalar.

La nueva configuración MaxAttempts afecta nuevas filas. No usar un cambio de
configuración como sustituto de una reactivación administrativa auditada.
