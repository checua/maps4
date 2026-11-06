# Diagnóstico Azure pendiente

Base revisada 407c4e4, Hito 5G del 2026-11-06. Resultado INCONCLUSO. READY_TO_DEPLOY=NO.
La inspección de oficina es estática. La identidad efectiva, configuración del App Service,
slot, publicación instalada y restauración real requieren evidencia del entorno autorizado.

maps4.csproj usa net10.0 y Microsoft.Data.SqlClient 7.0.2. Program.cs carga la configuración
estándar de ASP.NET Core. Los repositorios SQL consumen GetConnectionString("cadenaSQL").
La conexión de imágenes usa RSMapsImages y es independiente. Los archivos de configuración
versionados no prueban qué credencial o proveedor prevalece en Azure.

## Procedimiento de identidad efectivo

1. Identificar suscripción, App Service y slot con acceso de lectura autorizado.
   Registrar sólo identificadores aprobados, versión desplegada y modo de autenticación.
2. En Kudu, revisar nombres de ajustes y fuentes de configuración sin volcar valores,
   variables de entorno completas, archivos appsettings o perfiles de publicación.
   Comprobar posibles overrides de ConnectionStrings__cadenaSQL y conexiones de App Service.
3. Revisar sql-identity-diagnostic, ya recuperado en el Lab, y comprobar su equivalencia con la configuración
   y usa Microsoft.Data.SqlClient como el Web desplegado. Ejecutarlo requiere autorización
   para ese entorno. Usar directorio temporal privado y la conexión resuelta en memoria.
   No pasar contraseñas en argumentos ni abrir un endpoint público.
4. En esa conexión ejecutar únicamente la consulta de metadatos siguiente. Guardar la
   salida en evidencia de acceso restringido, fuera del repositorio público.

```sql
SELECT DB_NAME() AS database_name,
       ORIGINAL_LOGIN() AS original_login,
       SUSER_SNAME() AS server_principal,
       USER_NAME() AS database_principal,
       CONNECTIONPROPERTY('auth_scheme') AS auth_scheme;
```

5. Comprobar que la base es la esperada y contrastar principal y autenticación con la
   configuración. Una conexión desde una identidad de desarrollador o una identidad
   distinta en Kudu no acredita la identidad efectiva del proceso Web. Si hay diferencias,
   el resultado queda inconcluso. No conceder permisos ni alterar usuarios como diagnóstico.
   No copiar tokens ni excepciones que incluyan cadenas de conexión.

## Ensayo PITR independiente

Preparar una solicitud con origen, instante UTC dentro de retención, nombre único de
destino, región/servidor admitidos, coste, responsable, controles de acceso y plazo de
conservación. Autorizar explícitamente la creación antes de ejecutarla.
Azure SQL PITR crea una base nueva. Una ventana visible no acredita restauración.
Registrar identificador de operación y finalización, conectividad al destino y comprobaciones
de integridad y conteos agregados previamente acordados. Verificar DB_NAME() antes de
cualquier prueba. Mantener aplicaciones, Listener y envíos desconectados del destino.
La copia contiene información sensible y usuarios restaurados, por lo que su acceso debe
quedar restringido. No abrir firewall público ni cambiar cadenas productivas.
El borrado posterior requiere autorización y validación exacta del recurso de ensayo.
La ejecución real y sus resultados siguen pendientes.

Fuentes oficiales consultadas el 2026-11-05

- https://learn.microsoft.com/en-us/azure/azure-sql/database/recovery-using-backups?view=azuresql
- https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database

## Evidencia de lectura del Hito 5G

La sesión Azure CLI existente pudo leer metadatos ARM del App Service rsmap, activo,
sin slots adicionales enumerados. La propiedad identity.type es null.
La colección de conexiones contiene cadenaSQL, tipo SQLServer, slotSetting=false.
Los nombres de app settings consultados no contienen otro alias de cadenaSQL.
Se consultaron sólo proyecciones de nombres, tipos y estado; se omitieron valores.
El tipo SQLServer clasifica la entrada de App Service, no acredita autenticación SQL.
La ausencia de identidad administrada configurada tampoco identifica el principal SQL.

El código local usa WebApplication.CreateBuilder(args) y GetConnectionString("cadenaSQL").
No hay proveedores adicionales ni asignación de AccessToken en el repositorio de processing.
La entrada SQLServer de App Service se espera como SQLCONNSTR_cadenaSQL, normalizada
por .NET a ConnectionStrings:cadenaSQL. También pueden prevalecer
ConnectionStrings__cadenaSQL, SQLAZURECONNSTR_cadenaSQL, CUSTOMCONNSTR_cadenaSQL,
ConnectionStrings:cadenaSQL y argumentos de arranque. Deben verificarse el entorno,
web.config/argumentos y los binarios efectivamente desplegados. Los JSON locales
no prueban qué configuración está cargada en Azure.

No se ejecutó una conexión SQL productiva, un comando Kudu ni un upload.
Resultado de principal, base efectiva, auth_scheme y permisos productivos INCONCLUSO.
La base esperada es mapsMarkers; el valor efectivo todavía no está acreditado.

## Requisitos revisados

| Alcance | Permisos mínimos del código revisado | Resultado Azure |
| --- | --- | --- |
| Processing, claims y leases | SELECT, INSERT, UPDATE en dbo.RSMAPS_RadarMessageProcessing | Pendiente |
| Auditoría atómica | INSERT en dbo.RSMAPS_RadarMessageEvent | Pendiente |
| Delivery y recuperación | SELECT, INSERT, UPDATE en dbo.RSMAPS_RadarMessageDelivery, SELECT en Processing | Pendiente |
| Migración 57, identidad de mantenimiento | ALTER en Processing y visibilidad de sus metadatos/objetos requeridos | Pendiente |
| Ruta administrativa 58, fuera del primer despliegue | EXECUTE en procedimiento y CONTROL de base exigido dentro de éste | Fuera de alcance operativo |

SQL 57 añade columnas y constraints sobre Processing, dentro de una transacción.
No concede permisos. El usuario runtime no necesita ALTER, db_owner ni CONTROL
para el DML revisado. Ausencia de esos permisos administrativos no constituye un fallo
runtime. Si se utiliza una identidad de migración distinta, su validación es separada.
El diagnóstico tiene indicadores administrativos para detectar privilegios amplios.
HAS_PERMS_BY_NAME devuelve 1 si está permitido, 0 si no y NULL si no puede resolver
la consulta, por ejemplo metadatos/objeto no visible. NULL se clasifica INCONCLUSO.
No confundir permisos declarados con prueba de funcionamiento del esquema, triggers
o políticas de seguridad. Los módulos de pairing, discovery y matching tienen objetos
adicionales fuera de esta matriz de poison, y requieren una revisión por separado
antes de declarar que todos los permisos RADAR están cubiertos.

## Procedimiento pendiente con punto de autorización

1. Revisar en lectura el deployment activo, hash de binarios, slot y configuración
   efectiva del worker. Comparar código desplegado con la versión del repositorio.
   Confirmar el proveedor ganador de ConnectionStrings:cadenaSQL sin mostrar su valor.
   Evitar volcados de entorno, dumps de memoria, perfiles de publicación o cadenas en logs.
2. Acreditar que la configuración heredada por Kudu coincide con la del worker para
   esa clave y que no hay overrides en argumentos o web.config. El diagnóstico actual
   acepta exactamente uno de cinco aliases de entorno; no reproduce el conjunto de
   proveedores WebApplication ni demuestra esa equivalencia por sí solo. Una comparación
   en memoria debe emitir sólo coincidencia sí/no y nunca hashes de secretos persistentes.
3. Detenerse si falta esa evidencia. SSMS prueba su propio login; WindowsIdentity
   identifica el proceso; ninguno sustituye ORIGINAL_LOGIN/SUSER_SNAME/USER_NAME de
   una conexión abierta con la configuración efectiva de la aplicación. Para autenticación
   integrada o administrada, además debe coincidir la identidad de ejecución del worker.
4. Solicitar autorización específica para ejecutar el diagnóstico productivo de lectura.
   Si el binario del Lab no está presente, transferirlo a una carpeta privada sería una
   operación adicional que también necesita autorización. No arrancar el Web ni Listener.
   No cambiar App Settings, firewall, identidades, permisos ni configuración para conseguir acceso.
5. Sólo después de esa autorización y de las comprobaciones anteriores, ejecutar el
   siguiente comando en una consola privada Kudu con salida accesible únicamente al
   operador autorizado. La ruta representa el artefacto revisado previamente disponible.
   Los flags pertenecen al proceso de consola y no se escriben en configuración de Azure.

```powershell
$env:RADAR_LAB_MODE = 'sql-identity-diagnostic'
$env:RADAR_SQL_IDENTITY_AUTHORIZED = '1'
try {
    dotnet '<ruta-privada-verificada>/RSMaps.Radar.Lab.dll'
    if ($LASTEXITCODE -ne 0) { throw 'Diagnóstico inconcluso' }
} finally {
    Remove-Item Env:RADAR_SQL_IDENTITY_AUTHORIZED -ErrorAction SilentlyContinue
    Remove-Item Env:RADAR_LAB_MODE -ErrorAction SilentlyContinue
}
```

La consulta SELECT exacta está en SqlIdentityDiagnostic.Query. Usa DB_NAME,
ORIGINAL_LOGIN, SUSER_SNAME, USER_NAME, CONNECTIONPROPERTY('auth_scheme') y
HAS_PERMS_BY_NAME. Devuelve 18 campos permitidos, sin datos de mensajes ni credenciales.
Una salida correcta acredita sólo esa conexión hasta demostrar su equivalencia con Web.
Guardar evidencia completa en almacenamiento privado aprobado. Publicar en Git únicamente
conclusión, método, fecha y cumplimiento de permisos sin identificadores sensibles.
Si hay permisos faltantes, documentarlos y detenerse; no ejecutar GRANT ni SQL 57/58.

Fuentes de configuración y autenticación

- https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/sql/t-sql/functions/connectionproperty-transact-sql