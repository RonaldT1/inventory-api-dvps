using ApiInventario.Data;
using ApiInventario.DTOs.Categoria;
using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Services
{
    public class CategoriaService
    {
        private readonly ApiInventarioDbContext _context;

        public CategoriaService(ApiInventarioDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoriaResponseDto>> GetAllAsync()
        {
            return await _context.Categorias
                .Select(c => MapToResponse(c))
                .ToListAsync();
        }

        public async Task<CategoriaResponseDto?> GetByIdAsync(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            return categoria is null ? null : MapToResponse(categoria);
        }

        public async Task<CategoriaResponseDto> CreateAsync(CategoriaCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new InvalidOperationException("El nombre de la categoría es obligatorio.");

            if (await _context.Categorias.AnyAsync(c => c.Nombre == dto.Nombre))
                throw new InvalidOperationException($"Ya existe una categoría con el nombre '{dto.Nombre}'.");

            var categoria = new Categoria
            {
                Nombre = dto.Nombre.Trim(),
                Descripcion = dto.Descripcion?.Trim()
            };

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();

            return MapToResponse(categoria);
        }

        public async Task<CategoriaResponseDto?> UpdateAsync(int id, CategoriaUpdateDto dto)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria is null)
                return null;

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new InvalidOperationException("El nombre de la categoría es obligatorio.");

            if (await _context.Categorias.AnyAsync(c => c.Nombre == dto.Nombre && c.Id != id))
                throw new InvalidOperationException($"Ya existe otra categoría con el nombre '{dto.Nombre}'.");

            categoria.Nombre = dto.Nombre.Trim();
            categoria.Descripcion = dto.Descripcion?.Trim();

            await _context.SaveChangesAsync();
            return MapToResponse(categoria);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var categoria = await _context.Categorias
                .Include(c => c.Productos)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria is null)
                return false;

            if (categoria.Productos.Count != 0)
                throw new InvalidOperationException("No se puede eliminar la categoría porque tiene productos asociados.");

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
            return true;
        }

        private static CategoriaResponseDto MapToResponse(Categoria c) => new()
        {
            Id = c.Id,
            Nombre = c.Nombre,
            Descripcion = c.Descripcion,
            CreatedAt = c.CreatedAt
        };
    }
}
