namespace Escuela_API.Models
{
    // ---------- Secciones ----------
    public class SeccionResponseModel
    {
        public int ConsecutivoSeccion { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public int TotalEstudiantes { get; set; }
        public int TotalMaterias { get; set; }
    }

    public class RegistrarSeccionRequestModel
    {
        public string Nombre { get; set; } = string.Empty;
    }

    // ---------- Aulas ----------
    public class AulaResponseModel
    {
        public int ConsecutivoAula { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; }
        public int TotalBloques { get; set; }
    }

    public class RegistrarAulaRequestModel
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public class CambiarEstadoCatalogoRequestModel
    {
        public bool Activa { get; set; }
    }
}
