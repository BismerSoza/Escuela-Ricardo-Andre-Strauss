namespace Escuela_API.Models
{
    public class EnviarMensajeRequestModel
    {
        public int ConsecutivoReceptor { get; set; }
        public string Contenido { get; set; } = string.Empty;
    }

    public class MensajeResponseModel
    {
        public int ConsecutivoMensaje { get; set; }
        public int ConsecutivoEmisor { get; set; }
        public int ConsecutivoReceptor { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public bool Leido { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class ContactoResponseModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Seccion { get; set; }
    }
}
