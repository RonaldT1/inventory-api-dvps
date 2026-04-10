namespace ApiInventario.DTOs.Categoria
{
    public class CategoriaResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
