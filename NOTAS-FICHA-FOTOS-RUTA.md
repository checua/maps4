# Ficha: fotos rotas en enlaces /m/{id} y /i/{id}

## Problema
En la ficha abierta desde un enlace profundo (/m/187, /i/187) las fotos salían
rotas, aunque en el mapa principal (/) sí se veían. No era caché (Ctrl+F5 no
lo arregló) ni el Blob: las 14 fotos de la 187 están en rsmap-images y
/Cargas/187_1.jpg devuelve 200.

## Causa
GetCode1() en wwwroot/js/index.js construía la URL de las fotos como ruta
RELATIVA: `Cargas/187_1.jpg`.
- En / → se resuelve como /Cargas/187_1.jpg (200).
- En /m/187 → se resuelve como /m/Cargas/187_1.jpg (404).

## Cambio (1 línea)
`var imgSrc = \`Cargas/${b}_${x}.jpg\`;` → `var imgSrc = \`/Cargas/${b}_${x}.jpg\`;`
(ruta absoluta, con / al inicio). Solo afecta a la tira de fotos de la ficha;
el resto del código ya usaba URLs absolutas.

## Publicar
Publicar desde Visual Studio 2026 con Web Deploy, como siempre.
Verificar en vivo: abrir https://rsmap.azurewebsites.net/m/187 y la ficha debe
mostrar las 14 fotos.

## Pendiente (separado, en la base de datos)
La ficha en /m/187 muestra "Recámaras: — • Baños: — • Estacionamientos: —"
aunque la 187 tiene 2/2/3. GetInmuebleById devuelve esos campos en null porque
el stored procedure sp_GetInmuebleById (en Azure SQL, no está en el repo) no
los selecciona. Hay que actualizar el SP para que también devuelva Recamaras,
BanosCompletos, MediosBanos, Estacionamientos, Niveles, AntiguedadAnos y
AmenidadesCsv de dbo.RSMAPS_Inmueble. El código (MapearInmueblePublico) ya los
lee si el SP los trae.
