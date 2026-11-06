> Actualización posterior 5L.1/5L.2 (2026-11-06): el propietario acreditó C:
> FullyDecrypted / ProtectionStatus Off / 0% / EncryptionMethod None. La
> incertidumbre de este informe histórico ya no es el estado vigente de C:.
> ENCRYPTED_STORAGE_SIZING_5L2.md mide 29.71 MiB de archivos candidatos y recomienda
> VHDX fijo de 2 GiB, pendiente de autorización/preparación y controles. No hubo
> descargas privadas. READY_TO_DEPLOY=NO.

# RADAR — Hito 5L: recuperación detenida antes de descargar

2026-11-06, DESKTOP-E32803V. Rama diagnostic/radar-web-sql-identity.
Base ae120e1bc211a35cc0a660e4f28a3e146395a3ec.
CAPTURE=BLOCKED_BEFORE_DOWNLOAD. ENCRYPTION_VERIFIED=NO.
PRODUCTION_PACKAGE_READY=NO. READY_TO_DEPLOY=NO.

## Autorización recibida

El propietario autorizó inventario runtime, SHA-256 remoto de lectura, consulta de
ACL/metadatos y descarga privada acotada, incluidos web.config y configuraciones
indispensables sensibles. Destino exigido: una carpeta única bajo
C:\Users\tommy\AppData\Local\RSMapsPrivate\5L\.
No autoriza escrituras productivas, despliegues, reinicios ni cambios Azure/SQL.
La fase 1 exige acreditar cifrado activo, ACL y almacenamiento privado antes de
cualquier descarga y ordena detenerse si no puede hacerse.

## Evidencia nueva de fase 1

- Equipo DESKTOP-E32803V y rama esperada confirmados.
- Start aprobado: árbol limpio y HEAD igual a origin antes de documentar.
- Se revisaron AGENTS, DEV_SYNC, DEV_HANDOFF y propuesta 5K.
- Get-BitLockerVolume está disponible, pero consultar C: falló con CimException.
- Una comprobación fuera del sandbox mantuvo el fallo; la salida de
  manage-bde -status C: indicó acceso denegado y necesidad de derechos administrativos.
- El token actual no pertenece al rol administrador elevado (comprobación false).
- No se consultaron protectores, recovery keys, tokens ni credenciales.

INCONCLUSO: estado real de cifrado, protección y porcentaje del volumen C:.
El fallo de permisos NO prueba que el disco esté descifrado. Tampoco se acredita
cifrado por el nombre de carpeta, por estar fuera de OneDrive o por permisos NTFS.
Elevar el permiso de sandbox no convirtió la sesión Windows en administrador.

Se detuvo antes de crear la carpeta de captura, inventariar Azure, calcular hashes
remotos o descargar. Las ACL de una futura carpeta y su exclusión efectiva de
sincronización/Git siguen pendientes; no se declaró que pasaran. No hubo UAC,
instalaciones, activación/suspensión de BitLocker ni cambios de seguridad del sistema.

## Estado de la entrega

| Elemento | Resultado |
| --- | --- |
| Captura | BLOQUEADA en fase 1 |
| Archivos runtime recuperados | 0 |
| Tamaño recuperado | 0 bytes |
| Hashes servidor/local | NO EJECUTADOS |
| ZIP privado / SHA-256 | NO CREADO / NO DISPONIBLE |
| Cobertura de rollback | NO ACREDITADA; no hay paquete nuevo |
| Versión binaria/commit actual | INCONCLUSO; se conserva evidencia de 5K, sin inspección nueva |
| Compatibilidad diagnóstico | Pendiente de recuperar versión real; pruebas locales 5J no la acreditan |
| Azure/SQL productivo | Sin acceso nuevo ni cambios en este hito |

## Próximo paso exacto

El propietario/administrador local debe facilitar una consulta de estado en una
sesión Windows elevada, exclusivamente de lectura, sobre esta computadora y C:.
No necesita revelar claves de recuperación. Comando sugerido:

```powershell
hostname
Get-Date -AsUTC -Format o
Get-BitLockerVolume -MountPoint C: |
  Select-Object MountPoint,VolumeStatus,ProtectionStatus,EncryptionPercentage,EncryptionMethod
```

Alternativa de estado: manage-bde -status C:, también en sesión elevada.
Acreditar cifrado completo y protección activa (por ejemplo FullyEncrypted,
EncryptionPercentage=100 y ProtectionStatus=On); si hay suspensión o estado parcial,
no continuar con una inferencia de seguridad. Si la organización usa otro mecanismo,
se requiere evidencia equivalente de cifrado activo del volumen, no EFS supuesto.
Esta consulta no requiere autorizar de nuevo la descarga ya permitida. Falta la
capacidad/evidencia local; no se solicita permiso para modificar BitLocker.

Una vez acreditado el volumen, crear carpeta única bajo la raíz 5L autorizada,
provisionar/verificar ACL limitadas al usuario, SYSTEM y administradores necesarios,
comprobar rutas resueltas/reparse points, ausencia de sincronización OneDrive y de
Git y registrar evidencia no sensible. Sólo entonces seguir inventario/clasificación,
hashes remotos, descarga de lista aprobada, verificación posterior y ZIP privado.
La autorización no elimina la obligación de detenerse ante cambio concurrente o
límites ambiguos del árbol. No descargar temporalmente a otra carpeta para eludir
la condición ni publicar configuraciones o manifiestos sensibles.

Si se requiere activar cifrado o cambiar la seguridad compartida del sistema, presentar
impacto y solicitar autorización específica antes de hacerlo. La autorización 5L de
recuperación no autoriza despliegue diagnóstico, ajustes TLS, cambios productivos de
ACL, SQL 57/58, Listener, reinicios o integración de ramas.

## Validación documental

Sólo documentación: revisión de diff, git diff --check, escaneo dedicado de secretos
y sincronización Git. No corresponde repetir compilaciones/regresiones de código.
Los documentos no contienen archivos recuperados, secretos ni evidencia privada.
READY_TO_DEPLOY=NO.
