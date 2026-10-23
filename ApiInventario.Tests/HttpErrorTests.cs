using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApiInventario.Data;
using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApiInventario.Tests;

public class HttpErrorTests
{
    [Fact]
    public async Task ErrorEnOpenApi_RestauraRespuestaYDevuelve500Seguro()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        factory.DocumentFailure = new InvalidOperationException("INTERNAL_TEST_DETAIL");
        using var response = await client.GetAsync("/openapi/v1.json");
        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.False(problem.TryGetProperty("security", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task TokenAusenteOInvalido_Devuelve401ConChallenge(string? token)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        if (token is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync("/api/Productos");
        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task UsuarioSinRolAdmin_Devuelve403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsJsonAsync("/api/Categorias", new { nombre = "Nueva" });
        await AssertProblem(response, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/Productos/999")]
    [InlineData("/api/Categorias/999")]
    [InlineData("/api/no-existe")]
    public async Task RecursoORutaInexistente_Devuelve404(string route)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync(route);
        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DtoInvalido_DevuelveValidationProblemDetails()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using var response = await client.PostAsJsonAsync("/api/Auth/register", new { });
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task JsonMalformado_Devuelve400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using var response = await client.PostAsync("/api/Auth/login",
            new StringContent("{", Encoding.UTF8, "application/json"));
        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReglaDeNegocioInvalida_Devuelve400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsJsonAsync("/api/Productos", new
        {
            nombre = "Nuevo", codigo = "P-2", precio = 0, categoriaId = 1
        });
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Contains("precio", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CredencialesInvalidas_Devuelve401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "tester", password = "incorrecta"
        });
        await AssertProblem(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CuentaDesactivada_Devuelve403()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiInventarioDbContext>();
            (await db.Usuarios.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "tester", password = "TestPassword123!"
        });
        await AssertProblem(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CategoriaDuplicada_Devuelve409()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(UserRole.Admin);
        using var response = await client.PostAsJsonAsync("/api/Categorias", new { nombre = "Categoria" });
        await AssertProblem(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MovimientoSobreProductoInexistente_Devuelve404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsJsonAsync("/api/Movimientos", new
        {
            productoId = 999, tipoMovimiento = "Entrada", cantidad = 1, motivo = "Prueba"
        });
        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenSinIdentificador_Devuelve401()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(validUserId: false);
        using var response = await client.PostAsJsonAsync("/api/Movimientos", new
        {
            productoId = 1, tipoMovimiento = "Entrada", cantidad = 1, motivo = "Prueba"
        });
        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task ChallengeConAcceptHtml_TambienDevuelveProblemDetails()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        using var response = await client.GetAsync("/api/Productos");
        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task ConflictoDeConcurrencia_Devuelve409SinReintentar()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        factory.SaveFailure = new DbUpdateConcurrencyException("INTERNAL_TEST_DETAIL");
        using var response = await client.PostAsJsonAsync("/api/Movimientos", new
        {
            productoId = 1, tipoMovimiento = "Entrada", cantidad = 1, motivo = "Prueba"
        });
        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal(1, factory.FailedSaveAttempts);
    }

    [Theory]
    [InlineData(false, "application/json")]
    [InlineData(true, "application/json")]
    [InlineData(false, "text/html")]
    public async Task ExcepcionInesperada_Devuelve500SinDetallesInternos(bool databaseError, string accept)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        factory.SaveFailure = databaseError
            ? new DbUpdateException("INTERNAL_TEST_DETAIL")
            : new InvalidOperationException("INTERNAL_TEST_DETAIL");
        using var response = await client.PostAsJsonAsync("/api/Productos", new
        {
            nombre = "Nuevo", codigo = "P-2", precio = 10, categoriaId = 1
        });
        await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.Equal(1, factory.FailedSaveAttempts);
    }

    [Fact]
    public async Task ErrorDeNegocio_ConservaCorsParaReact()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        using var response = await client.PostAsJsonAsync("/api/Productos", new
        {
            nombre = "Nuevo", codigo = "P-2", precio = 0, categoriaId = 1
        });
        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal("http://localhost:5173", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task ProductoConHistorial_NoSeDesactivaYDevuelve409()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var movement = await client.PostAsJsonAsync("/api/Movimientos", new
        {
            productoId = 1, tipoMovimiento = "Entrada", cantidad = 2, motivo = "Compra"
        });
        Assert.Equal(HttpStatusCode.Created, movement.StatusCode);
        var body = await movement.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("productoId").GetInt32());
        Assert.Equal(1, body.GetProperty("usuarioId").GetInt32());
        Assert.Equal("Entrada", body.GetProperty("tipoMovimiento").GetString());
        using var deleted = await client.DeleteAsync("/api/Productos/1");
        await AssertProblem(deleted, HttpStatusCode.Conflict);
        using var read = await client.GetAsync("/api/Productos/1");
        var product = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(product.GetProperty("isActive").GetBoolean());
        Assert.Equal(7, product.GetProperty("stockActual").GetInt32());
    }

    [Theory]
    [InlineData("Salida", 6)]
    [InlineData("Ajuste", 0)]
    public async Task MovimientoInvalido_ConservaStockYDevuelve400(string type, int quantity)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsJsonAsync("/api/Movimientos", new
        {
            productoId = 1, tipoMovimiento = type, cantidad = quantity, motivo = "Prueba"
        });
        await AssertProblem(response, HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiInventarioDbContext>();
        Assert.Equal(5, (await db.Productos.SingleAsync()).StockActual);
        Assert.Empty(await db.MovimientosInventario.ToListAsync());
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("http://127.0.0.1:4200", true)]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://untrusted.example", false)]
    public async Task Cors_ConservaOrigenesYPermiteVite(string origin, bool allowed)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/Productos");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed) Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task LoginYCreacionYDesactivacion_ConservanContratosExitosos()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient(role: null);
        using var login = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "tester", password = "TestPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal(new[] { "email", "role", "token", "username" },
            session.EnumerateObject().Select(p => p.Name).Order().ToArray());
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
        using var created = await client.PostAsJsonAsync("/api/Productos", new
        {
            nombre = "Nuevo", codigo = "P-2", precio = 10, categoriaId = 1
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, product.GetProperty("stockActual").GetInt32());
        Assert.True(product.GetProperty("isActive").GetBoolean());
        Assert.Equal("P-2", product.GetProperty("codigo").GetString());
        Assert.NotNull(created.Headers.Location);
        var id = product.GetProperty("id").GetInt32();
        using var deleted = await client.DeleteAsync($"/api/Productos/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsStringAsync());
        using var read = await client.GetAsync($"/api/Productos/{id}");
        var inactive = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(inactive.GetProperty("isActive").GetBoolean());
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("INTERNAL_TEST_DETAIL", text);
        var problem = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.True(problem.TryGetProperty("type", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
        return problem;
    }
}
