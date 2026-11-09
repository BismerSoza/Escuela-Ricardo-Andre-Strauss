using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Escuela_API.Services
{
    public static class ValidacionHelper
    {
        // Valida y normaliza un correo (recorta espacios y pasa a minúsculas).
        public static bool CorreoValido(string? correo, out string normalizado)
        {
            normalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();

            if (normalizado.Length == 0 || normalizado.Length > 150) return false;

            try
            {
                var direccion = new MailAddress(normalizado);
                return direccion.Address == normalizado && normalizado.Contains('.', StringComparison.Ordinal);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Quita espacios al inicio/final y colapsa espacios repetidos: "  Aula   3 " -> "Aula 3".
        public static string LimpiarNombre(string? texto)
        {
            return Regex.Replace((texto ?? string.Empty).Trim(), @"\s+", " ");
        }
    }
}
