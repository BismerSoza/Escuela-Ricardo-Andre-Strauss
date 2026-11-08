using System.ComponentModel.DataAnnotations;

namespace Escuela_WEB.Models
{
    public class HistorialViewModel
    {
        public int ConsecutivoHistorial { get; set; }
        public string Materia { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public decimal? Nota { get; set; }
        public string? Observacion { get; set; }
        public string Docente { get; set; } = string.Empty;
        public string Estudiante { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
    }

    public class RegistrarHistorialViewModel
    {
        [Required]
        [Display(Name = "Estudiante")]
        public int ConsecutivoEstudiante { get; set; }

        [Required(ErrorMessage = "La materia es obligatoria.")]
        public string Materia { get; set; } = string.Empty;

        [Required(ErrorMessage = "El periodo es obligatorio.")]
        [Display(Name = "Periodo (ej: I Trimestre 2026)")]
        public string Periodo { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "La nota debe estar entre 0 y 100.")]
        public decimal? Nota { get; set; }

        public string? Observacion { get; set; }
    }

    public class PanelHistorialViewModel
    {
        public List<ContactoViewModel> Estudiantes { get; set; } = new();
        public int? EstudianteSeleccionado { get; set; }
        public List<HistorialViewModel> Registros { get; set; } = new();
        public RegistrarHistorialViewModel NuevoRegistro { get; set; } = new();
        public List<MateriaViewModel> MateriasDisponibles { get; set; } = new();
    }
}
