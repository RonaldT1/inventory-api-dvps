using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Data
{
    public class ApiInventarioDbContext : DbContext
    {
        public ApiInventarioDbContext(DbContextOptions<ApiInventarioDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Categoria
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasIndex(c => c.Nombre).IsUnique();
            });

            // Producto
            modelBuilder.Entity<Producto>(entity =>
            {
                entity.HasIndex(p => p.Codigo).IsUnique();

                entity.HasOne(p => p.Categoria)
                      .WithMany(c => c.Productos)
                      .HasForeignKey(p => p.CategoriaId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // MovimientoInventario
            modelBuilder.Entity<MovimientoInventario>(entity =>
            {
                entity.HasOne(m => m.Producto)
                      .WithMany(p => p.Movimientos)
                      .HasForeignKey(m => m.ProductoId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Usuario)
                      .WithMany(u => u.Movimientos)
                      .HasForeignKey(m => m.UsuarioId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
