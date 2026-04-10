namespace ApiInventario.DTOs.Producto
{
    public class ProductoResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string Codigo { get; set; } = null!;
        public decimal Precio { get; set; }
        public int StockActual { get; set; }
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
