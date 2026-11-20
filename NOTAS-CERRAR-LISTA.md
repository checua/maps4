# Paso 3 — Cerrar la lista pública (no exponer correo ni teléfono)

Fecha: 2026-11-20. Rama: `fix/cerrar-lista-publica-2026-11-20`, basada en master `6057048`.
Estado: parche preparado, NO publicado. La compilación con `dotnet` queda para la PC de la oficina.

## Qué problema cierra
- `GET /Home/listaInmuebles` era público, sin auth, y devolvía la lista completa
  con `refUsuario.correo` y `telefono` en las 70 propiedades.
- Lo mismo pasaba en `GET /Home/listaInmueblesViewport` (mapa por zona) y en
  `GET /Inmueble/GetInmuebleById` (la ficha la usa para cargar una propiedad).
- El JavaScript además comparaba el correo del asesor en el cliente para
  decidir si mostraba los botones de editar/eliminar.

## Qué cambió
- Nuevo `Models/InmueblePublico.cs`: proyección pública compartida.
  - Nunca incluye `refUsuario.correo`, `refUsuario.telefono`, `contra` ni
    demás datos del asesor. Solo: idAsesor, nombres, aPaterno, aMaterno,
    inmobiliaria, nick y foto.
  - `Telefono` solo se incluye cuando hay sesión iniciada
    (`TelefonoSoloAutenticados = true`). Para hacerlo público de nuevo,
    cambiar esa constante a `false` en ese mismo archivo.
  - Agrega `PuedeEditar` (true solo si el usuario autenticado es el dueño),
    calculado en el servidor con el correo de la sesión.
- `Controllers/HomeController.cs`: `listaInmuebles` y `listaInmueblesViewport`
  devuelven la proyección (mismos nombres de campo que antes + `puedeEditar`).
- `Controllers/InmuebleController.cs`: `GetInmuebleById` (público, lo usa la
  ficha) devuelve la misma proyección. `GetInmueblePrivadoById` (con `[Authorize]`
  y validación SQL) no se tocó.
- `wwwroot/js/index.js` y `wwwroot/js/map-viewport.js`: los botones de
  editar/eliminar ya no comparan correos; usan `item.puedeEditar`.
- Ficha (`GetCode1`): sin sesión, en lugar del teléfono dice
  "Inicia sesión para ver el teléfono". Con sesión, el teléfono se ve igual que antes.

## Qué NO cambió
- Mapa, filtros, ficha, Compartir, recámaras/baños/estacionamientos, precio por
  m², `renderMapPropertyCard`, Inventario y Zonas con login: todo igual.
- La página Share (`/Share/{id}`) no mostraba correo ni teléfono; no se tocó.
- `GET /Home/listaUsuarios` sigue existiendo y devuelve usuarios completos;
  ningún JS del mapa lo usa. Queda pendiente decidir si se cierra o se protege
  con `[Authorize]` (paso aparte, no incluido aquí).

## Cómo verificar en vivo después de publicar
1. Sin sesión: abrir `https://rsmap.azurewebsites.net/Home/listaInmuebles` — no
   debe aparecer ningún `correo` ni `telefono` de asesor en el JSON.
2. Abrir una ficha sin sesión: debe decir "Inicia sesión para ver el teléfono".
3. Con sesión: la ficha muestra el teléfono y el dueño ve sus botones de editar.

## Publicar (oficina, VS 2026)
1. Aplicar el parche en `C:\Proyectos\RSMaps` (master en 6057048).
2. Compilar en Release y publicar con el perfil Web Deploy, como las veces anteriores.
3. Verificar los 3 puntos de arriba.
