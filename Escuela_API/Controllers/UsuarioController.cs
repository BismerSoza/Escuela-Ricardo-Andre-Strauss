using BCrypt.Net;
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

        // POST api/Usuario/RegistrarUsuarioAPI
        [HttpPost("RegistrarUsuarioAPI")]
        public IActionResult RegistrarUsuarioAPI(RegistroUsuarioRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Nombre) || string.IsNullOrWhiteSpace(model.CorreoElectronico)
                || string.IsNullOrWhiteSpace(model.Contrasenna) || string.IsNullOrWhiteSpace(model.Rol))
            {
                return BadRequest("Nombre, correo, contraseña y rol son obligatorios.");
            }

            // El auto-registro público solo admite Docente o Estudiante.
            // Las cuentas de Director se crean desde el panel de Administración
            // por otro Director ya autenticado (ver AdminController).
            if (model.Rol != "Docente" && model.Rol != "Estudiante")
            {
                return BadRequest("El rol debe ser Docente o Estudiante.");
            }

            using var context = Conexion;

            var existente = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo",
                new { Correo = model.CorreoElectronico });

            if (existente != null)
            {
                return Conflict("Ya existe una cuenta registrada con ese correo.");
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Contrasenna);

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion)
                OUTPUT INSERTED.ConsecutivoUsuario
                VALUES (@Nombre, @Correo, @Contrasenna, @Rol, @Seccion)",
                new { model.Nombre, Correo = model.CorreoElectronico, Contrasenna = hash, model.Rol, model.Seccion });

            return Ok(new { Mensaje = "Cuenta creada correctamente.", ConsecutivoUsuario = nuevoId });
        }

        // POST api/Usuario/IniciarSesionAPI
        [HttpPost("IniciarSesionAPI")]
        public IActionResult IniciarSesionAPI(InicioSesionRequestModel model)
        {
            using var context = Conexion;

            var usuario = context.QueryFirstOrDefault(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Contrasenna, Rol, Seccion, FechaCreacion
                FROM Usuario
                WHERE CorreoElectronico = @Correo AND Activo = 1",
                new { Correo = model.CorreoElectronico });

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(model.Contrasenna, (string)usuario.Contrasenna))
            {
                return Unauthorized("Correo o contraseña incorrectos.");
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
        [HttpPost("RecuperarAccesoAPI")]
        public IActionResult RecuperarAccesoAPI(RecuperarAccesoRequestModel model)
        {
            using var context = Conexion;

            var consecutivo = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo",
                new { Correo = model.CorreoElectronico });

            if (consecutivo == null)
            {
                // No se revela si el correo existe, por seguridad.
                return Ok(new { Mensaje = "Si el correo existe, se generó una nueva contraseña temporal." });
            }

            var claveTemporal = _utiles.GenerarContrasennaTemporal();
            var hash = BCrypt.Net.BCrypt.HashPassword(claveTemporal);

            context.Execute(
                "UPDATE Usuario SET Contrasenna = @Contrasenna WHERE ConsecutivoUsuario = @Id",
                new { Contrasenna = hash, Id = consecutivo });

            // En producción esto se enviaría por correo electrónico (ej. con MailKit).
            return Ok(new { Mensaje = "Se generó una contraseña temporal.", ContrasennaTemporal = claveTemporal });
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

        // PUT api/Usuario/ActualizarPerfilAPI
        [Authorize]
        [HttpPut("ActualizarPerfilAPI")]
        public IActionResult ActualizarPerfilAPI(CambiarPerfilRequestModel model)
        {
            using var context = Conexion;
            var id = _utiles.ObtenerConsecutivoToken();

            var actualizados = context.Execute(@"
                UPDATE Usuario
                SET Nombre = COALESCE(NULLIF(@Nombre, ''), Nombre),
                    Seccion = COALESCE(NULLIF(@Seccion, ''), Seccion)
                WHERE ConsecutivoUsuario = @Id",
                new { model.Nombre, model.Seccion, Id = id });

            if (actualizados == 0) return BadRequest("No se pudo actualizar el perfil.");

            return Ok(new { Mensaje = "Perfil actualizado correctamente." });
        }

        // PUT api/Usuario/CambiarContrasennaAPI
        [Authorize]
        [HttpPut("CambiarContrasennaAPI")]
        public IActionResult CambiarContrasennaAPI(CambiarAccesoRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ContrasennaNueva) || model.ContrasennaNueva.Length < 6)
            {
                return BadRequest("La contraseña debe tener al menos 6 caracteres.");
            }

            using var context = Conexion;
            var hash = BCrypt.Net.BCrypt.HashPassword(model.ContrasennaNueva);

            var actualizados = context.Execute(
                "UPDATE Usuario SET Contrasenna = @Contrasenna WHERE ConsecutivoUsuario = @Id",
                new { Contrasenna = hash, Id = _utiles.ObtenerConsecutivoToken() });

            if (actualizados == 0) return BadRequest("La contraseña no se pudo actualizar.");

            return Ok(new { Mensaje = "Contraseña actualizada correctamente." });
        }
    }
}
