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

    [Fact]
    public async Task CreateAsync_ConPrecioCero_LanzaInvalidOperationException()
    {
        await using var context = CreateContext();
        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Laptops" });
        await context.SaveChangesAsync();

        var service = new ProductoService(context);
        var dto = new ProductoCreateDto
        {
            Nombre = "Laptop Lenovo",
            Codigo = "LEN-001",
            Precio = 0,
            CategoriaId = 1
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(dto));
        Assert.Equal("El precio debe ser mayor a 0.", exception.Message);
    }

    private static ApiInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApiInventarioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApiInventarioDbContext(options);
    }
}
