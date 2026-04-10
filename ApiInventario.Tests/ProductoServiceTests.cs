using ApiInventario.Data;
using ApiInventario.DTOs.Producto;
using ApiInventario.Models;
using ApiInventario.Services;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Tests;

public class ProductoServiceTests
{
    [Fact]
    public async Task CreateAsync_CreaProductoConStockEnCero()
    {
        await using var context = CreateContext();
        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Laptops" });
        await context.SaveChangesAsync();

        var service = new ProductoService(context);
        var dto = new ProductoCreateDto
        {
            Nombre = "Laptop Lenovo",
            Descripcion = "Equipo para oficina",
            Codigo = "LEN-001",
            Precio = 2500,
            CategoriaId = 1
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(0, result.StockActual);
    }

    private static ApiInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApiInventarioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApiInventarioDbContext(options);
    }
}
