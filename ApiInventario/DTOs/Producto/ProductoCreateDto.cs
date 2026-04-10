using System.ComponentModel.DataAnnotations;

namespace ApiInventario.DTOs.Producto
{
    public class ProductoCreateDto
    {
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        [MaxLength(255)]
        public string? Descripcion { get; set; }

        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Precio { get; set; }

        [Required]
        public int CategoriaId { get; set; }
    }
}
