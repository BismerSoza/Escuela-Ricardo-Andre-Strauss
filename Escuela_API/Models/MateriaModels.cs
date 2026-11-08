namespace Escuela_API.Models
{
    public class RegistrarMateriaRequestModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int? ConsecutivoDocente { get; set; } // solo lo usa el Director al crear
    }

    public class MateriaResponseModel
    {
        public int ConsecutivoMateria { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int ConsecutivoDocente { get; set; }
        public string Docente { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
