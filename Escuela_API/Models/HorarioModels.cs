namespace Escuela_API.Models
{
    public class RegistrarHorarioRequestModel
    {
        public int ConsecutivoMateria { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;
        public string HoraFin { get; set; } = string.Empty;
        public string? Aula { get; set; }
    }

    public class HorarioResponseModel
    {
        public int ConsecutivoHorario { get; set; }
        public int ConsecutivoMateria { get; set; }
        public string Materia { get; set; } = string.Empty;
        public string DiaSemana { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;
        public string HoraFin { get; set; } = string.Empty;
        public string? Aula { get; set; }
    }
}
