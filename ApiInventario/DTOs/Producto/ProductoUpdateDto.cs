using System.ComponentModel.DataAnnotations;

namespace ApiInventario.DTOs.Producto
{
    public class ProductoUpdateDto
    {
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        [MaxLength(255)]
        public string? Descripcion { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Precio { get; set; }

        [Required]
        public int CategoriaId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
