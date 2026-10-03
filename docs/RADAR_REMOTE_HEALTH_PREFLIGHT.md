# RADAR_REMOTE_HEALTH_01-F — Preflight verificable y retorno a versión estable

**Tipo:** inventario y plan de preparación. **NO AUTORIZA DESPLIEGUE**; no ejecutar escrituras sobre SQL productivo, Web App, RADAR ni Task Scheduler durante este bloque.

## 0. Referencias verificadas en GitHub (no equivalen a versión instalada)

| Referente | SHA exacto | Evidencia |
|---|---|---|
| Fuente Listener estable de referencia, último reporte operativo | `b08de0a00d8ff59fb3c13e5787d984b41cca75cc` | Commit presente: `fix: recover WhatsApp navigation timeouts` |
| Candidato validado en desarrollo | `a07c61baf388fc725ed40de2be1482db57829609` | `feature/radar-remote-health`, incluye las correcciones integradas |
| Relación Git | Candidato 17 commits delante de `b08de0a`, 0 detrás | Compare GitHub |
| Master en la revisión | Candidato 20 commits delante de master, 0 detrás | NO se fusionó ni modificó master |
| Script SQL | `sql/RSMaps2/57_radar_agent_health.sql` | Script aditivo, guard de base `mapsMarkers` y precisión `datetime2(7)` |

**Limitación crítica:** saber qué commit se informó como operativo NO demuestra los hashes ni la versión realmente instalada en la laptop, ni el paquete Web vigente en Azure. **No construir el rollback desde master ni asumir que b08de0a es la versión exacta de todo el Web App.** El punto de restauración válido es el conjunto de binarios/configuración realmente instalado y comprobado inmediatamente antes del cambio.

### Evidencia técnica ya disponible

- Build Web, Listener y Lab y regresiones aisladas: [GitHub Actions 37094879023](https://github.com/checua/maps4/actions/runs/37094879023) (success).
- SQL Server 2022 temporal, instalación, idempotencia, concurrencia, reinicio/replay y rollback: [GitHub Actions 37094879006](https://github.com/checua/maps4/actions/runs/37094879006) (success).
- No reemplazan pruebas de compatibilidad con SQL productivo, ni los backups y restauraciones reales.

## 1. Condiciones de inicio — completar ANTES de desplegar

| Gate | Comprobación / evidencia requerida | Estado |
|---|---|---|
| G1 — Listener productivo | Confirmar tarea `RSMaps RADAR Produccion`, sesión interactiva, un único Listener/Chromium, WhatsApp Ready y al menos un sweep real; guardar evidencia sin chats ni tokens | **PENDIENTE EN LAPTOP** |
| G2 — Backup Listener | Capturar copia íntegra recuperable de todos los binarios y dependencias de la instalación actual fuera de su directorio; comparar nombres/hashes SHA-256; conservar launcher y configuración mediante procedimiento protegido. No copiar/alterar el perfil de WhatsApp | **PENDIENTE EN LAPTOP** |
| G3 — Credenciales y WhatsApp | Confirmar `WhatsAppProfile` intacto y cuenta Windows original disponible, sin exportar DPAPI ni secretos. La recuperación se realizará con ese mismo usuario/host | **PENDIENTE EN LAPTOP** |
| G4 — Backup Web | Obtener paquete real desplegado y procedimiento comprobable de restauración de Web App (slot/snapshot/artefacto), con hash y preservación de App Settings/connection strings | **PENDIENTE EN AZURE** |
| G5 — SQL producción | Confirmar `mapsMarkers`, instancia/proveedor exactos, esquema `RSMAPS_RadarAgentDevice`, backup/restore/PITR y permisos; verificar script 57 contra objetos reales por lectura | **PENDIENTE EN AZURE/SQL** |
| G6 — No mezcla de artefactos | Ambos paquetes nuevos Web/Listener del **mismo** SHA bloqueado `a07c61b...`, Release .NET 10; manifiesto SHA-256 por paquete y exclusión de secretos, perfiles y datos locales | **PENDIENTE** |
| G7 — Condiciones de recuperación | Disponer de acceso y tiempo para recuperar Web y Listener; responsables y ubicación de respaldos conocidos, sin imprimir ni guardar credenciales en repositorio | **PENDIENTE** |
| G8 — Aprobación | Autorización expresa para SQL productivo y publicación por fases, con ventana y observación; ninguna prueba en CI la sustituye | **PENDIENTE** |

**Política:** un solo gate en PENDIENTE o FALLIDO significa **NO DEPLOY**. Sólo después de demostrar G1–G8 se realiza el siguiente bloque operativo, con parada ante error en cada fase.

## 2. Auditoría local no destructiva desde Windows

Herramienta preparada: `tools/RadarRemoteHealthPreflight.ps1`. **Sólo lee**, no exporta contraseñas, no crea backups, no inicia/detiene tareas ni realiza publicaciones.

Ejecutar únicamente **después** de obtener un respaldo en una ubicación segura y un paquete de reversión Web:

```powershell
pwsh -NoProfile -File .\tools\RadarRemoteHealthPreflight.ps1 `
  -ListenerDirectory "<RUTA_AGENT_PRODUCTIVO_APP>" `
  -ListenerRollbackDirectory "<RUTA_BACKUP_BINARIOS_EXISTENTE>" `
  -WebRollbackPackage "<PAQUETE_WEB_PREVIO_EXISTENTE>" 
```

Verificar salida `LOCAL_PREFLIGHT_OK`. Ese indicador demuestra correspondencia binaria básica, existencia de paquete Web y tarea: **no prueba que un restore haya funcionado ni que el SQL productivo sea correcto**. Mantener evidencia local privada; no subir rutas, nombres de usuario, hashes de archivos sensibles ni archivos de configuración a GitHub.

## 3. Publicación autorizada futura (NO EJECUTAR DURANTE PREFLIGHT)

1. Gate check completo G1–G8 y copia íntegra preservada. Paquetes firmados/manifiesto hash y respaldos con hora UTC.
2. Aplicar **sólo** script 57 en `mapsMarkers` productivo con backup/PITR verificado. Confirmar transacción, FK, checks y que no altera tablas existentes. **STOP** si cambia algo imprevisto.
3. Publicar **sólo Web** desde SHA bloqueado; probar funcionalidad existente, autenticación propia, dueño/tenant y API con credencial Agent de prueba autorizada. **STOP** y restaurar Web si hay regresión. Listener anterior permanece operativo.
4. Detener `RSMaps RADAR Produccion` mediante procedimiento controlado y registrar estado/colas; respaldar/verificar artefactos instalados; sustituir **únicamente binarios** necesarios de Listener. No copiar/borrar `WhatsAppProfile`, DPAPI, config, launcher, ni reiniciar otras instancias.
5. Iniciar tarea controladamente; confirmar una instancia, WhatsApp `Ready` (sin QR), **tres heartbeats aceptados** con `instanceId` constante y `sequence` creciente (~60 s).
6. Confirmar barrido real posterior al inicio; chats configurados/revisados completos; CENTRAL y Delivery operativos. Probar panel desde PC/iPhone con usuario `PROPIETARIO` y cuenta correcta, así como rechazo de otro tenant.
7. Cerrar únicamente con evidencia. Ausencia de heartbeat no identifica si PC apagada, sin red o RADAR detenido; reportar `Sin comunicación` sin inventar causa.

## 4. Rollback por componente (NO EJECUTAR SIN FALLA Y AUTORIZACIÓN)

- **Si falla SQL antes de publicación:** no desplegar más; dejar nueva tabla **inerte**, salvo que una orden SQL separada y justificada autorice cualquier retiro. No borrar/alterar otras tablas.
- **Si falla Web:** restaurar paquete exacto anterior y verificar las funciones anteriores; no modificar Listener si aún no se ha desplegado.
- **Si falla Listener o WhatsApp:** detener tarea **una vez**, restaurar exclusivamente binarios/dependencias respaldados y volver a arrancar con el usuario habitual; no mover ni borrar perfil, token DPAPI, configuración ni sesiones. Revisar un solo proceso y barridos.
- **Si Web y Listener fallan:** recuperar ambos por separado. Mantener tabla nueva si es inerte; no revertir SQL de manera destructiva.
- **Si se pierde acceso o falla la restauración:** detener nuevas acciones y conservar evidencias; no iniciar loops ni reintentos agresivos. Resolver manualmente con acceso local a los respaldos.

**Criterio de éxito de rollback:** la versión anterior vuelve a revisar chats, procesar CENTRAL y Delivery con un único Listener, sin requerir escanear QR ni duplicar envíos; verificarse con observación real. No dar por exitoso sólo que el proceso aparezca abierto.

## 5. Evidencia de cierre de F

```text
PROMPT_ID: RADAR_REMOTE_HEALTH_01-F
SOURCE_STABLE_COMMIT: b08de0a00d8ff59fb3c13e5787d984b41cca75cc
CANDIDATE_COMMIT: a07c61baf388fc725ed40de2be1482db57829609
GITHUB_BUILD_CI: PASS
GITHUB_SQL_TEST: PASS
REAL_LISTENER_INSTALLED_HASH: NO VERIFICADO
REAL_WEB_DEPLOYED_HASH: NO VERIFICADO
LISTENER_BACKUP_CREATED_AND_CHECKED: NO
WEB_ROLLBACK_PACKAGE_VERIFIED: NO
PRODUCTION_SQL_BACKUP_AND_SCHEMA_VERIFIED: NO
ROLLBACK_REHEARSAL: NO
PRODUCTION_MODIFIED: NO
DEPLOY_AUTHORIZED: NO
READY_TO_DEPLOY: NO
```

**Siguiente paso:** completar gates de entorno real, registrar hashes privados y obtener autorización de ejecución. No usar "READY_TO_DEPLOY: SÍ" por tener únicamente CI aprobada.
