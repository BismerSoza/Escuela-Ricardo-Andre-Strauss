namespace Escuela_API.Models
{
    public class RegistrarHistorialRequestModel
    {
        public int ConsecutivoEstudiante { get; set; }
        public int? ConsecutivoMateria { get; set; }
        public string Materia { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public decimal? Nota { get; set; }
        public string? Observacion { get; set; }
    }

    public class ImportarCalificacionesResultModel
    {
        public int FilasProcesadas { get; set; }
        public int FilasInsertadas { get; set; }
        public List<string> Errores { get; set; } = new();
    }

    public class HistorialResponseModel
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
}
