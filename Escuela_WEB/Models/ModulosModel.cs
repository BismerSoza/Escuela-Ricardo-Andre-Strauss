using System.ComponentModel.DataAnnotations;

namespace Escuela_WEB.Models
{
    // ---------- Materia (Asignaturas) ----------
    public class MateriaViewModel
    {
        public int ConsecutivoMateria { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int ConsecutivoDocente { get; set; }
        public string Docente { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class RegistrarMateriaViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione la sección.")]
        [Display(Name = "Grado / Sección")]
        public string Seccion { get; set; } = string.Empty;

        [Display(Name = "Docente a cargo")]
        public int? ConsecutivoDocente { get; set; }
    }

    // ---------- Horario ----------
    public class HorarioViewModel
    {
        public int ConsecutivoHorario { get; set; }
        public int ConsecutivoMateria { get; set; }
        public string Materia { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int ConsecutivoDocente { get; set; }
        public string Docente { get; set; } = string.Empty;
        public string DiaSemana { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;
        public string HoraFin { get; set; } = string.Empty;
        public int? ConsecutivoAula { get; set; }
        public string? Aula { get; set; }
    }

    public class AulaViewModel
    {
        public int ConsecutivoAula { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public int TotalBloques { get; set; }
    }

    public class SeccionAdminViewModel
    {
        public int ConsecutivoSeccion { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public int TotalEstudiantes { get; set; }
        public int TotalMaterias { get; set; }
    }

    public class RegistrarHorarioViewModel : IValidatableObject
    {
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione una asignatura.")]
        [Display(Name = "Asignatura")]
        public int ConsecutivoMateria { get; set; }

        [Required(ErrorMessage = "Seleccione un día.")]
        [Display(Name = "Día")]
        public string DiaSemana { get; set; } = "Lunes";

        [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
        [Display(Name = "Hora de inicio")]
        public string HoraInicio { get; set; } = string.Empty;

        [Required(ErrorMessage = "La hora de fin es obligatoria.")]
        [Display(Name = "Hora de fin")]
        public string HoraFin { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione el aula.")]
        [Display(Name = "Aula")]
        public int? ConsecutivoAula { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
        {
            if (!string.IsNullOrEmpty(HoraInicio) && !string.IsNullOrEmpty(HoraFin)
                && string.CompareOrdinal(HoraInicio, HoraFin) >= 0)
            {
                yield return new ValidationResult("La hora de fin debe ser posterior a la de inicio.", new[] { nameof(HoraFin) });
            }
        }
    }

    // Todo lo que necesita la pantalla del calendario semanal.
    public class PanelHorarioViewModel
    {
        public List<HorarioViewModel> Bloques { get; set; } = new();
        public string Rol { get; set; } = string.Empty;
        public int ConsecutivoUsuario { get; set; }
        public string? Tipo { get; set; }           // mio | seccion | aula | docente
        public string? Valor { get; set; }
        public List<string> Secciones { get; set; } = new();
        public List<AulaViewModel> Aulas { get; set; } = new();
        public List<UsuarioAdminViewModel> Docentes { get; set; } = new();
        public bool PuedeGestionar => Rol == "Director" || Rol == "Docente";
    }

    // ---------- Asistencia ----------
    public class EstudianteAsistenciaViewModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? ConsecutivoAsistencia { get; set; }
        public string? Estado { get; set; }
        public string? Observacion { get; set; }
    }

    public class PanelAsistenciaViewModel
    {
        public List<MateriaViewModel> Materias { get; set; } = new();
        public int? MateriaSeleccionada { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;
        public List<EstudianteAsistenciaViewModel> Estudiantes { get; set; } = new();
    }

    public class MiAsistenciaViewModel
    {
        public int ConsecutivoAsistencia { get; set; }
        public string Materia { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Observacion { get; set; }
    }

    // ---------- Dashboard ----------
    public class DashboardEstudianteViewModel
    {
        public int TotalAsignaturas { get; set; }
        public double PromedioGeneral { get; set; }
        public int AvisosNuevos { get; set; }
        public double PorcentajeAsistencia { get; set; }
        public List<HistorialViewModel> UltimasCalificaciones { get; set; } = new();
        public List<string> Materias { get; set; } = new();
        public List<double> PromediosPorMateria { get; set; } = new();
    }

    public class DashboardDocenteViewModel
    {
        public int TotalMaterias { get; set; }
        public int TotalEstudiantes { get; set; }
        public double PromedioGrupo { get; set; }
        public int AvisosPublicados { get; set; }
        public List<string> Materias { get; set; } = new();
        public List<double> PromediosPorMateria { get; set; } = new();
    }

    public class DashboardDirectorViewModel
    {
        public int TotalUsuarios { get; set; }
        public int TotalDocentes { get; set; }
        public int TotalEstudiantes { get; set; }
        public int TotalMaterias { get; set; }
        public double PromedioInstitucional { get; set; }
        public int AvisosRecientes { get; set; }
        public int UsuariosPendientes { get; set; }
        public int ClavesSolicitadas { get; set; }
        public List<string> Secciones { get; set; } = new();
        public List<double> PromediosPorSeccion { get; set; } = new();
    }

    // ---------- Administración (Director) ----------
    public class UsuarioAdminViewModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Seccion { get; set; }
        public bool Activo { get; set; }
        public bool Pendiente { get; set; }
        public DateTime? ClaveSolicitada { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class EditarUsuarioViewModel : IValidatableObject
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required]
        public string Rol { get; set; } = "Estudiante";

        [Display(Name = "Grado / Sección")]
        public string? Seccion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
        {
            if (Rol == "Estudiante" && string.IsNullOrWhiteSpace(Seccion))
                yield return new ValidationResult("Seleccione la sección del estudiante.", new[] { nameof(Seccion) });
        }
    }

    public class RegistrarUsuarioAdminViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre es demasiado largo.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "Debe tener al menos 6 caracteres.")]
        [Display(Name = "Contraseña inicial")]
        public string Contrasenna { get; set; } = string.Empty;

        [Required]
        public string Rol { get; set; } = "Estudiante";

        [Display(Name = "Grado / Sección")]
        public string? Seccion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
        {
            if (Rol == "Estudiante" && string.IsNullOrWhiteSpace(Seccion))
                yield return new ValidationResult("Seleccione la sección del estudiante.", new[] { nameof(Seccion) });
        }
    }

    public class RegistroErrorViewModel
    {
        public int ConsecutivoError { get; set; }
        public string? Ruta { get; set; }
        public string? Mensaje { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    // ---------- Componentes visuales del tablero ----------
    public class TarjetaStatModel
    {
        public string Etiqueta { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
        public string Icono { get; set; } = "bi-circle";
        public string Color { get; set; } = "azul";   // azul | verde | ambar | morado
        public string? Pie { get; set; }
    }

    public class TileModel
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = "bi-circle";
        public string Controlador { get; set; } = string.Empty;
        public string Accion { get; set; } = "Index";
    }
}
