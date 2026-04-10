using ApiInventario.Data;
using ApiInventario.DTOs.Movimiento;
using ApiInventario.Models;
using ApiInventario.Services;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Tests;

public class MovimientoServiceTests
{
    [Fact]
    public async Task CreateAsync_Entrada_AumentaStockDelProducto()
    {
        await using var context = CreateContext();
        await SeedInventarioAsync(context, stockActual: 0);

        var service = new MovimientoService(context);
        var dto = new MovimientoCreateDto
        {
            ProductoId = 1,
            TipoMovimiento = "Entrada",
            Cantidad = 10,
            Motivo = "Compra inicial"
        };

        await service.CreateAsync(dto, usuarioId: 1);

        var producto = await context.Productos.FindAsync(1);
        Assert.NotNull(producto);
        Assert.Equal(10, producto.StockActual);
    }

    [Fact]
    public async Task CreateAsync_SalidaSinStockSuficiente_LanzaInvalidOperationException()
    {
        await using var context = CreateContext();
        await SeedInventarioAsync(context, stockActual: 5);

        var service = new MovimientoService(context);
        var dto = new MovimientoCreateDto
        {
            ProductoId = 1,
            TipoMovimiento = "Salida",
            Cantidad = 10,
            Motivo = "Venta"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(dto, usuarioId: 1));
        Assert.Equal("Stock insuficiente. Stock actual: 5, cantidad solicitada: 10.", exception.Message);
    }

    private static ApiInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApiInventarioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApiInventarioDbContext(options);
    }

    private static async Task SeedInventarioAsync(ApiInventarioDbContext context, int stockActual)
    {
        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Laptops" });
        context.Usuarios.Add(new Usuario
        {
            Id = 1,
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = "Admin"
        });
        context.Productos.Add(new Producto
        {
            Id = 1,
            Nombre = "Laptop Lenovo",
            Codigo = "LEN-001",
            Precio = 2500,
            StockActual = stockActual,
            CategoriaId = 1,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }
}
