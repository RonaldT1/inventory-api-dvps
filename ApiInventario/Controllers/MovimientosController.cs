using System.Security.Claims;
using ApiInventario.DTOs.Movimiento;
using ApiInventario.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiInventario.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MovimientosController : ControllerBase
    {
        private readonly MovimientoService _movimientoService;

        public MovimientosController(MovimientoService movimientoService)
        {
            _movimientoService = movimientoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var movimientos = await _movimientoService.GetAllAsync();
            return Ok(movimientos);
        }

        [HttpGet("producto/{productoId}")]
        public async Task<IActionResult> GetByProducto(int productoId)
        {
            var movimientos = await _movimientoService.GetByProductoIdAsync(productoId);
            return Ok(movimientos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MovimientoCreateDto dto)
        {
            try
            {
                var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await _movimientoService.CreateAsync(dto, usuarioId);
                return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
