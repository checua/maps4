# RADAR — Hito 5L.2: dimensionamiento del almacenamiento cifrado

2026-11-06, DESKTOP-E32803V. Rama diagnostic/radar-web-sql-identity.
Base de trabajo 0efc240642ff1491e789d4b13b09e063a4fba485.
READY_TO_DEPLOY=NO. CAPTURE=BLOCKED_BEFORE_DOWNLOAD.

## Medición observada

Consulta ARM de metadatos del App Service rsmap para localizar SCM, seguida de GET
Kudu VFS de directorios bajo site/wwwroot. Sólo listados: nombres, tipo y tamaño;
ningún GET de contenido de archivos, configuraciones efectivas, ZIP ni consultas SQL.
Recorrido concluido a 2026-11-06T20:43:43Z: 45 directorios consultados, sin errores,
347 archivos, 31,150,541 bytes (29.7075 MiB). Inventario detallado sólo en memoria;
se publican exclusivamente agregados. No se conservaron tokens ni listas privadas.

| Categoría | Archivos | Bytes |
| --- | ---: | ---: |
| Ensamblado y ejecutable de aplicación | 2 | 1,195,520 |
| Dependencias en raíz | 24 | 6,433,760 |
| Dependencias por plataforma (runtimes) | 5 | 5,250,048 |
| Recursos de idiomas | 13 | 3,479,856 |
| Estáticos (wwwroot) | 293 | 13,796,446 |
| Configuración (sólo tamaño) | 4 | 2,329 |
| Metadatos runtime/dependencias/staticwebassets | 3 | 688,087 |
| Recurso de conocimiento de aplicación | 1 | 1,775 |
| Símbolos de depuración, incluidos conservadoramente | 2 | 302,720 |
| **Total candidato de captura** | **347** | **31,150,541** |

La clasificación usa rutas/extensiones, no inspección del contenido remoto.
Se retienen dependencias de todas las plataformas para evitar una poda innecesaria.
Se excluyó íntegramente publish-rsmaps: copia histórica separada descrita en 5K,
no parte del árbol candidato de ejecución. No se recorrió ni se sumó su contenido.
No aparecieron directorios cargas/uploads, logs, temporales o backups en el árbol
recorrido. La política del recorrido los excluye si aparecen; no se recorrieron
LogFiles, data, backups ni otras raíces de App Service.
El código local identifica wwwroot/cargas como destino de cargas, y el proyecto
lo excluye de publicación; esa carpeta no apareció en el listado productivo.
Esta observación local ayuda a clasificar, pero NO acredita igualdad de fuente con
producción ni ausencia de datos sensibles dentro de archivos aparentemente estáticos.

## Presupuesto y capacidad

No se cuenta con ahorro por compresión. MiB = 1,048,576 bytes; GiB = 1,073,741,824 bytes.

| Uso simultáneo | Reserva MiB |
| --- | ---: |
| Captura original | 29.71 |
| ZIP: tamaño original más 2% de overhead | 30.30 |
| Extracción temporal para verificar recuperación | 29.71 |
| Manifiestos, hashes, recibos y temporales adicionales | 16.00 |
| Subtotal | 105.72 |
| Subtotal con margen del 25% | 132.15 |

Recomendación: **VHDX fijo de 2 GiB**, volumen NTFS de datos, cifrado con BitLocker
antes de recibir contenido. Es la menor de las capacidades preferidas (2/4/8 GiB).
El mínimo técnico de este presupuesto sería menor: 256 MiB podría bastar para estos
bytes; 2 GiB evita operar cerca de límites y reserva espacio para comprobaciones y
un futuro paquete diagnóstico acotado, cuyo tamaño aún no se midió. No se justifican
4 u 8 GiB para la captura actual. Si se incorpora SDK, fuente, build, múltiples
paquetes o contenido adicional, volver a medir; este presupuesto no los incluye.
Reservar al menos 256 MiB utilizables dentro del volumen para este flujo y detener
la captura si el inventario cambia o el presupuesto disponible resulta insuficiente.

C: tiene 479,986,917,376 bytes libres (~447 GiB), medidos mediante DriveInfo.
No es una prueba de cifrado. El propietario informó C: FullyDecrypted,
ProtectionStatus Off, EncryptionPercentage 0, EncryptionMethod None en 5L.1.
Esta evidencia posterior sustituye la incertidumbre histórica de 5L sobre C:.

## Fijo frente a dinámico y tiempos

Fijo reserva aproximadamente 2 GiB más metadatos desde el principio; evita depender
del crecimiento posterior del archivo. Dinámico empieza pequeño y crece al escribir;
su ventaja de espacio puede perderse si se cifra todo el volumen. No aporta una
ventaja relevante aquí con ~447 GiB libres. Ninguno es cifrado por ser VHDX.
Microsoft describe ambas modalidades en [Manage Virtual Hard Disks](https://learn.microsoft.com/en-us/windows-server/storage/disk-management/manage-virtual-hard-disks)
y [VHDX overview](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-vhdx/83f6b700-6216-40f0-aa99-9fcb421206e2).
Es una unidad de datos montada, no arranque nativo desde VHDX.

No se ejecutó benchmark ni creación/cifrado. Estimación de planificación, suponiendo
20–100 MiB/s efectivos: escribir 2 GiB requiere aproximadamente 20–103 segundos por
pasada. Creación fija ~0.5–3 minutos; cifrado completo ~1–5 minutos o más según CPU,
E/S y políticas. Reservar 5–15 minutos para preparación, protectores y verificaciones;
no es una garantía de rendimiento. Dinámico puede crearse en segundos, pero no
elimina el tiempo de cifrado ni la verificación de protección activa.

## Seguridad y límites pendientes

- No se creó VHDX, volumen, carpeta privada ni ZIP; no se modificaron ACL ni BitLocker.
- Ubicación propuesta: %LOCALAPPDATA%\RSMapsPrivate\5L\, fuera de OneDrive y Git;
  verificar rutas resueltas, reparse points y exclusión de copias/sincronización antes
  de crear. Sólo el contenedor cifrado se almacena en C:, nunca su contenido suelto.
- Crear/montar/formatear únicamente el nuevo disco virtual, establecer ACL privadas,
  activar BitLocker y custodiar recuperación fuera del contenedor requieren
  autorización específica y una sesión Windows administradora. No tocar particiones
  existentes, no desbloqueo automático. Verificar cifrado completo y protección On.
- Captura, ZIP, extracción, manifiestos y TEMP/TMP del proceso deben quedar dentro
  del volumen cifrado. Descargar directamente allí, sin intermediarios en C:.
  Bloquear y desmontar al finalizar, con todos los archivos cerrados.
- C: descifrado conserva riesgo residual de pagefile, hibernación y volcados, incluso
  con TEMP/TMP privados. El contenedor no garantiza ausencia de residuos del SO;
  resolver/aceptar explícitamente ese límite antes de material sensible. No se cambió
  configuración compartida del sistema para mitigarlo.
- El listado no es snapshot atómico, no calcula hashes ni acredita enlaces/ACL,
  integridad, completitud del rollback o correspondencia con un commit. Revalidar
  inventario, clasificación de archivos ambiguos y estabilidad al recuperar.
- Configuración externa de App Service, certificados, claves y runtime instalado
  no se incluyen como archivos de este paquete; su recuperación sigue en el plan 5K.
  No se reconstruye ni se declara desplegable el paquete productivo.

## Validación y continuación

Start pasó con árbol limpio y sincronizado. Sumas por categorías y presupuesto
verificados; revisión de diff, git diff --check y escaneo de secretos dedicados antes
de publicar. El gate heurístico global Secrets informa 22 hallazgos heredados en archivos de
código/configuración/librerías que no se modificaron respecto de origin; no se
declara aprobado ese gate global. Gitleaks sobre el documento nuevo y los diffs
documentales no detectó secretos. Se revisa también el commit antes del push.
Sólo documentación: no se requieren builds ni regresiones de código.
La siguiente autorización debe cubrir la creación/preparación del contenedor de
2 GiB y sus controles; este hito no autoriza ejecutarla. Después acreditar condiciones
de almacenamiento del 5L antes de utilizar la autorización de recuperación privada.
READY_TO_DEPLOY=NO.
