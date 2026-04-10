using System.ComponentModel.DataAnnotations;

namespace ApiInventario.DTOs.Categoria
{
    public class CategoriaCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = null!;

        [MaxLength(255)]
        public string? Descripcion { get; set; }
    }
}
