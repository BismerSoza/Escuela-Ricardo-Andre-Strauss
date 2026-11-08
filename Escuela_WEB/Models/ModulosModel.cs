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

        [Required(ErrorMessage = "La sección es obligatoria.")]
        public string Seccion { get; set; } = string.Empty;

        [Display(Name = "Docente a cargo (solo Director)")]
        public int? ConsecutivoDocente { get; set; }
    }

    // ---------- Horario ----------
    public class HorarioViewModel
    {
        public int ConsecutivoHorario { get; set; }
        public int ConsecutivoMateria { get; set; }
        public string Materia { get; set; } = string.Empty;
        public string DiaSemana { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;
        public string HoraFin { get; set; } = string.Empty;
        public string? Aula { get; set; }
    }

    public class RegistrarHorarioViewModel
    {
        [Required]
        [Display(Name = "Asignatura")]
        public int ConsecutivoMateria { get; set; }

        [Required(ErrorMessage = "Seleccione un día.")]
        [Display(Name = "Día")]
        public string DiaSemana { get; set; } = "Lunes";

        [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
        [Display(Name = "Hora inicio")]
        public string HoraInicio { get; set; } = string.Empty;

        [Required(ErrorMessage = "La hora de fin es obligatoria.")]
        [Display(Name = "Hora fin")]
        public string HoraFin { get; set; } = string.Empty;

        public string? Aula { get; set; }
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
        public DateTime FechaCreacion { get; set; }
    }

    public class RegistrarUsuarioAdminViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "Debe tener al menos 6 caracteres.")]
        public string Contrasenna { get; set; } = string.Empty;

        [Required]
        public string Rol { get; set; } = "Estudiante";

        [Display(Name = "Sección / Grupo")]
        public string? Seccion { get; set; }
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
