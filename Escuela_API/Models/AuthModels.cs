namespace Escuela_API.Models
{
    public class InicioSesionRequestModel
    {
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Contrasenna { get; set; } = string.Empty;
    }

    public class RegistroUsuarioRequestModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Contrasenna { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty; // "Docente" | "Estudiante"
        public string? Seccion { get; set; }
    }

    public class RecuperarAccesoRequestModel
    {
        public string CorreoElectronico { get; set; } = string.Empty;
    }

    public class CambiarPerfilRequestModel
    {
        public string? Nombre { get; set; }
        public string? Seccion { get; set; }
    }

    public class CambiarAccesoRequestModel
    {
        public string ContrasennaNueva { get; set; } = string.Empty;
    }

    public class DatosUsuarioResponseModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Seccion { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class InicioSesionResponseModel
    {
        public string Token { get; set; } = string.Empty;
        public DatosUsuarioResponseModel Usuario { get; set; } = new();
    }
}
