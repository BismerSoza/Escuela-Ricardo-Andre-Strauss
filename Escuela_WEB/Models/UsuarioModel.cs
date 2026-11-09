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

    public class RegistroViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre es demasiado largo.")]
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

        [Required(ErrorMessage = "Confirme la contraseña.")]
        [Compare(nameof(Contrasenna), ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContrasenna { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione un rol.")]
        [Display(Name = "Soy")]
        public string Rol { get; set; } = "Estudiante";

        // Solo aplica a estudiantes: el docente no elige sección (la dirección le asigna asignaturas).
        [Display(Name = "Grado / Sección")]
        public string? Seccion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
        {
            if (Rol != "Estudiante" && Rol != "Docente")
                yield return new ValidationResult("Seleccione si es estudiante o docente.", new[] { nameof(Rol) });

            if (Rol == "Estudiante" && string.IsNullOrWhiteSpace(Seccion))
                yield return new ValidationResult("Seleccione su grado o sección.", new[] { nameof(Seccion) });
        }
    }

    public class RecuperarAccesoViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;
    }

    public class ConfiguracionViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre es demasiado largo.")]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; } = string.Empty;

        // Solo lectura: se muestran pero no se editan desde aquí.
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Display(Name = "Rol")]
        public string Rol { get; set; } = string.Empty;

        [Display(Name = "Grado / Sección")]
        public string? Seccion { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string? ContrasennaActual { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string? ContrasennaNueva { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmar nueva contraseña")]
        public string? ConfirmarContrasenna { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
        {
            if (string.IsNullOrWhiteSpace(ContrasennaNueva)) yield break;

            if (string.IsNullOrWhiteSpace(ContrasennaActual))
                yield return new ValidationResult("Ingrese su contraseña actual para cambiarla.", new[] { nameof(ContrasennaActual) });

            if (ContrasennaNueva.Length < 6)
                yield return new ValidationResult("La nueva contraseña debe tener al menos 6 caracteres.", new[] { nameof(ContrasennaNueva) });

            if (ContrasennaNueva != ConfirmarContrasenna)
                yield return new ValidationResult("Las contraseñas no coinciden.", new[] { nameof(ConfirmarContrasenna) });
        }
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
