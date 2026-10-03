# RADAR Remote Health — despliegue futuro controlado

Este documento es un runbook; no autoriza ni ejecuta cambios productivos. Cada fase requiere validación explícita antes de avanzar y conserva el Listener operativo hasta que su reemplazo esté listo.

## 1. SQL productivo autorizado explícitamente

- Confirmar backup y proveedor/base de datos reales sin exponer la cadena de conexión.
- Aplicar exclusivamente `sql/RSMaps2/57_radar_agent_health.sql` mediante el mecanismo SQL aprobado.
- Verificar tabla, PK, FK, checks, índice y segunda ejecución idempotente.
- No insertar heartbeats manuales ni modificar Agents existentes.

Rollback: retirar sólo los objetos creados por el script si aún no existen datos útiles y existe autorización SQL específica. De lo contrario, conservar el esquema inerte.

Nota predeploy: la marca `InstanceStartedUtc` debe conservar precisión `datetime2(7)` para diferenciar reinicios rápidos. El par `LastSweepStartedUtc` / `LastSweepCompletedUtc` siempre corresponde al último barrido terminado; un barrido activo no debe invalidar el snapshot persistido.

## 2. Web receptora y panel

- Publicar desde un HEAD bloqueado que contenga API, repositorio, rate limiting y panel.
- Preservar configuración y secretos del App Service.
- Confirmar que `/RadarAgent/Health` exige usuario `PROPIETARIO` con membresía activa y filtra por `IdCuenta`.
- Confirmar `Cache-Control: no-store` y ausencia de datos personales o controles remotos.

Rollback: restaurar el paquete Web anterior. La tabla puede permanecer sin afectar el sistema existente.

## 3. Validación de API

- Con una credencial Agent de prueba aprobada, validar un único heartbeat bien formado y respuesta `204`.
- Confirmar rechazo de credencial inválida, payload extra, secuencia repetida e instancia anterior.
- Confirmar aislamiento de cuenta en el panel.
- No usar contenidos de chats, teléfonos, rutas, hostname, tokens ni excepciones libres.

Rollback: volver al paquete Web anterior; no alterar el estado funcional del Listener.

## 4. Reporter productivo

- Preparar backup completo y recuperable del Listener actual.
- Publicar el Listener desde el mismo HEAD validado.
- Preservar `WhatsAppProfile`, credencial DPAPI, launcher y configuración Agent.
- Detener y arrancar únicamente mediante `RSMaps RADAR Produccion`.
- Confirmar una sola instancia de Listener y un solo árbol Playwright/Chromium.

Rollback: detener la tarea, restaurar los binarios respaldados, conservar perfil/configuración y arrancar por la tarea programada.

## 5. Tres heartbeats consecutivos

- Confirmar tres respuestas aceptadas con secuencia estrictamente creciente y misma instancia.
- Verificar intervalo aproximado de 60 segundos, UTC del servidor y versión desplegada.
- Confirmar que el panel cambia de “Sin datos” a “Online” o “Degradado” según el estado funcional real.
- Un `403`, `429`, timeout o `5xx` debe producir sólo diagnóstico sanitizado y backoff, nunca reinicio ni bloqueo del Listener.

## 6. Sweep y CENTRAL

- Confirmar WhatsApp `Ready` sin QR.
- Confirmar un barrido completo posterior al deploy, con chats configurados/revisados coherentes.
- Confirmar `CENTRAL`, fallback efectivo y última llamada central exitosa o degradación explicable.
- Revisar que captura, recovery y Delivery continúan operativos aunque el endpoint de health esté inaccesible.

## 7. Cierre o rollback no destructivo

El despliegue se aprueba sólo si Web, panel, tres heartbeats, WhatsApp, sweep y CENTRAL son estables. Ante crash, pérdida de sesión, bloqueo operativo, error de aislamiento o regresión funcional:

1. no borrar ni copiar `WhatsAppProfile`;
2. detener normalmente la tarea;
3. restaurar los binarios del Listener respaldados;
4. restaurar el paquete Web anterior si la falla está en API/panel;
5. iniciar la tarea y validar PID, logs, WhatsApp, CENTRAL y colas;
6. conservar evidencia sanitizada y no modificar manualmente los datos de health.

La ausencia de heartbeats después del rollback es esperada y el panel debe mostrar “Sin comunicación”; no constituye motivo para tocar WhatsApp, Delivery o SQL.
