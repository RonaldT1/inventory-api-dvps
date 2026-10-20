# Pruebas de concurrencia

Ejecutar las pruebas disponibles:

```bash
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj
```

`ConcurrencyControllerTests` inyecta una `DbUpdateConcurrencyException` al guardar
y comprueba HTTP 409, ausencia de detalles internos y ausencia de reintentos.
Utiliza InMemory solamente para preparar los datos: no demuestra concurrencia
ni atomicidad de SQL Server.

## Integración con SQL Server

Se necesita un SQL Server exclusivo de pruebas y una cuenta con permiso para
crear bases y `VIEW ANY DATABASE` para comprobar que no existan nombres ocultos.
No se requiere `sysadmin` ni permisos sobre otras bases existentes.
Configurar externamente la variable `INVENTARIO_TEST_SQLSERVER`
con una conexión a ese servidor cuyo catálogo inicial sea `master`.
No guardar la conexión en archivos versionados ni usar conexiones de producción.
Se rechazan `AttachDbFilename` y sus alias (`extended properties`,
`initial file name`), incluso con valor vacío, así como `User Instance=true`.
Las opciones no reconocidas también provocan rechazo antes de abrir conexiones.

```bash
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --filter FullyQualifiedName~SqlServerConcurrencyTests
```

Sin la variable, la teoría de integración aparece como omitida. Con la variable,
se ejecutan tres casos: entrada, salida y ajuste que mantiene el mismo stock.
Cada caso crea una base nueva `InventarioConcurrencyTests_<GUID>` y aplica las
migraciones solamente en ella; nunca en el catálogo recibido. Las bases nuevas
se conservan para inspección, incluso si falla una prueba. Su limpieza queda a
cargo del responsable del servidor; las pruebas no eliminan bases ni datos.

Antes de crear cada base se consulta `sys.databases` desde `master`, usando un
parámetro para el nombre generado. Si existe, se aborta sin migraciones. Después
se ejecuta un `CREATE DATABASE` explícito: si el nombre aparece entre la consulta
y la creación, SQL Server rechaza la creación y tampoco se ejecutan migraciones.
Solo después de crear la base con éxito se cambia de catálogo y se migra.
La salida de xUnit registra el nombre antes del intento y confirma la creación
antes de migrar, para conservarlo incluso si una fase posterior falla. Un intento
sin confirmación debe revisarse manualmente si hubo un fallo de conexión.
Para ver la salida de los casos aprobados, utilizar
`--logger "console;verbosity=detailed"` y conservar el resultado de la ejecución.

## Ejecutar únicamente pruebas sin SQL Server

```bash
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --filter 'FullyQualifiedName!~ApiInventario.Tests.SqlServerConcurrencyTests'
```

`SqlServerTestSafetyTests` comprueba la validación de conexiones y la generación
de nombres sin abrir conexiones. No demuestra la consulta de existencia ni la
creación en SQL Server; esas comprobaciones pertenecen a la integración.

Dos contextos leen la misma versión antes del primer guardado. El primero
confirma su movimiento y el segundo intenta guardar con la versión obsoleta.
Esto reproduce de forma determinista el intercalado conflictivo, sin depender
de la planificación de tareas ni compartir un DbContext entre operaciones.
Un tercer contexto comprueba el stock, la versión y la existencia de exactamente
un movimiento, verificando que la operación rechazada no deja datos parciales.

## Migración de la aplicación

`AddProductoRowVersion` añade una columna SQL Server `rowversion` a `Productos`.
Generar esta migración no la aplica. El arranque existente de la API aplica
migraciones por defecto: no iniciar la aplicación contra una base existente sin
autorización. Para deshabilitarlo, usar `Database__AutoMigrate=false`.
Las pruebas invocan servicios/controladores directamente y no arrancan la API.
