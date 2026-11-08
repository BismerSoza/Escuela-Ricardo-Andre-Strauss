using System.ComponentModel.DataAnnotations;

namespace Escuela_WEB.Models
{
    public class InicioSesionViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasenna { get; set; } = string.Empty;
    }

    public class RegistroViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "Debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasenna { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione un rol.")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = "Estudiante";

        [Display(Name = "Sección / Grupo")]
        public string? Seccion { get; set; }
    }

    public class RecuperarAccesoViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;
    }

    public class ConfiguracionViewModel
    {
        [Required]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; } = string.Empty;

        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Display(Name = "Sección / Grupo")]
        public string? Seccion { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña (opcional)")]
        public string? ContrasennaNueva { get; set; }
    }

    public class UsuarioSesionModel
    {
        public int ConsecutivoUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Seccion { get; set; }
    }
}
