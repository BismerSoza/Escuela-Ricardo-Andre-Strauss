using System.ComponentModel.DataAnnotations;

namespace Escuela_WEB.Models
{
    public class AvisoViewModel
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

    public class RegistrarAvisoViewModel
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contenido es obligatorio.")]
        [DataType(DataType.MultilineText)]
        public string Contenido { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Prioridad")]
        public string Prioridad { get; set; } = "General";

        [Display(Name = "Dirigido a")]
        public string? Seccion { get; set; }
    }
}
