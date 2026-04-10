using ApiInventario.Data;
using ApiInventario.DTOs.Movimiento;
using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Services
{
    public class MovimientoService
    {
        private readonly ApiInventarioDbContext _context;

        public MovimientoService(ApiInventarioDbContext context)
        {
            _context = context;
        }

        public async Task<List<MovimientoResponseDto>> GetAllAsync()
        {
            return await _context.MovimientosInventario
                .Include(m => m.Producto)
                .Include(m => m.Usuario)
                .OrderByDescending(m => m.FechaMovimiento)
                .Select(m => MapToResponse(m))
                .ToListAsync();
        }

        public async Task<List<MovimientoResponseDto>> GetByProductoIdAsync(int productoId)
        {
            return await _context.MovimientosInventario
                .Include(m => m.Producto)
                .Include(m => m.Usuario)
                .Where(m => m.ProductoId == productoId)
                .OrderByDescending(m => m.FechaMovimiento)
                .Select(m => MapToResponse(m))
                .ToListAsync();
        }

        public async Task<MovimientoResponseDto> CreateAsync(MovimientoCreateDto dto, int usuarioId)
        {
            // Validar tipo de movimiento
            if (dto.TipoMovimiento is not ("Entrada" or "Salida" or "Ajuste"))
                throw new InvalidOperationException("El tipo de movimiento debe ser 'Entrada', 'Salida' o 'Ajuste'.");

            if (dto.Cantidad <= 0)
                throw new InvalidOperationException("La cantidad debe ser mayor a 0.");

            var producto = await _context.Productos.FindAsync(dto.ProductoId)
                ?? throw new InvalidOperationException($"El producto con Id {dto.ProductoId} no existe.");

            if (!producto.IsActive)
                throw new InvalidOperationException("No se pueden registrar movimientos en un producto inactivo.");

            // Validar stock suficiente para salidas
            if (dto.TipoMovimiento == "Salida" && producto.StockActual < dto.Cantidad)
                throw new InvalidOperationException(
                    $"Stock insuficiente. Stock actual: {producto.StockActual}, cantidad solicitada: {dto.Cantidad}.");

            // Actualizar stock según tipo
            producto.StockActual = dto.TipoMovimiento switch
            {
                "Entrada" => producto.StockActual + dto.Cantidad,
                "Salida" => producto.StockActual - dto.Cantidad,
                "Ajuste" => dto.Cantidad, // Ajuste establece el stock directamente
                _ => producto.StockActual
            };

            if (producto.StockActual < 0)
                throw new InvalidOperationException("El movimiento resultaría en stock negativo.");

            var movimiento = new MovimientoInventario
            {
                ProductoId = dto.ProductoId,
                TipoMovimiento = dto.TipoMovimiento,
                Cantidad = dto.Cantidad,
                Motivo = dto.Motivo.Trim(),
                Observacion = dto.Observacion?.Trim(),
                UsuarioId = usuarioId
            };

            _context.MovimientosInventario.Add(movimiento);
            await _context.SaveChangesAsync();

            await _context.Entry(movimiento).Reference(m => m.Producto).LoadAsync();
            await _context.Entry(movimiento).Reference(m => m.Usuario).LoadAsync();

            return MapToResponse(movimiento);
        }

        private static MovimientoResponseDto MapToResponse(MovimientoInventario m) => new()
        {
            Id = m.Id,
            ProductoId = m.ProductoId,
            ProductoNombre = m.Producto.Nombre,
            TipoMovimiento = m.TipoMovimiento,
            Cantidad = m.Cantidad,
            Motivo = m.Motivo,
            Observacion = m.Observacion,
            FechaMovimiento = m.FechaMovimiento,
            UsuarioId = m.UsuarioId,
            UsuarioUsername = m.Usuario.Username
        };
    }
}
