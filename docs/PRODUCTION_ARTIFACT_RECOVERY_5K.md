# RADAR — Hito 5K: procedencia productiva y propuesta de rollback privado

2026-11-06, DESKTOP-E32803V. Rama diagnostic/radar-web-sql-identity.
Base d1c5c60255a8c3d72e72e5dea5ec8cc39976678c.
PRODUCTION_VERSION=INCONCLUSIVE. ORIGINAL_PACKAGE=NOT_AVAILABLE_IN_INSPECTED_LOCATIONS.
PRODUCTION_PACKAGE_READY=NO. READY_TO_DEPLOY=NO.

## Método y alcance

Lecturas ARM, API de metadatos deployment/log de Kudu, listados VFS de directorios
y HEAD de maps4.dll. Se emitieron sólo campos permitidos, nunca logs crudos,
configuraciones completas, secretos, URLs de paquetes o perfiles de publicación.
HEAD confirmó cero bytes de cuerpo descargados. No GET de DLL, JSON de aplicación,
web.config, PDB, ZIP ni manifiesto como archivo completo. No comandos remotos,
descargas privadas, SQL, cambios de configuración o reinicios.
La técnica nueva frente a 5G.1 fue inventario de paquetes/metadatos y HEAD, no repetir
la descarga del DLL que falló. Consulta de configuración sólo en memoria y booleanos.

## Resultados clasificados

| Comprobación | Clasificación | Evidencia |
| --- | --- | --- |
| Deployment marcado activo | CONFIRMADO | 9127bde49b9e469d9d5231c09b2b40df; active=true; status=4 |
| Método registrado | CONFIRMADO | deployer=OneDeploy; no prueba del formato exacto del paquete ni de todas las escrituras posteriores |
| Fecha registrada | CONFIRMADO | Recepción 2026-09-24T04:17:12.3506629Z; inicio 04:17:12.4444066Z; fin 04:17:19.6326076Z |
| Historial consultado | CONFIRMADO | Cuatro registros; resumen de log activo con 13 entradas, sin candidato hexadecimal de 40 caracteres ni build_summary |
| Commit de origen | INCONCLUSO | ID activo tiene 32 caracteres y no es SHA Git completo; sin referencia de commit acreditada |
| Fuente ARM | NO DISPONIBLE | scmType=None; sin repoUrl/branch configurados en la consulta. No descarta publicación externa desde Git |
| Versión binaria actual | INCONCLUSO | Sin AssemblyInformationalVersion, SourceLink o SHA-256 del contenido; no se descargaron bytes |
| maps4.dll actual | CONFIRMADO, metadatos | 1,033,216 bytes; HEAD 200; Last-Modified 2026-09-28T03:01:04Z; ETag presente, sin cabecera SHA-256 |
| web.config actual | CONFIRMADO, metadatos | 549 bytes; mtime convertido a UTC 2026-09-29T00:09:11.5693383Z |
| Correspondencia deployment/contenido | INCONCLUSO | DLL y web.config tienen fechas posteriores al deployment activo; no se puede asociar ese registro al contenido actual sin hashes y procedencia |
| Run-from-package | NO DISPONIBLE en ajustes consultados | WEBSITE_RUN_FROM_PACKAGE ausente; no se habilitó |
| ZIP original en ubicaciones inspeccionadas | NO DISPONIBLE | data enumera aspnet, kudusync y monaco, sin SitePackages; no ZIP en raíz wwwroot listada |
| Archivo manifest del deployment | CONFIRMADO, existencia solamente | 17 bytes; no leído. No se interpreta como inventario completo ni hash |
| Copia publish-rsmaps | INCONCLUSO como rollback | Listado contiene deps/runtimeconfig/staticwebassets/web.config antiguos, sin maps4.dll en su raíz; no se asume publicación íntegra |
| Artefactos locales versionados/raíz | NO DISPONIBLE | Sin ZIP/perfil pubxml/workflow/manifiesto coincidente en la búsqueda acotada; no se inspeccionó otra computadora |
| Hash original SHA-256 | NO DISPONIBLE | ETag/tamaño/fecha no son SHA-256; no se fabricó hash de un listado ni se usó el del prototipo |

Las fechas de archivo no prueban por sí solas modificación de contenido o identidad
de quien publicó. Sí impiden tratar el deployment activo como identificación suficiente.
La ausencia de ZIP en esos directorios no acredita ausencia en todo Azure, almacenamiento
externo, máquina publicadora o sistema CI. No se buscaron indiscriminadamente otros recursos.
No se ha acreditado una versión semántica, un commit ni paquete original recuperable.

## TLS y ACL

CONFIRMADO sobre la entrada ARM cadenaSQL: TrustServerCertificate=true; Encrypt no
está explícito. La lectura se hizo con System.Data.SqlClient sólo como analizador,
cuyo valor predeterminado no acredita el del Microsoft.Data.SqlClient desplegado.
No concluir Encrypt=false ni ausencia de cifrado en el Web a partir de ese analizador.
La versión del driver efectivamente publicada todavía no se verificó.

El prototipo 5J usa Microsoft.Data.SqlClient 7.0.2 y rechaza TrustServerCertificate=true
o Encrypt=Optional sin cambiar la cadena. Si esa entrada es la ganadora efectiva,
el diagnóstico se detendrá ANTES de abrir SQL. La conexión de oficina 5H.3, con TLS
forzado y certificado validado, no prueba el resultado desde el worker. No se propone
relajar la guardia ni cambiar opciones silenciosamente para conseguir una lectura.
Un cambio productivo a TLS requeriría aprobación separada, prueba de cadena/nombre de
certificado y ruta de rollback de configuración; tampoco debe confundirse con demostrar
cómo se comportaba la conexión anterior. Encrypt/Trust tienen semántica dependiente del
modo y versión; revisar el driver real antes de decidir el ajuste.

ACL: INCONCLUSO. VFS/HEAD no acreditan las ACL NTFS ni que la futura carpeta privada
cumpla el allowlist de 5J (identidad del worker, SYSTEM y Administradores). No ejecutar
Set-Acl ni ampliar SCM/RBAC. Deben leerse ACL y verificar exclusión de ContentRoot/WebRoot
y mapeos estáticos antes de activar. Un rechazo de ACL del prototipo es seguro y no
se resuelve automáticamente concediendo acceso.

## Recomendación concreta: capturar privadamente el estado actual sin recompilar

Solicitar autorización limitada de recuperación privada, distinta de despliegue.
Dado que no hay paquete original acreditado, el resultado inicialmente será una
captura de los archivos desplegados, NO el ZIP original ni un commit reconstruido.
Buscar el paquete original en el origen de publicación sólo mediante su propietario,
si puede aportar trazabilidad verificable; no condicionar toda recuperación a hallarlo.

1. Acordar una ventana sin publicaciones, edición de archivos ni cambios de configuración.
   No requiere parar el Web. Registrar instante, deployment y metadatos previos. Si no
   puede asegurarse estabilidad, no declarar snapshot íntegro; una captura no atómica
   podría mezclar versiones aunque el sitio continúe Online.
2. Inventariar recursivamente sólo la raíz real del Web y rutas de runtime necesarias.
   Clasificar antes de descargar: ensamblados/dependencias, runtimes nativos, ejecutable,
   deps/runtimeconfig, web.config, vistas precompiladas o físicas requeridas, estáticos y
   archivos de contenido usados. No dar por suficiente maps4.dll o publish-rsmaps.
3. Segregar contenido mutable/privado (cargas, imágenes de usuarios, logs, temporales,
   claves de DataProtection y backups): no descargarlo ni sobrescribirlo salvo alcance
   explícito distinto. La configuración local requerida puede contener secretos y
   necesita consentimiento de recuperación privada; no omitirla silenciosamente de un
   paquete que se denomine íntegro. Mantener los valores ARM fuera del ZIP de código.
4. Obtener SHA-256 servidor por archivo mediante comando de sólo lectura aprobado,
   usando rutas resueltas dentro del árbol autorizado y sin seguir enlaces a otros
   destinos. Emitir sólo ruta relativa, tamaño y hash; inventario privado. Sin instalar
   herramientas ni subir binarios. Es una operación adicional no ejecutada en 5K.
5. Descargar únicamente la lista aprobada por canal Kudu autenticado/TLS hacia una
   carpeta fuera de repo y OneDrive, en volumen cifrado verificado y ACL restringidas.
   No usar ZIP de toda wwwroot por comodidad: podría incluir datos personales/cargas.
   No publicar perfiles, SAS, tokens ni cadenas. Credencial de acceso sólo en memoria.
6. Verificar hashes locales contra manifiesto servidor y volver a verificar inventario,
   hashes y deployment al terminar. Si cambian, captura INCONCLUSA; no reintentar
   automáticamente ni mezclar archivos de capturas diferentes.
7. Crear ZIP privado de esa captura estable, calcular SHA-256 del ZIP y conservar
   manifiesto por archivo, fecha y procedencia. Get-FileHash -Algorithm SHA256 se
   ejecutará sobre esos archivos privados aprobados; no hay hash verificable hasta
   leer los bytes. Un hash local aislado acredita integridad posterior, no procedencia.
8. Sin ejecutar DLL desconocidos, inspeccionar metadatos PE, InformationalVersion,
   deps/runtimeconfig y PDB/SourceLink si disponibles y autorizados. Contrastar un SHA
   candidato con Git y con pipeline/artefacto original; una cadena de versión o fecha
   sola no acredita que la fuente corresponda a todos los bytes del paquete.

## Rollback íntegro: qué debe preservarse y cómo probarlo

Preservar por separado: snapshot de código/contenido de runtime; configuración de
hosting (web.config, runtime/bitness y rutas); inventario de App Settings y conexiones,
slot flags, referencias Key Vault, identidad asignada y accesos existentes. Los valores
sensibles se conservan sólo por mecanismo privado autorizado si fuera necesario; no se
reaplican masivamente al hacer rollback del código. SQL, datos de usuarios, almacenamiento
externo y DataProtection no forman parte de un rollback binario y deben preservarse.
No cambiar WEBSITE_RUN_FROM_PACKAGE como parte de obtener el respaldo.

Primero validar offline inventario, tamaños y hashes sin ejecutar el paquete contra
servicios reales. Para ensayo dinámico se requiere copia de trabajo aislada con secretos
reemplazados y salida de red bloqueada; nunca iniciar un Web recuperado con sus conexiones
productivas. Comparar comportamiento original/diagnóstico/retirada con datos sintéticos.
La copia maestra privada permanece intacta. No instalar componentes compartidos sin
revisar impacto y autorización. El ensayo de patch/reverse de 5J no sustituye este ensayo.

Después de acreditar fuente y snapshot: portar sólo el diagnóstico a checkout de esa
versión, reconciliar repositorios/proveedores y driver real, repetir pruebas/escaneos,
producir paquetes original y diagnóstico con manifiestos/hashes. El prototipo sobre
poison no se despliega. Si la fuente no puede recuperarse, no presentar una recompilación
aproximada como parche mínimo; detener la preparación de despliegue y definir otro alcance.

Rollback remoto futuro, con autorización independiente y mecanismo previamente probado:
desarmar manifiesto exacto, esperar/cancelar tarea acotada, restaurar archivos del original
con política explícita para retirar sólo los añadidos por el parche (no barrer cargas),
conservar configuración/datos ajenos al parche y verificar hashes, salud y conexión.
Posibles bloqueos de archivos/recycle/ventana de mantenimiento se aprueban previamente.
No usar redeploy del registro 9127... como sustituto del snapshot: no corresponde de
forma acreditada al árbol actual. No ejecutar cambios en SQL ni SQL 57/58.

## Autorizaciones adicionales exactas

A. Recuperación privada: destino propuesto C:\Users\tommy\AppData\Local\RSMapsPrivate\5K\{runId}, fuera de Git/OneDrive, aún no creado; verificar cifrado antes de usarlo.
   Aprobar ese directorio cifrado con ACL, inventario remoto
   acotado, comandos remotos de lectura para SHA-256/ACL y descarga de archivos runtime
   aprobados. Incluir explícitamente web.config/appsettings si contienen secretos,
   PDB si se necesita procedencia, tratamiento privado y plazo de conservación definido
   antes de descargar (propuesta: hasta cierre del diagnóstico más 7 días, con revisión).
   No autoriza configuración, instalación, reinicio ni despliegue. Si no se permite
   recuperar configuración necesaria, registrar respaldo incompleto y detener despliegue.
B. Investigación del origen: propietario identifica máquina/CI publicador y entrega
   paquete/commit/manifiesto por canal privado; acceso a almacenamiento externo sólo
   con alcance nuevo específico. No buscar credenciales ni archivos de otras cuentas.
C. TLS/ACL: sólo si se demuestra necesario, solicitud separada con ajuste exacto,
   efecto, prueba y rollback; NO incluida en A ni B. No se solicita una concesión amplia.
D. Despliegue diagnóstico posterior: aprobación sobre hashes revisados del parche y
   rollback, ventana/recycle, activación de una ejecución por instancia, SELECT privado,
   retirada y retención de evidencia. Fuera del alcance actual.

El próximo paso recomendado es A, una vez concretados destino protegido, lista y plazo;
no solicitar todavía permiso para desplegar un paquete desconocido. Mientras tanto se
puede continuar revisión/pruebas locales del prototipo. READY_TO_DEPLOY=NO.

## Validación de este hito y fuentes

Start Git aprobado, revisión documental/diff, escaneo dedicado de secretos y Prepare
antes de cierre. Sólo documentación publicada; sin cambios al prototipo o dependencias,
por lo que no se repiten regresiones. Token y conexiones temporales limpiados en finally.

- https://learn.microsoft.com/en-us/azure/app-service/deploy-run-package
- https://learn.microsoft.com/en-us/azure/app-service/deploy-zip
- https://github.com/projectkudu/kudu/wiki/MSDeploy-VS.-ZipDeploy
- https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation
