namespace Escuela_API.Models
{
    // ---------- Dashboard ----------
    public class TarjetaResumenModel
    {
        public string Etiqueta { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
    }

    public class DashboardEstudianteResponseModel
    {
        public int TotalAsignaturas { get; set; }
        public double PromedioGeneral { get; set; }
        public int AvisosNuevos { get; set; }
        public double PorcentajeAsistencia { get; set; }
        public List<HistorialResponseModel> UltimasCalificaciones { get; set; } = new();
        public List<string> Materias { get; set; } = new();
        public List<double> PromediosPorMateria { get; set; } = new();
    }

    public class DashboardDocenteResponseModel
    {
        public int TotalMaterias { get; set; }
        public int TotalEstudiantes { get; set; }
        public double PromedioGrupo { get; set; }
        public int AvisosPublicados { get; set; }
        public List<string> Materias { get; set; } = new();
        public List<double> PromediosPorMateria { get; set; } = new();
    }

    public class DashboardDirectorResponseModel
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

    // ---------- Administración de usuarios (Director) ----------
    public class UsuarioAdminResponseModel
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

    public class CambiarEstadoUsuarioRequestModel
    {
        public bool Activo { get; set; }
    }

    public class CambiarRolUsuarioRequestModel
    {
        public string Rol { get; set; } = string.Empty;
        public string? Seccion { get; set; }
    }
}
