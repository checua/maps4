# RADAR — Hito 5C, oficina activada con bloqueo local de SQL

Fecha 2026-11-06. Equipo comprobado DESKTOP-E32803V.
Ruta definitiva de oficina C:\Users\tommy\source\repos\radar-office.
Clon independiente desde https://github.com/checua/maps4.git.
Rama fix/radar-poison-message-recovery, con upstream homónimo.
HEAD inicial verificado exactamente d2f2182bcf6f95b8791016d5c0b0272d9d005878.
El commit documental de esta activación avanza la rama sin cambiar código.
Las copias anteriores de oficina se conservan.

Recuperados desde GitHub SQL 57/58, PoisonMessageRegression, PoisonMessageSqlRegression,
SqlIdentityDiagnostic, DeliveryFlowRegression, herramientas de sincronización y
los cuatro documentos RADAR_POISON de implementación, preflight, despliegue y rollback.
El ZIP privado de rollback y credenciales no forman parte de este clon.
SQL 58 continúa excluido del primer despliegue.

Validación realizada con SDK .NET 10.0.400

- Start pasó sobre el SHA esperado con árbol limpio e igualdad local/remota.
- Build Release maps4.sln pasó con 0 errores y 18 advertencias.
- Build Release RSMaps.Radar.Lab pasó con 0 errores y 22 advertencias.
- Test-RadarWorkspaceSync pasó sus diez comprobaciones aisladas.
- poison-message-regression y delivery-flow-regression pasaron.
- poison-controller-timeout-regression pasó con timeout real de 75.1 segundos.
- Gitleaks 8.30.1 sobre 5299f15..HEAD inicial pasó sin hallazgos.
- poison-message-sql-regression falló antes de crear su base de prueba porque
  LocalDB no pudo abrir MSSQLLocalDB, error 50 / 0x89C50118.
- sql-identity-regression devolvió SQL_IDENTITY_FAILED en el mismo entorno.

LocalDB enumera MSSQLLocalDB y ProjectsV13, pero consultar e iniciar MSSQLLocalDB
falla. Hay motores 15.0 y 17.0 instalados y registros de instancias vinculados a
MSSQL13E.LOCALDB. El diagnóstico apunta a configuración heredada incompatible,
pendiente de confirmar. No se recrearon ni eliminaron instancias o bases existentes.
Para completar validación SQL se necesita reparar o aislar LocalDB preservando los
archivos y bases anteriores, y repetir ambos modos. Los fixtures usan LocalDB fijo,
bases sintéticas con nombre único y nunca admiten una cadena productiva.

Las advertencias incluyen nulabilidad y NU1902/NU1903 de
System.Security.Cryptography.Pkcs 9.0.13. Su actualización requiere una unidad
separada con pruebas. Persisten los hallazgos históricos de claves cliente Google
ya registrados en 5A. El análisis del historial nuevo no certifica toda la base.

El código ya puede recuperarse y compilarse desde GitHub sin la computadora de casa.
La validación SQL de oficina sigue bloqueada. Identidad efectiva de Azure, PITR real
y condiciones finales productivas siguen pendientes. READY_TO_DEPLOY=NO.

Para continuar, abrir esta ruta, ejecutar Sync-RadarWorkspace.ps1 -Action Start,
consultar este documento y conservar la rama homónima. La igualdad final se comprueba
con HEAD y origin/fix/radar-poison-message-recovery, después del push documental.

---
# RADAR — Hito 5B: entrega casa-oficina

Fecha 2026-11-05. Origen verificado en LAPTOP-JANETH, worktree maps4-radar-poison.
Rama que debe continuarse: fix/radar-poison-message-recovery.
Base 5299f15c88c67034bb1219bbe8a9750f61e8656a.
Commit de implementación publicado y verificado local/remoto:
991daee453037884c409479a03930d2487930eaa.
Build solución Release y Lab PASS, 0 errores y 18 advertencias preexistentes.
Cuatro modos de regresión enumerados abajo PASS y diez comprobaciones de
sincronización local PASS. Se adaptó git init del fixture para Git antiguo de casa.
Gitleaks 8.30.1, descargado del release oficial con SHA256 verificado, no encontró
secretos en los archivos candidatos, el diff staged ni el historial nuevo.
Esta validación cubre los cambios publicados; la base heredada conserva los
hallazgos históricos de claves cliente Google documentados por oficina.
ZIP de rollback comprobado como ignorado y excluido del index.
Política importada selectivamente desde 10967c0710b02bec6e2e007a355bfb304abae441
de diagnostic/radar-azure-readiness-office, sin merge de master.

La implementación original está presente: presupuesto durable, terminal permanente,
fencing de leases, auditoría atómica, contrato HTTP422, Listener sin fallback central,
validación de cache, SQL57/58, regresiones y sql-identity-diagnostic. SQL58 continúa
excluido del primer despliegue. No reimplementar poison desde la preparación de oficina.

Para retomar, con árbol limpio en oficina:

```powershell
git fetch origin
git switch --track origin/fix/radar-poison-message-recovery
pwsh -NoProfile -File tools/Sync-RadarWorkspace.ps1 -Action Start
git rev-parse HEAD
```

Si la rama local ya existe, inspeccionar divergencia y usar esa rama homónima.
No descartar trabajo local ni aplicar pull automático. El SHA final de entrega es
el HEAD remoto de esta rama después del commit documental; verificar igualdad con
git rev-parse origin/fix/radar-poison-message-recovery. El informe de entrega registra
el SHA inmutable final. Un archivo no puede contener el hash del commit que lo incluye.

Build Release solución y Lab; pruebas poison-message-regression,
poison-message-sql-regression, sql-identity-regression y delivery-flow-regression.
SQL aislado requiere LocalDB. Nunca apuntar estos fixtures a mapsMarkers.
El test e2e-hard-no-orillas conserva el fallo preexistente documentado.

El rollback ZIP, manifiestos, outputs, logs y bases de prueba quedan fuera de Git.
La oficina recibe fuentes y procedimiento reproducible Test-PoisonRollback.ps1.
No trasladar credenciales ni perfil WhatsApp. La recuperación productiva PITR,
identidad SQL efectiva y configuración Azure siguen pendientes. READY_TO_DEPLOY=NO.

## Historial de preparación de oficina (Hito 5A)

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
