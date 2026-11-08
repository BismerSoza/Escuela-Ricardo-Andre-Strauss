namespace Escuela_WEB.Models
{
    public class ContactoViewModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Seccion { get; set; }
    }

    public class MensajeViewModel
    {
        public int ConsecutivoMensaje { get; set; }
        public int ConsecutivoEmisor { get; set; }
        public int ConsecutivoReceptor { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public bool Leido { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class ChatViewModel
    {
        public List<ContactoViewModel> Contactos { get; set; } = new();
        public int? ContactoSeleccionado { get; set; }
        public string? NombreContactoSeleccionado { get; set; }
        public List<MensajeViewModel> Mensajes { get; set; } = new();
    }
}
