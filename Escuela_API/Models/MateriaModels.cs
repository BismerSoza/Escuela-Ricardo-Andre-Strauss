namespace Escuela_API.Models
{
    public class RegistrarMateriaRequestModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int? ConsecutivoDocente { get; set; } // lo exige el Director al crear (el Docente se asigna a sí mismo)
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

    // Datos mínimos de una asignatura, para validaciones internas.
    public class MateriaBasica
    {
        public int ConsecutivoMateria { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Seccion { get; set; } = string.Empty;
        public int ConsecutivoDocente { get; set; }
        public bool Activa { get; set; }
    }
}
