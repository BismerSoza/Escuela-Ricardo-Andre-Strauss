using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Escuela_API.Services
{
    public class UtilesService(IConfiguration _config, IHttpContextAccessor _httpContext) : IUtilesService
    {
        public string GenerarToken(int consecutivo, string nombre, string rol, string? seccion)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!);
            var minutos = int.TryParse(_config["Jwt:ExpiraMinutos"], out var m) ? m : 480;

            var claims = new List<Claim>
            {
                new("consecutivo", consecutivo.ToString()),
                new("nombre", nombre),
                new(ClaimTypes.Role, rol)
            };

            if (!string.IsNullOrEmpty(seccion))
            {
                claims.Add(new Claim("seccion", seccion));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(minutos),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string GenerarContrasennaTemporal()
        {
            // Sin caracteres ambiguos (0/O, 1/I/L) para que sea fácil de dictar o copiar.
            const string caracteres = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            var chars = new char[10];

            for (int i = 0; i < chars.Length; i++)
                chars[i] = caracteres[System.Security.Cryptography.RandomNumberGenerator.GetInt32(caracteres.Length)];

            return new string(chars);
        }

        public int ObtenerConsecutivoToken()
        {
            var valor = _httpContext.HttpContext?.User.FindFirstValue("consecutivo");
            return int.TryParse(valor, out var id) ? id : 0;
        }

        public string ObtenerRolToken()
        {
            return _httpContext.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }

        public string ObtenerNombreToken()
        {
            return _httpContext.HttpContext?.User.FindFirstValue("nombre") ?? string.Empty;
        }

        public string? ObtenerSeccionToken()
        {
            return _httpContext.HttpContext?.User.FindFirstValue("seccion");
        }
    }
}
