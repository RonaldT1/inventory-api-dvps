using ApiInventario.Data;
using ApiInventario.DTOs.Auth;
using ApiInventario.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiInventario.Services
{
    public class AuthService
    {
        private readonly ApiInventarioDbContext _context;
        private readonly TokenService _tokenService;

        public AuthService(ApiInventarioDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                throw new InvalidOperationException("El nombre de usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new InvalidOperationException("El email es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new InvalidOperationException("La contraseña debe tener al menos 6 caracteres.");

            if (await _context.Usuarios.AnyAsync(u => u.Username == dto.Username))
                throw new InvalidOperationException($"El nombre de usuario '{dto.Username}' ya está en uso.");

            if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
                throw new InvalidOperationException($"El email '{dto.Email}' ya está registrado.");

            var usuario = new Usuario
            {
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User"
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return new LoginResponseDto
            {
                Token = _tokenService.GenerateToken(usuario),
                Username = usuario.Username,
                Email = usuario.Email,
                Role = usuario.Role
            };
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                throw new InvalidOperationException("Usuario y contraseña son obligatorios.");

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Username == dto.Username)
                ?? throw new InvalidOperationException("Credenciales inválidas.");

            if (!usuario.IsActive)
                throw new InvalidOperationException("La cuenta está desactivada.");

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
                throw new InvalidOperationException("Credenciales inválidas.");

            return new LoginResponseDto
            {
                Token = _tokenService.GenerateToken(usuario),
                Username = usuario.Username,
                Email = usuario.Email,
                Role = usuario.Role
            };
        }
    }
}
