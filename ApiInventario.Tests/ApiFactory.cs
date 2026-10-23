using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using ApiInventario.Data;
using ApiInventario.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ApiInventario.Tests;

// Uses real JWT validation and the real HTTP pipeline, but never connects to SQL Server.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private static readonly string SigningKey = new('t', 64);
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!");
    public Exception? SaveFailure { get; set; }
    public Exception? DocumentFailure { get; set; }
    public int FailedSaveAttempts { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("Jwt:Key", SigningKey);
        builder.UseSetting("Jwt:Issuer", "ApiInventario");
        builder.UseSetting("Jwt:Audience", "ApiInventarioClient");
        builder.ConfigureTestServices(services =>
        {
            services.Configure<OpenApiOptions>("v1", options => options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                if (DocumentFailure is { } failure) throw failure;
                return Task.CompletedTask;
            }));
            services.RemoveAll<ApiInventarioDbContext>();
            services.RemoveAll<DbContextOptions<ApiInventarioDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApiInventarioDbContext>>();
            services.AddDbContext<ApiInventarioDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .AddInterceptors(new FailureInterceptor(this)));
        });
    }

    public HttpClient CreateApiClient(UserRole? role = UserRole.User, bool validUserId = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiInventarioDbContext>();
        if (!db.Usuarios.Any())
        {
            db.Usuarios.Add(new Usuario
            {
                Id = 1, Username = "tester", Email = "tester@example.com", PasswordHash = PasswordHash
            });
            db.Categorias.Add(new Categoria { Id = 1, Nombre = "Categoria" });
            db.Productos.Add(new Producto
            {
                Id = 1, Nombre = "Producto", Codigo = "P-1", Precio = 10, StockActual = 5, CategoriaId = 1
            });
            db.SaveChanges();
        }
        if (role is not null)
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, role.Value.ToString()) };
            if (validUserId) claims.Add(new Claim(ClaimTypes.NameIdentifier, "1"));
            var token = new JwtSecurityToken(
                issuer: "ApiInventario", audience: "ApiInventarioClient", claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    private sealed class FailureInterceptor(ApiFactory factory) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (factory.SaveFailure is { } failure)
            {
                factory.FailedSaveAttempts++;
                throw failure;
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
