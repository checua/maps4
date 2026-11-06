# Diagnóstico Azure pendiente

Base revisada 5299f15. READY_TO_DEPLOY=NO.
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
3. Recuperar sql-identity-diagnostic desde casa y revisar que construye la configuración
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
