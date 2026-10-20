using ApiInventario.Data;
using ApiInventario.DTOs.Movimiento;
using ApiInventario.Models;
using ApiInventario.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace ApiInventario.Tests;

public class SqlServerConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public SqlServerConcurrencyTests(ITestOutputHelper output) => _output = output;

    [SqlServerTheory]
    [InlineData("Entrada", 2, 12)]
    [InlineData("Salida", 7, 3)]
    [InlineData("Ajuste", 10, 10)]
    public async Task VersionObsoleta_RechazaMovimientoYRevierteTodo(
        string tipo, int cantidad, int stockEsperado)
    {
        var connection = SqlServerTestSafety.ValidateConnection(
            Environment.GetEnvironmentVariable("INVENTARIO_TEST_SQLSERVER"));
        var databaseName = SqlServerTestSafety.GenerateDatabaseName();
        await using (var master = new SqlConnection(connection.ConnectionString))
        {
            await master.OpenAsync();
            // Sin visibilidad completa, una base existente podria quedar oculta.
            await using var permission = master.CreateCommand();
            permission.CommandText = "SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DATABASE')";
            if (Convert.ToInt32(await permission.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException("Las pruebas requieren VIEW ANY DATABASE para verificar nombres existentes.");

            await using var exists = master.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
            exists.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException($"La base {databaseName} ya existe. No se ejecutaron migraciones.");

            // CREATE explicito: si alguien crea el mismo nombre tras la comprobacion,
            // SQL Server rechaza CREATE y nunca se llega a MigrateAsync.
            _output.WriteLine($"Intento de creacion de base de pruebas: {databaseName}");
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
            _output.WriteLine($"Base de pruebas creada (sin eliminacion automatica): {databaseName}");
        }
        connection.InitialCatalog = databaseName;
        var options = new DbContextOptionsBuilder<ApiInventarioDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        await using (var setup = new ApiInventarioDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var categoria = new Categoria { Nombre = "Prueba" };
            setup.Categorias.Add(categoria);
            var usuario = new Usuario
            {
                Username = "prueba", Email = "prueba@example.com", PasswordHash = "test-only",
                Role = UserRole.User
            };
            setup.Usuarios.Add(usuario);
            await setup.SaveChangesAsync();
            setup.Productos.Add(new Producto
            {
                Nombre = "Producto", Codigo = "TEST-1", Precio = 1, StockActual = 10,
                CategoriaId = categoria.Id
            });
            await setup.SaveChangesAsync();
        }

        await using var first = new ApiInventarioDbContext(options);
        await using var second = new ApiInventarioDbContext(options);
        var productoA = await first.Productos.SingleAsync();
        var productoB = await second.Productos.SingleAsync();
        var originalVersion = productoB.RowVersion.ToArray();
        Assert.Equal(8, originalVersion.Length);
        Assert.Equal(productoA.RowVersion, originalVersion);
        var usuarioId = await first.Usuarios.Select(u => u.Id).SingleAsync();

        MovimientoCreateDto Dto() => new()
        {
            ProductoId = productoA.Id, TipoMovimiento = tipo, Cantidad = cantidad, Motivo = "Prueba"
        };

        // Ambas solicitudes ya leyeron la misma version. FindAsync reutiliza la entidad seguida.
        await new MovimientoService(first).CreateAsync(Dto(), usuarioId);
        Assert.False(originalVersion.SequenceEqual(productoA.RowVersion));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => new MovimientoService(second).CreateAsync(Dto(), usuarioId));

        // Otro contexto evita verificar accidentalmente el estado local del guardado fallido.
        await using var verification = new ApiInventarioDbContext(options);
        var persisted = await verification.Productos.SingleAsync();
        Assert.Equal(stockEsperado, persisted.StockActual);
        Assert.Equal(productoA.RowVersion, persisted.RowVersion);
        var movimiento = Assert.Single(await verification.MovimientosInventario.ToListAsync());
        Assert.Equal(tipo, movimiento.TipoMovimiento);
        Assert.Equal(cantidad, movimiento.Cantidad);

        // No se elimina la base: no ejecutar operaciones destructivas automaticamente.
    }
}

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVENTARIO_TEST_SQLSERVER")))
            Skip = "Requiere INVENTARIO_TEST_SQLSERVER y un SQL Server exclusivo de pruebas.";
    }
}
