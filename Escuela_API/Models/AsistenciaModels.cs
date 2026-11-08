namespace Escuela_API.Models
{
    public class RegistrarAsistenciaItemModel
    {
        public int ConsecutivoEstudiante { get; set; }
        public string Estado { get; set; } = string.Empty; // Presente | Ausente | Tardía | Justificada
        public string? Observacion { get; set; }
    }

    public class RegistrarAsistenciaLoteRequestModel
    {
        public int ConsecutivoMateria { get; set; }
        public DateTime Fecha { get; set; }
        public List<RegistrarAsistenciaItemModel> Registros { get; set; } = new();
    }

    public class AsistenciaResponseModel
    {
        public int ConsecutivoAsistencia { get; set; }
        public int ConsecutivoEstudiante { get; set; }
        public string Estudiante { get; set; } = string.Empty;
        public int ConsecutivoMateria { get; set; }
        public string Materia { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Observacion { get; set; }
    }

    public class ResumenAsistenciaResponseModel
    {
        public int TotalRegistros { get; set; }
        public int Presentes { get; set; }
        public int Ausentes { get; set; }
        public int Tardias { get; set; }
        public int Justificadas { get; set; }
        public double PorcentajeAsistencia { get; set; }
    }
}
