using System.Security.Claims;
using ApiInventario.Controllers;
using ApiInventario.Data;
using ApiInventario.DTOs.Movimiento;
using ApiInventario.DTOs.Producto;
using ApiInventario.Models;
using ApiInventario.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApiInventario.Tests;

public class ConcurrencyControllerTests
{
    [Theory]
    [InlineData("movimiento")]
    [InlineData("actualizacion")]
    [InlineData("desactivacion")]
    public async Task EscrituraConConflicto_Devuelve409SinDetallesInternos(string operacion)
    {
        var interceptor = new ConflictInterceptor();
        var options = new DbContextOptionsBuilder<ApiInventarioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new ApiInventarioDbContext(options);
        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Categoria" });
        context.Productos.Add(new Producto
        {
            Id = 1, Nombre = "Producto", Codigo = "P-1", Precio = 1,
            CategoriaId = 1, StockActual = 10
        });
        await context.SaveChangesAsync();
        interceptor.Enabled = true;

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "1")], "Test"))
        };
        IActionResult result;
        if (operacion == "movimiento")
        {
            var controller = new MovimientosController(new MovimientoService(context))
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
            result = await controller.Create(new MovimientoCreateDto
            {
                ProductoId = 1, TipoMovimiento = "Entrada", Cantidad = 1, Motivo = "Prueba"
            });
        }
        else
        {
            var controller = new ProductosController(new ProductoService(context))
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
            result = operacion == "actualizacion"
                ? await controller.Update(1, new ProductoUpdateDto
                {
                    Nombre = "Actualizado", Precio = 2, CategoriaId = 1, IsActive = true
                })
                : await controller.Delete(1);
        }

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Conflicto de concurrencia", problem.Title);
        Assert.DoesNotContain("INTERNAL_TEST_DETAIL", problem.Detail!);
        Assert.Equal(1, interceptor.ConflictAttempts);
    }

    // Solo comprueba la traduccion HTTP; no simula rowversion ni transacciones SQL Server.
    private sealed class ConflictInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public int ConflictAttempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                ConflictAttempts++;
                throw new DbUpdateConcurrencyException("INTERNAL_TEST_DETAIL");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
