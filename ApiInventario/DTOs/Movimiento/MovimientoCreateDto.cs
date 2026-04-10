using System.ComponentModel.DataAnnotations;

namespace ApiInventario.DTOs.Movimiento
{
    public class MovimientoCreateDto
    {
        [Required]
        public int ProductoId { get; set; }

        [Required]
        [RegularExpression("^(Entrada|Salida|Ajuste)$", ErrorMessage = "TipoMovimiento debe ser 'Entrada', 'Salida' o 'Ajuste'.")]
        public string TipoMovimiento { get; set; } = null!;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        [Required]
        [MaxLength(150)]
        public string Motivo { get; set; } = null!;

        [MaxLength(255)]
        public string? Observacion { get; set; }
    }
}
