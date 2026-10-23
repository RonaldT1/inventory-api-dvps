using ApiInventario.DTOs.Categoria;
using ApiInventario.Models;
using ApiInventario.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiInventario.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriaService _categoriaService;

        public CategoriasController(CategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categorias = await _categoriaService.GetAllAsync();
            return Ok(categorias);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var categoria = await _categoriaService.GetByIdAsync(id);
            return categoria is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Categoría no encontrada") : Ok(categoria);
        }

        [HttpPost]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> Create([FromBody] CategoriaCreateDto dto)
        {
            var result = await _categoriaService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> Update(int id, [FromBody] CategoriaUpdateDto dto)
        {
            var result = await _categoriaService.UpdateAsync(id, dto);
            return result is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Categoría no encontrada") : Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _categoriaService.DeleteAsync(id);
            return deleted ? NoContent() : Problem(statusCode: StatusCodes.Status404NotFound, title: "Categoría no encontrada");
        }
    }
}
