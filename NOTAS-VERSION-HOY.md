# Versión de hoy — 2026-11-17
Rama: `fix/version-hoy-2026-11-17`

## Qué se arregló
1. **“Compatir” → “Compartir”** en `Views/Home/Index.cshtml`. Además `shareItem()` ya avisa: si se comparte, muestra “Inmueble compartido con éxito”; si no hay API de compartir, copia el enlace y avisa con el enlace. Si el usuario cancela el diálogo, ya no se reporta como error.
2. **Clave de Google Maps fuera del código.** Estaba quemada en `wwwroot/js/index.js` y en `wwwroot/js/zona-admin.js`. Ahora:
   - `appsettings.json` tiene `RSMaps:GoogleMapsApiKey` (vacío, sin clave real en el repo).
   - `HomeController` y `ZonaAdminController` la leen de configuración y la pasan a la vista.
   - La vista la pone en un `<meta name="rsmaps-google-maps-api-key">` y el JS la lee de ahí.
   - Sin clave, el mapa muestra un aviso en lugar del error “For development purposes only”.
   - Verificado: ya no hay ninguna clave `AIza…` en el repo.
3. **Registro de prueba oculto en el mapa público.** El inmueble “Casa en renta de prueba para validación RADAR” ya no se dibuja como marcador (`fetchMarkers` lo salta). **Falta el borrado definitivo en la BD** — ver abajo.
4. **Ficha completa.** La ficha ya muestra recámaras, baños y estacionamientos (ya existían en la BD, no se veían). Se agregó `#caracteristicasResumen` en el modal y `GetCode1()` ahora recibe esos 3 campos en los dos caminos (clic en marcador y enlace directo `?inmuebleId=`).

## Lo que NO se hizo en esta versión
- No se tocó RADAR ni la IA. Se queda ChatGPT por hoy (ver “IA” abajo).
- No se borró la fila de prueba de la BD (no hay acceso a la BD desde aquí).
- No se compiló aquí: en esta máquina no hay `dotnet`. **Hay que compilar en Visual Studio 2022.**

## Para publicar hoy (José, en tu PC)
1. En Visual Studio 2022: `git pull` / cambiar a la rama `fix/version-hoy-2026-11-17`.
2. Poner la clave de Google Maps:
   - Local: en `appsettings.json` → `RSMaps:GoogleMapsApiKey` (solo local, no subir la clave real a GitHub), o mejor en User Secrets.
   - Azure: Portal → App Service rsmap → Configuration → Application settings → nueva: nombre `RSMaps__GoogleMapsApiKey` (con doble guion bajo), valor = tu clave.
   - En Google Cloud Console, restringe esa clave: HTTP referrers `https://rsmap.azurewebsites.net/*` y solo la API “Maps JavaScript API”.
3. Borrar el registro de prueba en la BD (el borrado definitivo): en SQL, buscar el inmueble con `Observaciones` que contenga “validación RADAR” / contacto “Radar Prueba” y eliminarlo, o ponerle un estado inactivo si prefieres conservarlo. El filtro del JS es solo para que no se vea mientras tanto.
4. En Visual Studio: Build → Rebuild Solution. Si compila sin errores, Publish → perfil “Web Deploy rsmap” a Azure.
5. Verificar en `https://rsmap.azurewebsites.net/`: el mapa carga sin el aviso de desarrollo, un inmueble muestra recámaras/baños/estacionamientos, el botón dice “Compartir” y al compartir avisa, y la casa de prueba ya no aparece.

## IA: Muse (Pock) vs ChatGPT, sugerencia para hoy
- **Hoy: no cambiar RADAR.** RADAR ya está integrado con OpenAI (`OpenAiRadarInterpreter`). Cambiar el motor es un cambio aparte, con pruebas, y no es para la versión de hoy.
- **Usar Muse (Pock) para el desarrollo:** análisis, código, revisión y decisiones. Ahí ya lo tienes incluido en Muse, sin llave por uso.
- **Para decidir el cambio después:** hay que comparar con números reales. De Muse Wonder, el precio documentado es $4 por millón de tokens de entrada y $20 por millón de salida ($0.20 con caché). El precio actual de OpenAI no se verificó en esta sesión; antes de cambiar hay que revisar la tarifa vigente del modelo que usa RADAR y el volumen real de mensajes de WhatsApp, porque con poco volumen la diferencia es pequeña y el riesgo de romper RADAR es mayor. Si quieres, en la siguiente sesión lo medimos y armamos la comparación.
