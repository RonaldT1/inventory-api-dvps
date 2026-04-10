using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApiInventario.Models
{
    [Table("MovimientosInventario")]
    public class MovimientoInventario
    {
        [Key]
        public int Id { get; set; }

        public int ProductoId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TipoMovimiento { get; set; } = null!;

        public int Cantidad { get; set; }

        [Required]
        [MaxLength(150)]
        public string Motivo { get; set; } = null!;

        [MaxLength(255)]
        public string? Observacion { get; set; }

        public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;

        public int UsuarioId { get; set; }

        // Navigation
        [ForeignKey(nameof(ProductoId))]
        public Producto Producto { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }
}
