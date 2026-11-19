# Precio por m² — notas de la versión

Rama: `fix/precio-m2-2026-11-19`, basada en master `8d52b66` (versión unificada casa + oficina).
Fecha: 2026-11-19. Paso 2 del plan de licenciamiento. NO publicado todavía.

## Qué cambió

La ficha de propiedad y las tarjetas de Mi Inventario ahora muestran precio por m²,
solo cuando hay datos (precio > 0 y superficie > 0). Si falta precio o superficie,
muestra "Precio por m² no disponible" y no se inventa ningún número.

- **Ficha (mapa):** nueva línea debajo de Recámaras / Baños / Estacionamientos:
  `Precio por m²: Terreno $8,413 • Construcción $13,462` (ejemplo id 187:
  $1,750,000 / 208 m² terreno = $8,413/m²; / 130 m² construcción = $13,462/m²).
  Si solo hay terreno o solo construcción, muestra solo ese. Redondeo al peso.
- **Mi Inventario:** en cada tarjeta, junto a Terreno y Construcción, aparecen
  `Precio/m² terreno: $8,413` y `Precio/m² construcción: $13,462` cuando hay datos.

## Archivos tocados (solo adiciones, 22 líneas)

- `Views/Home/Index.cshtml`: nuevo `<div id="precioM2Resumen">` en el modal Inmueble.
- `wwwroot/js/index.js`: en `GetCode1()` se calcula y se escribe `#precioM2Resumen`.
- `Views/Inventario/Index.cshtml`: dos `<span>` de precio/m² en `.property-meta` de cada tarjeta.

## Qué NO se tocó

- Compartir (`shareItem`), recámaras/baños/estacionamientos, `renderMapPropertyCard`
  y el filtro de la propiedad de prueba (`isRadarTestMapListing`) siguen igual.
- `index.js` verificado con `node --check` (OK). No se pudo compilar con `dotnet`
  en esta máquina (no hay SDK); la compilación se hace en la PC de la oficina.

## Publicar (oficina, Visual Studio 2026)

1. En la PC de la oficina: `git fetch origin`, `git checkout master`,
   `git merge origin/fix/precio-m2-2026-11-19` (o aplicar el patch
   `rsmap-precio-m2.patch` con `git apply`), `git push origin master`.
2. En VS 2026 abrir `C:\Proyectos\RSMaps\maps4.sln`, Rebuild en Release.
3. Publicar con el perfil Web Deploy (el que funcionó el 2026-11-18/19).
   Si da 401 No autorizado, descargar de nuevo el perfil de publicación en
   Azure y reimportarlo en VS (Publicar > Nuevo perfil > Importar perfil).
4. Verificar en vivo: abrir la ficha de la casa de $1,750,000 en Valle del
   Guadiana (id 187) y revisar que diga Terreno $8,413 y Construcción $13,462;
   en Mi Inventario, la misma tarjeta debe mostrar los dos precios por m².
