using BCrypt.Net;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    // Todo este controlador es exclusivo del rol Director: administración
    // institucional de usuarios y secciones. Ni Docente ni Estudiante
    // pueden acceder a ninguna de estas rutas.
    [Authorize(Roles = "Director")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Admin/ConsultarUsuariosAPI?rol=Docente
        [HttpGet("ConsultarUsuariosAPI")]
        public IActionResult ConsultarUsuariosAPI(string? rol)
        {
            using var context = Conexion;

            var usuarios = context.Query<UsuarioAdminResponseModel>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion, Activo, FechaCreacion
                FROM Usuario
                WHERE (@Rol IS NULL OR Rol = @Rol)
                ORDER BY Rol, Nombre", new { Rol = string.IsNullOrWhiteSpace(rol) ? null : rol });

            return Ok(usuarios);
        }

        // POST api/Admin/RegistrarUsuarioAPI  (el Director puede crear cuentas de cualquier rol, incluyendo otro Director)
        [HttpPost("RegistrarUsuarioAPI")]
        public IActionResult RegistrarUsuarioAPI(RegistroUsuarioRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Nombre) || string.IsNullOrWhiteSpace(model.CorreoElectronico)
                || string.IsNullOrWhiteSpace(model.Contrasenna) || string.IsNullOrWhiteSpace(model.Rol))
            {
                return BadRequest("Nombre, correo, contraseña y rol son obligatorios.");
            }

            if (!new[] { "Director", "Docente", "Estudiante" }.Contains(model.Rol))
            {
                return BadRequest("Rol inválido.");
            }

            using var context = Conexion;

            var existente = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo",
                new { Correo = model.CorreoElectronico });

            if (existente != null) return Conflict("Ya existe una cuenta registrada con ese correo.");

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Contrasenna);

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion)
                OUTPUT INSERTED.ConsecutivoUsuario
                VALUES (@Nombre, @Correo, @Contrasenna, @Rol, @Seccion)",
                new { model.Nombre, Correo = model.CorreoElectronico, Contrasenna = hash, model.Rol, model.Seccion });

            return Ok(new { Mensaje = "Usuario creado correctamente.", ConsecutivoUsuario = nuevoId });
        }

        // PUT api/Admin/CambiarEstadoUsuarioAPI/5  (activar / desactivar acceso)
        [HttpPut("CambiarEstadoUsuarioAPI/{id:int}")]
        public IActionResult CambiarEstadoUsuarioAPI(int id, CambiarEstadoUsuarioRequestModel model)
        {
            if (id == _utiles.ObtenerConsecutivoToken() && !model.Activo)
            {
                return BadRequest("No puede desactivar su propia cuenta.");
            }

            using var context = Conexion;
            var actualizados = context.Execute(
                "UPDATE Usuario SET Activo = @Activo WHERE ConsecutivoUsuario = @Id",
                new { model.Activo, Id = id });

            if (actualizados == 0) return NotFound("Usuario no encontrado.");

            return Ok(new { Mensaje = model.Activo ? "Usuario activado." : "Usuario desactivado." });
        }

        // PUT api/Admin/CambiarRolUsuarioAPI/5
        [HttpPut("CambiarRolUsuarioAPI/{id:int}")]
        public IActionResult CambiarRolUsuarioAPI(int id, CambiarRolUsuarioRequestModel model)
        {
            if (!new[] { "Director", "Docente", "Estudiante" }.Contains(model.Rol))
            {
                return BadRequest("Rol inválido.");
            }

            using var context = Conexion;
            var actualizados = context.Execute(
                "UPDATE Usuario SET Rol = @Rol, Seccion = @Seccion WHERE ConsecutivoUsuario = @Id",
                new { model.Rol, model.Seccion, Id = id });

            if (actualizados == 0) return NotFound("Usuario no encontrado.");

            return Ok(new { Mensaje = "Rol actualizado correctamente." });
        }

        // GET api/Admin/ConsultarSeccionesAPI  -> secciones/grupos distintos registrados
        [HttpGet("ConsultarSeccionesAPI")]
        public IActionResult ConsultarSeccionesAPI()
        {
            using var context = Conexion;

            var secciones = context.Query<string>(
                "SELECT DISTINCT Seccion FROM Usuario WHERE Seccion IS NOT NULL ORDER BY Seccion");

            return Ok(secciones);
        }

        // GET api/Admin/ConsultarErroresAPI  -> bitácora de errores no controlados (del middleware)
        [HttpGet("ConsultarErroresAPI")]
        public IActionResult ConsultarErroresAPI()
        {
            using var context = Conexion;

            var errores = context.Query(@"
                SELECT TOP 50 ConsecutivoError, Ruta, Mensaje, FechaCreacion
                FROM RegistroError ORDER BY FechaCreacion DESC");

            return Ok(errores);
        }
    }
}
