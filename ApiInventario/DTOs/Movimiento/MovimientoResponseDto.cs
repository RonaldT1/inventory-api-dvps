namespace ApiInventario.DTOs.Movimiento
{
    public class MovimientoResponseDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = null!;
        public string TipoMovimiento { get; set; } = null!;
        public int Cantidad { get; set; }
        public string Motivo { get; set; } = null!;
        public string? Observacion { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioUsername { get; set; } = null!;
    }
}
