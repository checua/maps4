# Sincronización casa y oficina

El remoto verificado es https://github.com/checua/maps4.git y es público.
checua/RSMaps devuelve 404 con el acceso disponible. Confirmar el destino y su
visibilidad al configurar una sede. La autorización permanente del propietario permite
commits y push seguros a ramas de desarrollo en este repositorio verificado.

Cada sede usa una raíz local propia. Los comandos se ejecutan desde la raíz del
repositorio y los scripts resuelven rutas relativas. RADAR_HOME puede señalar esa raíz
en cada sesión. No guardar rutas absolutas personales en configuración compartida.
Usar ramas por tarea, conservando exactamente el mismo nombre en ambas sedes.
La rama actual es diagnostic/radar-azure-readiness-office. Su nombre se mantiene también
si se continúa en casa. La corrección original conserva fix/radar-poison-message-recovery.
Una sola sede escribe en cada rama a la vez.

## Comandos

Requieren Git y PowerShell 7. Desde la raíz del repositorio

```powershell
pwsh -File tools/Sync-RadarWorkspace.ps1 -Action Status
pwsh -File tools/Sync-RadarWorkspace.ps1 -Action Fetch
pwsh -File tools/Sync-RadarWorkspace.ps1 -Action Secrets
pwsh -File tools/Sync-RadarWorkspace.ps1 -Action Start
pwsh -File tools/Sync-RadarWorkspace.ps1 -Action Prepare
```

Fetch, Start y Prepare actualizan únicamente referencias de origin. Un fallo de red
produce error y bloquea la verificación de sincronización. Status usa referencias en caché.
Start y Prepare exigen rama remota homónima, igualdad de HEAD y árbol limpio, incluidos
archivos nuevos. Informan también archivos ignorados, que requieren revisión separada.
El script nunca hace checkout, commit, push, merge, stash, reset ni elimina archivos.
El upstream de master no sirve como prueba de publicación de una rama nueva.

Antes de salir, revisar los cambios, incluir código, pruebas y documentación no rastreados,
actualizar DEV_HANDOFF y ejecutar Secrets. La comprobación heurística examina archivos
actuales y archivos modificados en commits ausentes de origin, informa rutas y oculta valores.
Puede producir falsos positivos y falsos negativos, y no inspecciona secretos dentro de
archivos comprimidos. Exigir revisión humana y un escáner dedicado actualizado antes
de publicar. Revisar también historial, archivos ignorados y datos de producción.
No agregar automáticamente todos los archivos.

Con la autorización permanente vigente para commits y push de desarrollo, registrar los archivos revisados,
crear el commit y publicar la rama homónima con upstream. Ejecutar Prepare nuevamente.
En destino, fetch, seleccionar la rama ya existente con árbol limpio y ejecutar Start.
Si falta la rama local, crear una que siga origin con el mismo nombre.
Si está atrasada, revisar y actualizar por fast-forward conservando el trabajo.
Si diverge, detener el cambio de sede y conciliar conservando ambos historiales. No crear otra
rama con sufijo de sede para el mismo trabajo. No usar pull automático.

## Incorporación única del trabajo de casa

En el origen, verificar worktree, rama y base 5299f15. Inventariar cambios staged,
unstaged, no rastreados e ignorados relevantes, incluidas pruebas y herramientas.
Conservar un respaldo local íntegro antes de cualquier saneamiento. Revisar secretos
tanto del contenido actual como de todo el historial que se publicaría.
Preservar SQL 57/58, sql-identity-diagnostic, documentación y scripts auxiliares.
El rollback integral y su manifiesto se conservan en almacenamiento privado autorizado,
con hash verificado, fuera del repositorio público.

Tras verificar los archivos reales y su integridad, incorporar los archivos seguros a la rama
original y publicarla con su nombre original, sin forzar. Verificar el SHA en GitHub.
En oficina, fetch y crear un checkout independiente de esa rama. Comparar inventario,
HEAD y pruebas con el origen. Integrar posteriormente estas herramientas mediante
una revisión que preserve ambos trabajos dentro de ramas de desarrollo. No reconstruir poison messages.
Si hay secretos en el historial, detener publicación y acordar saneamiento sin destruir
la copia original. Los secretos se provisionan por un canal seguro en cada sede.

Tras esta incorporación, código y pruebas viajan por GitHub. El estado sin commit y
los archivos ignorados permanecen locales, por lo que Prepare debe pasar antes de salir.
