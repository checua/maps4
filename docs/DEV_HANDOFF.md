# RADAR — Hito 5A

Fecha 2026-11-05. Sede oficina, DESKTOP-E32803V.
Rama diagnostic/radar-azure-readiness-office.
Base 5299f15c88c67034bb1219bbe8a9750f61e8656a, origin/master.
El commit de esta unidad se obtiene con git log -1; el SHA remoto debe coincidir
al pasar Start. READY_TO_DEPLOY=NO.

## Auditoría

Repositorio accesible https://github.com/checua/maps4.git, público según API autenticada.
El remoto esperado checua/RSMaps devolvió 404. Se eligió maps4 porque coincide con
los remotos locales, el proyecto y la base exacta solicitada.
Fetch se ejecutó en un repositorio independiente. Las copias existentes se preservaron.
La creación bajo OneDrive falló. El workspace está bajo la raíz local autorizada
de visualizations de Codex, en el subdirectorio radar-azure-readiness-office.
Puede trasladarse posteriormente mediante clonación de GitHub a una ruta estable elegida
por el propietario. Los scripts no dependen de esa ruta.

Copias de oficina revisadas

- source/repos/checua/maps4, master 8e4fddb, un commit ausente de todas las ramas
  descargadas de origin. Conservado en su origen y como refs/audit/office-old-master
  en el repositorio independiente. Esa referencia es local y no debe publicarse automáticamente.
- source/repos/maps4.10/maps4, feature/rsmap-publication-readiness 0014172,
  cero commits exclusivos y tres por detrás de la rama remota homónima.
- Su worktree maps4-master, master 8c222ef, cero exclusivos y 420 por detrás de master.
- feature/rsmap-radar-matching local e653246, cero exclusivos y 239 por detrás.
- Las ramas locales radar-integration, radar-lab y radar-navigation-resilience
  coinciden con sus ramas remotas. Los árboles revisados estaban limpios.

Hay 23 ramas remotas, registradas en DEV_REMOTE_REFS.txt. No aparece
fix/radar-poison-message-recovery ni una rama con poison en su nombre.
La búsqueda de mensajes de commit con poison tampoco produjo resultados.
GitHub no acredita cambios que nunca se publicaron desde casa.

## Unidad preparada

AGENTS.md incorpora autorización permanente para commits y push de desarrollo en ambas
sedes, conservando restricciones productivas. DEV_SYNC describe la sincronización y la
incorporación única del trabajo original. Sync-RadarWorkspace ofrece Status, Fetch,
Start, Prepare y Secrets. Test-RadarWorkspaceSync usa repositorios temporales locales.
AZURE_READINESS_OFFICE conserva el procedimiento de identidad SQL y ensayo PITR pendiente.

Pruebas aisladas verifican rama sin publicar, rama sincronizada, modificación preservada,
archivo nuevo, commit sin publicar, divergencia 1/1, secreto sintético actual e histórico
y fallo de fetch. Ninguna prueba usa Azure, SQL, el Listener ni GitHub como destino de fixtures.

Gitleaks del árbol base detectó posibles claves Google en wwwroot/js/index.js y
wwwroot/js/zona-admin.js. No se muestran valores. Requieren revisar uso y restricciones
con el propietario antes de decidir medidas. La heurística también marca configuraciones
y código heredado para revisión. Estos resultados no son una certificación de exposición.
Secrets audita el árbol completo; Prepare examina cambios actuales y archivos modificados
en cada commit aún ausente de origin. La revisión dedicada del diff publicable se realiza
antes del push. Un resultado limpio no garantiza ausencia de todo secreto.
La suite aislada completó sus diez comprobaciones y git diff --cached --check pasó.
Gitleaks analizó el diff completo de esta unidad con código de salida 0 y sin hallazgos.

## Pendientes

Recuperar de casa los archivos reales de poison messages, cambios sin commit, archivos
nuevos, pruebas, SQL 57/58, sql-identity-diagnostic, auxiliares y documentación.
El ZIP de rollback y manifiesto requieren almacenamiento privado y verificación del SHA-256
240D0443DAC9CB94B5FFEDE42EA0780661A9BAFBC0EB01C6D48D30C34D7AF17D.
No están incluidos en esta unidad. SQL 58 sigue fuera del primer despliegue.

Verificar identidad SQL efectiva, restauración PITR real y condiciones finales de despliegue.
El código confirma cadenaSQL como nombre de conexión y SqlClient como proveedor,
pero la configuración efectiva de Azure sigue sin verificarse.
Se puede continuar con documentación, pruebas locales y herramientas de diagnóstico desde
esta base. La corrección original requiere recuperación e integración, sin reimplementarla.

La autorización permanente cubre commits y push seguros de desarrollo. Merge a master,
despliegues, migraciones productivas y operaciones destructivas requieren autorización.
