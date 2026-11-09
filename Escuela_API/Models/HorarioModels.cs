namespace Escuela_API.Models
{
    public class RegistrarHorarioRequestModel
    {
        public int ConsecutivoMateria { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;   // "HH:mm"
        public string HoraFin { get; set; } = string.Empty;      // "HH:mm"
        public int? ConsecutivoAula { get; set; }
    }

    public class HorarioResponseModel
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

    public class DisponibilidadResponseModel
    {
        public bool Disponible { get; set; }
        public List<string> Conflictos { get; set; } = new();
    }
}
