namespace Escuela_API.Models
{
    public class RegistrarAvisoRequestModel
    {
        public string Titulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public string Prioridad { get; set; } = "General"; // Importante | Información | General
        public string? Seccion { get; set; }
    }

    public class AvisoResponseModel
    {
        public int ConsecutivoAviso { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public string Prioridad { get; set; } = "General";
        public string? Seccion { get; set; }
        public string Autor { get; set; } = string.Empty;
        public int ConsecutivoAutor { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
