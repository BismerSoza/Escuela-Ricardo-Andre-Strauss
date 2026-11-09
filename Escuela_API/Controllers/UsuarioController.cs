using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // POST api/Usuario/RegistrarUsuarioAPI  (auto-registro público)
        //  - Estudiante: elige su sección de la lista oficial y la cuenta queda activa.
        //  - Docente   : NO elige sección (se deduce de las asignaturas que la dirección le asigne)
        //                y la cuenta queda PENDIENTE hasta que el Director la apruebe.
        [HttpPost("RegistrarUsuarioAPI")]
        public IActionResult RegistrarUsuarioAPI(RegistroUsuarioRequestModel model)
        {
            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);

            if (nombre.Length == 0 || string.IsNullOrWhiteSpace(model.CorreoElectronico)
                || string.IsNullOrWhiteSpace(model.Contrasenna) || string.IsNullOrWhiteSpace(model.Rol))
            {
                return BadRequest("Nombre, correo, contraseña y rol son obligatorios.");
            }

            if (nombre.Length > 150) return BadRequest("El nombre es demasiado largo.");

            if (!ValidacionHelper.CorreoValido(model.CorreoElectronico, out var correo))
            {
                return BadRequest("Ingrese un correo electrónico válido.");
            }

            if (model.Contrasenna.Length < 6) return BadRequest("La contraseña debe tener al menos 6 caracteres.");

            // El auto-registro público solo admite Docente o Estudiante.
            // Las cuentas de Director se crean desde Administración por otro Director.
            if (model.Rol != "Docente" && model.Rol != "Estudiante")
            {
                return BadRequest("El rol debe ser Docente o Estudiante.");
            }

            using var context = Conexion;

            var existente = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo",
                new { Correo = correo });

            if (existente != null) return Conflict("Ya existe una cuenta registrada con ese correo.");

            string? seccion = null;
            var esDocente = model.Rol == "Docente";

            if (!esDocente)
            {
                seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
                if (seccion == null) return BadRequest("Seleccione su grado o sección de la lista.");
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Contrasenna);

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion, Activo, Pendiente)
                OUTPUT INSERTED.ConsecutivoUsuario
                VALUES (@Nombre, @Correo, @Contrasenna, @Rol, @Seccion, @Activo, @Pendiente)",
                new
                {
                    Nombre = nombre,
                    Correo = correo,
                    Contrasenna = hash,
                    model.Rol,
                    Seccion = seccion,
                    Activo = !esDocente,
                    Pendiente = esDocente
                });

            return Ok(new
            {
                Mensaje = esDocente
                    ? "Solicitud enviada. La dirección debe aprobar su cuenta de docente antes de que pueda ingresar."
                    : "Cuenta creada correctamente.",
                ConsecutivoUsuario = nuevoId,
                Pendiente = esDocente
            });
        }

        // POST api/Usuario/IniciarSesionAPI
        [HttpPost("IniciarSesionAPI")]
        public IActionResult IniciarSesionAPI(InicioSesionRequestModel model)
        {
            using var context = Conexion;

            var usuario = context.QueryFirstOrDefault<UsuarioLogin>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Contrasenna, Rol, Seccion,
                       FechaCreacion, Activo, Pendiente
                FROM Usuario
                WHERE CorreoElectronico = @Correo",
                new { Correo = (model.CorreoElectronico ?? string.Empty).Trim() });

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(model.Contrasenna ?? string.Empty, usuario.Contrasenna))
            {
                return Unauthorized("Correo o contraseña incorrectos.");
            }

            // Solo se explica el motivo DESPUÉS de verificar la contraseña, para no
            // revelar a un extraño qué correos existen o están desactivados.
            if (!usuario.Activo)
            {
                return Unauthorized(usuario.Pendiente
                    ? "Su cuenta está pendiente de aprobación por la dirección."
                    : "Su cuenta está desactivada. Comuníquese con la dirección de la escuela.");
            }

            var datosUsuario = new DatosUsuarioResponseModel
            {
                ConsecutivoUsuario = usuario.ConsecutivoUsuario,
                Nombre = usuario.Nombre,
                CorreoElectronico = usuario.CorreoElectronico,
                Rol = usuario.Rol,
                Seccion = usuario.Seccion,
                FechaCreacion = usuario.FechaCreacion
            };

            var token = _utiles.GenerarToken(datosUsuario.ConsecutivoUsuario, datosUsuario.Nombre, datosUsuario.Rol, datosUsuario.Seccion);

            return Ok(new InicioSesionResponseModel { Token = token, Usuario = datosUsuario });
        }

        // POST api/Usuario/RecuperarAccesoAPI
        // NO cambia ninguna contraseña ni la devuelve (eso permitiría que cualquiera
        // tomara una cuenta ajena solo conociendo su correo). Registra la solicitud para
        // que el Director la vea en Administración > Usuarios y restablezca la clave.
        [HttpPost("RecuperarAccesoAPI")]
        public IActionResult RecuperarAccesoAPI(RecuperarAccesoRequestModel model)
        {
            using var context = Conexion;

            context.Execute(
                "UPDATE Usuario SET ClaveSolicitada = GETDATE() WHERE CorreoElectronico = @Correo AND Activo = 1",
                new { Correo = (model.CorreoElectronico ?? string.Empty).Trim() });

            // Misma respuesta exista o no el correo: no se revela qué cuentas existen.
            return Ok(new
            {
                Mensaje = "Si el correo está registrado, la dirección recibió su solicitud y le entregará una contraseña temporal."
            });
        }

        // GET api/Usuario/ConsultarPerfilAPI
        [Authorize]
        [HttpGet("ConsultarPerfilAPI")]
        public IActionResult ConsultarPerfilAPI()
        {
            using var context = Conexion;

            var usuario = context.QueryFirstOrDefault<DatosUsuarioResponseModel>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion, FechaCreacion
                FROM Usuario WHERE ConsecutivoUsuario = @Id",
                new { Id = _utiles.ObtenerConsecutivoToken() });

            if (usuario == null) return NotFound("El usuario no se pudo encontrar.");

            return Ok(usuario);
        }

        // PUT api/Usuario/ActualizarPerfilAPI  (solo el nombre; la sección la asigna la dirección)
        [Authorize]
        [HttpPut("ActualizarPerfilAPI")]
        public IActionResult ActualizarPerfilAPI(CambiarPerfilRequestModel model)
        {
            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);
            if (nombre.Length == 0) return BadRequest("El nombre es obligatorio.");
            if (nombre.Length > 150) return BadRequest("El nombre es demasiado largo.");

            using var context = Conexion;

            var actualizados = context.Execute(
                "UPDATE Usuario SET Nombre = @Nombre WHERE ConsecutivoUsuario = @Id",
                new { Nombre = nombre, Id = _utiles.ObtenerConsecutivoToken() });

            if (actualizados == 0) return BadRequest("No se pudo actualizar el perfil.");

            return Ok(new { Mensaje = "Perfil actualizado correctamente." });
        }

        // PUT api/Usuario/CambiarContrasennaAPI  (pide la contraseña actual)
        [Authorize]
        [HttpPut("CambiarContrasennaAPI")]
        public IActionResult CambiarContrasennaAPI(CambiarAccesoRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ContrasennaNueva) || model.ContrasennaNueva.Length < 6)
            {
                return BadRequest("La contraseña nueva debe tener al menos 6 caracteres.");
            }

            using var context = Conexion;
            var id = _utiles.ObtenerConsecutivoToken();

            var hashActual = context.QueryFirstOrDefault<string>(
                "SELECT Contrasenna FROM Usuario WHERE ConsecutivoUsuario = @Id", new { Id = id });

            if (hashActual == null || !BCrypt.Net.BCrypt.Verify(model.ContrasennaActual ?? string.Empty, hashActual))
            {
                return BadRequest("La contraseña actual no es correcta.");
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.ContrasennaNueva);

            var actualizados = context.Execute(
                "UPDATE Usuario SET Contrasenna = @Contrasenna, ClaveSolicitada = NULL WHERE ConsecutivoUsuario = @Id",
                new { Contrasenna = hash, Id = id });

            if (actualizados == 0) return BadRequest("La contraseña no se pudo actualizar.");

            return Ok(new { Mensaje = "Contraseña actualizada correctamente." });
        }

        // Fila interna usada solo para el inicio de sesión.
        private class UsuarioLogin
        {
            public int ConsecutivoUsuario { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string CorreoElectronico { get; set; } = string.Empty;
            public string Contrasenna { get; set; } = string.Empty;
            public string Rol { get; set; } = string.Empty;
            public string? Seccion { get; set; }
            public DateTime FechaCreacion { get; set; }
            public bool Activo { get; set; }
            public bool Pendiente { get; set; }
        }
    }
}
