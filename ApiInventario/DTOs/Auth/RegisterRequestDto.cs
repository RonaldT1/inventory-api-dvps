using System.ComponentModel.DataAnnotations;

namespace ApiInventario.DTOs.Auth
{
    public class RegisterRequestDto
    {
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;
    }
}
