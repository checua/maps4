# RADAR_AUTOSTART_RESILIENCE_01 — Diseño previo (sin implementación)

**Estado:** propuesta para revisión. No ejecutar scripts ni modificar Task Scheduler, producción, SQL, Web, `master` o `WhatsAppProfile` mediante este documento.

## Objetivo

Mantener RADAR operativo en la laptop Windows tras inicio de sesión, reanudación de suspensión y fallos accidentales del Listener/Chromium, **sin contrariar un paro manual**. El monitor remoto se implementa por separado: este hito no introduce control desde internet.

## Premisas verificadas en código / historial operativo

- RADAR Listener usa Playwright `LaunchPersistentContextAsync` con **Chromium visible** (`Headless = false`): requiere escritorio de usuario interactivo.
- La tarea productiva existente se denomina `RSMaps RADAR Produccion` y se ha observado iniciando tras inicio de sesión.
- `RadarAgentInstanceLock` emplea un mutex `Local\\RSMaps.RadarAgent.*` derivado de la ruta de configuración para prevenir instancias duplicadas **en una misma sesión**; no sustituye comprobación de árbol y tarea.
- WhatsApp `WaitingForReady` o `LoggedOut` puede ser un estado recuperable: **no reiniciar** sólo por ese estado.
- Cerrar Chromium manualmente es indistinguible de una caída para un supervisor externo; la intención de parada requiere acción explícita.

## Contrato operativo

| Evento | Resultado esperado |
| --- | --- |
| Reinicio Windows + inicio de sesión interactiva | Arranque único de RADAR, salvo paro manual vigente |
| Equipo encendido sin inicio de sesión | No prometer Chromium visible ni escucha activa |
| Suspensión / reanudación | Comprobación idempotente, sin sesión paralela ni pérdida de perfil |
| Listener finaliza inesperadamente | Reinicio con límite de reintentos y backoff |
| Chromium termina inesperadamente | Permitir recuperación interna; si el árbol desaparece persistentemente, reinicio **controlado** de Listener |
| WhatsApp esperando sincronización / QR | Mantener Listener vivo y estado degradado, sin forzar logout ni reiniciar por esperar |
| Botón "Detener RADAR" | Persistir bandera de paro **antes** de detener procesos; no resucitar al reiniciar Windows |
| Botón "Iniciar RADAR" | Eliminar bandera, iniciar tarea una vez y verificar resultado |
| Falta red / Azure / CENTRAL | No provocar reinicios por fallos externos; Remote Health informará degradación |

## Bandera de paro intencional

Propuesta de ruta local, fuera de la aplicación y de `WhatsAppProfile`:

`%LOCALAPPDATA%\\RSMaps\\RadarAgent\\AgentProduccion\\control\\manual-stop.flag`

- Crear bandera **atómicamente** mediante archivo temporal y rename, con acceso restringido al usuario dueño de RADAR.
- Crear antes de parar la tarea, para evitar carrera con watchdog.
- No eliminarla al cerrar sesión, suspender o reiniciar Windows.
- Sólo `Iniciar RADAR` la elimina; documentar cómo inspeccionarla manualmente.
- Evitar que scripts antiguos `Start-RadarProduction.ps1` arranquen al margen de esta política: el wrapper/guard deberá imponer la bandera tanto al inicio como en la supervisión.
- No almacenar secretos ni razones personales en la bandera.

## Tareas y procesos

1. Preservar la tarea `RSMaps RADAR Produccion` y su lanzador; auditar primero su XML/configuración real desde la laptop.
2. Diseñar un watchdog local mínimo, con disparador al **iniciar sesión** y comprobaciones periódicas (propuesta: 60–120 s), que no necesite privilegios elevados persistentes.
3. Validar sesión interactiva antes de intentar abrir Chromium. Opcionalmente detectar reanudación por evento Windows, pero no depender de IDs de eventos no verificados.
4. Comprobar bandera de paro al inicio de cada decisión.
5. Confirmar **un solo Listener productivo** y únicamente Node/Chromium pertenecientes al árbol del Listener (no confundir con otros procesos).
6. Antes de reiniciar, registrar motivo/candidato y confirmar que no hay operación de despliegue/copias en curso.
7. Limitar a tres reinicios por ventana de 30 min, con backoff y registro; al alcanzar límite, dejar estado **requiere atención**, no loop infinito.
8. No tocar Chrome personal, otros node.exe, otros procesos ni sesiones ajenas.
9. Evitar escrituras repetidas en disco; registros sanitizados con retención.
10. Mantener una operación totalmente local, sin endpoint remoto de control.

## Implementación prevista (bloques futuros)

- `tools/radar/Start-RadarControlled.ps1`: quitar bandera, comprobar único árbol, iniciar tarea, verificar resultado sin inventar Ready.
- `tools/radar/Stop-RadarControlled.ps1`: crear bandera, detener tarea/árbol exclusivamente productivo, validar 0 procesos.
- `tools/radar/Test-RadarWatchdog.ps1`: lectura segura de estado, idempotencia y reinicio acotado; modo `-WhatIf` obligatorio.
- `tools/radar/Install-RadarWatchdog.ps1`: exportar tarea original y preparar cambios reversible/opt-in; **no ejecutar sin autorización humana**.
- Documentar en `docs/` el rollback que restaura tarea original y respeta `WhatsAppProfile`.

Los nombres son propuestas, no scripts existentes. Separar watchdog de la telemetría remota y del Listener.

## Pruebas de aceptación necesarias

1. Login Windows sin bandera → exactamente una instancia.
2. Segundo disparador durante un RADAR sano → no duplicar ni matar proceso.
3. Login/reanudación con bandera → cero inicios.
4. Parada manual durante supervisión → nunca resucitar.
5. Caída Listener → un reinicio controlado, backoff probado.
6. Caída Chromium → recuperar sin perder sesión; si Listener no recupera y árbol sigue ausente, reiniciar bajo límites.
7. WhatsApp WaitingForReady prolongado → **cero reinicios**, telemetría degradada pero viva.
8. Corte internet/CENTRAL → sin restart por conectividad externa.
9. Suspensión y retorno con sesión bloqueada → no intentar Chromium en sesión no interactiva.
10. Tres fallos repetidos → circuit breaker, sin loop.
11. Procesos Chrome/Node personales presentes → ninguno modificado.
12. Restauración tarea original y ausencia de duplicados, con hashes/backups y perfil intacto.

## Condiciones de despliegue

**Primero** cerrar y validar `RADAR_REMOTE_HEALTH_01`; luego comenzar este hito en su propia rama. Predeploy: inventario exacto de Task Scheduler, usuarios, triggers, árbol de procesos, scripts y ruta real de `WhatsAppProfile`. Backup de XML de tarea y scripts antes de cualquier cambio; no realizar cambios si falta rollback demostrable. Despliegue durante sesión interactiva con observación suficiente.

## Alcance y límites

- Ninguna acción de este diseño está ejecutada.
- `Run only when user is logged on` es un requisito operativo probable para Chromium visible, **no una configuración productiva ya verificada**.
- El semáforo de Remote Health debe distinguir "Sin comunicación" de "equipo apagado"; no es posible deducir la causa de un heartbeat ausente.
- Un cierre con la X de Chromium no implica parada voluntaria segura: usar explícitamente `Detener RADAR`.
- Nunca publicar `manual-stop.flag` ni detalles locales de sesión por la API pública.

**Próximo paso:** auditar configuración real de Task Scheduler desde Windows cuando Codex vuelva a estar disponible, y ajustar este diseño según evidencia.
