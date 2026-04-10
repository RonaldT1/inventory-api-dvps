using ApiInventario.Data;
using ApiInventario.DTOs.Producto;
using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Services
{
    public class ProductoService
    {
        private readonly ApiInventarioDbContext _context;

        public ProductoService(ApiInventarioDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductoResponseDto>> GetAllAsync()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Select(p => MapToResponse(p))
                .ToListAsync();
        }

        public async Task<ProductoResponseDto?> GetByIdAsync(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);

            return producto is null ? null : MapToResponse(producto);
        }

        public async Task<ProductoResponseDto> CreateAsync(ProductoCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new InvalidOperationException("El nombre del producto es obligatorio.");

            if (await _context.Productos.AnyAsync(p => p.Nombre == dto.Nombre))
                throw new InvalidOperationException($"Ya existe un producto con el nombre '{dto.Nombre}'.");

            if (dto.Precio <= 0)
                throw new InvalidOperationException("El precio debe ser mayor a 0.");

            if (!await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId))
                throw new InvalidOperationException($"La categoría con Id {dto.CategoriaId} no existe.");

            if (await _context.Productos.AnyAsync(p => p.Codigo == dto.Codigo))
                throw new InvalidOperationException($"Ya existe un producto con el código '{dto.Codigo}'.");

            var producto = new Producto
            {
                Nombre = dto.Nombre.Trim(),
                Descripcion = dto.Descripcion?.Trim(),
                Codigo = dto.Codigo.Trim(),
                Precio = dto.Precio,
                StockActual = 0, 
                CategoriaId = dto.CategoriaId
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            await _context.Entry(producto).Reference(p => p.Categoria).LoadAsync();
            return MapToResponse(producto);
        }

        public async Task<ProductoResponseDto?> UpdateAsync(int id, ProductoUpdateDto dto)
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto is null)
                return null;

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new InvalidOperationException("El nombre del producto es obligatorio.");

            if (dto.Precio <= 0)
                throw new InvalidOperationException("El precio debe ser mayor a 0.");

            if (!await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId))
                throw new InvalidOperationException($"La categoría con Id {dto.CategoriaId} no existe.");

            producto.Nombre = dto.Nombre.Trim();
            producto.Descripcion = dto.Descripcion?.Trim();
            producto.Precio = dto.Precio;
            producto.CategoriaId = dto.CategoriaId;
            producto.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            await _context.Entry(producto).Reference(p => p.Categoria).LoadAsync();
            return MapToResponse(producto);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto is null)
                return false;

            if (producto.Movimientos.Count != 0)
                throw new InvalidOperationException("No se puede eliminar el producto porque tiene movimientos registrados. Considere desactivarlo.");

            // Soft delete
            producto.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        private static ProductoResponseDto MapToResponse(Producto p) => new()
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Codigo = p.Codigo,
            Precio = p.Precio,
            StockActual = p.StockActual,
            CategoriaId = p.CategoriaId,
            CategoriaNombre = p.Categoria.Nombre,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }
}
