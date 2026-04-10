using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiInventario.Models
{
    [Table("Productos")]
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        [MaxLength(255)]
        public string? Descripcion { get; set; }

        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        public int StockActual { get; set; } = 0;

        public int CategoriaId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey(nameof(CategoriaId))]
        public Categoria Categoria { get; set; } = null!;

        public ICollection<MovimientoInventario> Movimientos { get; set; } = [];
    }
}
