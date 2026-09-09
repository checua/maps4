# RSMaps — Azure Blob como fuente única de fotografías

## Decisión arquitectónica

Las copias locales de RSMaps (casa/oficina/worktrees) ejecutan código, pero **no son la fuente de verdad de las fotografías**.

Arquitectura objetivo:

- Código: Git/GitHub
- Datos y metadata: Azure SQL (`mapsMarkers`)
- Fotografías: Azure Blob Storage (`rsmap-images`)
- Aplicación productiva: Azure App Service (`rsmap.azurewebsites.net`)
- Casa/oficina: localhost sólo como host de desarrollo, leyendo/escribiendo contra los servicios centrales configurados

`App_Data/RSMapsImages` queda únicamente como origen temporal para recuperar imágenes que fueron creadas antes de centralizar el almacenamiento.

## Configuración

RSMaps usa:

- `RSMaps:ImageStorageProvider=AzureBlob`
- `RSMaps:ImageStorageContainer=rsmap-images`
- `ConnectionStrings:RSMapsImages=<connection string del Storage Account>`

La cadena de conexión **no debe almacenarse en Git**.

En desarrollo puede suministrarse por variable de entorno:

```powershell
$env:ConnectionStrings__RSMapsImages='<SECRET>'
```

En Azure App Service debe configurarse como Application/Connection String equivalente antes de publicar una versión cuyo proveedor sea `AzureBlob`.

## Lectura de imágenes

Las vistas legacy pueden seguir solicitando URLs compatibles como:

```text
/cargas/187_1.jpg
```

`ModernImageCompatibilityController` resuelve esa posición contra `RSMAPS_InmuebleImagen` y abre `ClaveAlmacenamiento` mediante `IInmuebleFotoStorage`. Con `AzureBlob`, la misma ruta funciona desde casa, oficina y producción sin copiar archivos entre equipos.

## Migración de archivos existentes

No cambiar metadata existente sólo para adaptar rutas. Las claves modernas ya guardadas en SQL deben preservarse. Ejemplo del inmueble 187:

```text
187/c31ed1cbda6b4c75b21c79d4f6b94094.jpg
```

Cuando se recupere una carpeta local antigua, el archivo se debe subir al contenedor con **exactamente la misma `ClaveAlmacenamiento`** registrada en SQL.

## Inmueble 187

Diagnóstico 2026-09-08:

- `RSMAPS_InmuebleImagenes.Imagenes = 14`
- metadata moderna: 15 filas, 14 activas
- las 14 activas tienen claves GUID bajo `187/...`
- las fotografías no existen en las copias locales de casa
- las rutas legacy públicas `/Cargas/187_n.jpg` no encuentran archivo físico en la Web App actual

Conclusión: la metadata está centralizada, pero los binarios fueron guardados con el proveedor `Local` en el equipo que realizó la carga. Hay que recuperar esos binarios desde el equipo origen y subirlos al Blob preservando sus claves.

## Criterio de terminado

Este ramal queda terminado cuando:

1. casa ejecuta RSMaps con `AzureBlob` y puede leer una fotografía existente del Blob;
2. oficina usa la misma configuración;
3. Azure App Service usa la misma configuración;
4. una foto nueva subida desde cualquier entorno aparece en los demás;
5. se migraron/recuperaron las fotos modernas y legacy activas;
6. `App_Data/RSMapsImages` deja de ser necesario en operación normal.
