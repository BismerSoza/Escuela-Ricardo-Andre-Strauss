using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    // Todo este controlador es exclusivo del rol Director: administración
    // institucional de usuarios, secciones y aulas. Ni Docente ni Estudiante
    // pueden acceder a ninguna de estas rutas.
    [Authorize(Roles = "Director")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private static readonly string[] RolesValidos = { "Director", "Docente", "Estudiante" };

        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // =====================================================================
        //  USUARIOS
        // =====================================================================

        // GET api/Admin/ConsultarUsuariosAPI?rol=Docente
        [HttpGet("ConsultarUsuariosAPI")]
        public IActionResult ConsultarUsuariosAPI(string? rol)
        {
            using var context = Conexion;

            var usuarios = context.Query<UsuarioAdminResponseModel>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion, Activo, Pendiente,
                       ClaveSolicitada, FechaCreacion
                FROM Usuario
                WHERE (@Rol IS NULL OR Rol = @Rol)
                ORDER BY Pendiente DESC, Rol, Nombre", new { Rol = string.IsNullOrWhiteSpace(rol) ? null : rol });

            return Ok(usuarios);
        }

        // POST api/Admin/RegistrarUsuarioAPI  (el Director puede crear cuentas de cualquier rol, incluyendo otro Director)
        // Las cuentas creadas por el Director quedan activas de inmediato.
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
            if (!RolesValidos.Contains(model.Rol)) return BadRequest("Rol inválido.");

            if (!ValidacionHelper.CorreoValido(model.CorreoElectronico, out var correo))
            {
                return BadRequest("Ingrese un correo electrónico válido.");
            }

            if (model.Contrasenna.Length < 6) return BadRequest("La contraseña debe tener al menos 6 caracteres.");

            using var context = Conexion;

            var existente = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo",
                new { Correo = correo });

            if (existente != null) return Conflict("Ya existe una cuenta registrada con ese correo.");

            // Solo el Estudiante tiene sección. Para Docente y Director se ignora cualquier valor recibido.
            string? seccion = null;
            if (model.Rol == "Estudiante")
            {
                seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
                if (seccion == null) return BadRequest("Seleccione la sección del estudiante.");
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Contrasenna);

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion)
                OUTPUT INSERTED.ConsecutivoUsuario
                VALUES (@Nombre, @Correo, @Contrasenna, @Rol, @Seccion)",
                new { Nombre = nombre, Correo = correo, Contrasenna = hash, model.Rol, Seccion = seccion });

            return Ok(new { Mensaje = "Usuario creado correctamente.", ConsecutivoUsuario = nuevoId });
        }

        // PUT api/Admin/CambiarEstadoUsuarioAPI/5  (activar / desactivar acceso)
        // Al ACTIVAR una cuenta también se quita la marca "Pendiente de aprobación".
        [HttpPut("CambiarEstadoUsuarioAPI/{id:int}")]
        public IActionResult CambiarEstadoUsuarioAPI(int id, CambiarEstadoUsuarioRequestModel model)
        {
            if (id == _utiles.ObtenerConsecutivoToken() && !model.Activo)
            {
                return BadRequest("No puede desactivar su propia cuenta.");
            }

            using var context = Conexion;
            var actualizados = context.Execute(@"
                UPDATE Usuario
                SET Activo = @Activo,
                    Pendiente = CASE WHEN @Activo = 1 THEN 0 ELSE Pendiente END
                WHERE ConsecutivoUsuario = @Id",
                new { model.Activo, Id = id });

            if (actualizados == 0) return NotFound("Usuario no encontrado.");

            return Ok(new { Mensaje = model.Activo ? "Usuario activado." : "Usuario desactivado." });
        }

        // PUT api/Admin/CambiarRolUsuarioAPI/5
        [HttpPut("CambiarRolUsuarioAPI/{id:int}")]
        public IActionResult CambiarRolUsuarioAPI(int id, CambiarRolUsuarioRequestModel model)
        {
            if (!RolesValidos.Contains(model.Rol)) return BadRequest("Rol inválido.");

            if (id == _utiles.ObtenerConsecutivoToken() && model.Rol != "Director")
            {
                return BadRequest("No puede cambiar su propio rol.");
            }

            using var context = Conexion;

            var actual = context.QueryFirstOrDefault<string?>(
                "SELECT Rol FROM Usuario WHERE ConsecutivoUsuario = @Id", new { Id = id });

            if (actual == null) return NotFound("Usuario no encontrado.");

            // Un docente con asignaturas activas no puede dejar de serlo sin reasignarlas antes.
            if (actual == "Docente" && model.Rol != "Docente")
            {
                var materias = context.QueryFirstOrDefault<int>(
                    "SELECT COUNT(*) FROM Materia WHERE ConsecutivoDocente = @Id AND Activa = 1", new { Id = id });

                if (materias > 0)
                {
                    return BadRequest($"El docente tiene {materias} asignatura(s) activa(s). Desactívelas o reasígnelas antes de cambiar su rol.");
                }
            }

            string? seccion = null;
            if (model.Rol == "Estudiante")
            {
                seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
                if (seccion == null) return BadRequest("Seleccione la sección del estudiante.");
            }

            context.Execute("UPDATE Usuario SET Rol = @Rol, Seccion = @Seccion WHERE ConsecutivoUsuario = @Id",
                new { model.Rol, Seccion = seccion, Id = id });

            return Ok(new { Mensaje = "Usuario actualizado correctamente." });
        }

        // PUT api/Admin/RestablecerContrasennaAPI/5
        // Genera una contraseña temporal y la devuelve UNA sola vez al Director, quien se
        // la entrega al usuario. Es el reemplazo seguro de la antigua "recuperación" pública.
        [HttpPut("RestablecerContrasennaAPI/{id:int}")]
        public IActionResult RestablecerContrasennaAPI(int id)
        {
            using var context = Conexion;

            var nombre = context.QueryFirstOrDefault<string?>(
                "SELECT Nombre FROM Usuario WHERE ConsecutivoUsuario = @Id", new { Id = id });

            if (nombre == null) return NotFound("Usuario no encontrado.");

            var clave = _utiles.GenerarContrasennaTemporal();
            var hash = BCrypt.Net.BCrypt.HashPassword(clave);

            context.Execute(
                "UPDATE Usuario SET Contrasenna = @Hash, ClaveSolicitada = NULL WHERE ConsecutivoUsuario = @Id",
                new { Hash = hash, Id = id });

            return Ok(new { Mensaje = "Contraseña restablecida.", Nombre = nombre, ContrasennaTemporal = clave });
        }

        // =====================================================================
        //  SECCIONES (catálogo de grados)
        // =====================================================================

        // GET api/Admin/ConsultarSeccionesAPI  -> todas, con conteos
        [HttpGet("ConsultarSeccionesAPI")]
        public IActionResult ConsultarSeccionesAPI()
        {
            using var context = Conexion;

            var secciones = context.Query<SeccionResponseModel>(@"
                SELECT s.ConsecutivoSeccion, s.Nombre, s.Activa,
                       (SELECT COUNT(*) FROM Usuario u
                         WHERE u.Seccion = s.Nombre AND u.Rol = 'Estudiante' AND u.Activo = 1) AS TotalEstudiantes,
                       (SELECT COUNT(*) FROM Materia m
                         WHERE m.Seccion = s.Nombre AND m.Activa = 1) AS TotalMaterias
                FROM Seccion s
                ORDER BY s.Activa DESC, s.Nombre");

            return Ok(secciones);
        }

        // POST api/Admin/RegistrarSeccionAPI
        [HttpPost("RegistrarSeccionAPI")]
        public IActionResult RegistrarSeccionAPI(RegistrarSeccionRequestModel model)
        {
            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);

            if (nombre.Length == 0) return BadRequest("El nombre de la sección es obligatorio.");
            if (nombre.Length > 80) return BadRequest("El nombre de la sección es demasiado largo (máximo 80 caracteres).");

            using var context = Conexion;

            var existe = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Seccion WHERE Nombre = @Nombre", new { Nombre = nombre });

            if (existe > 0) return Conflict("Ya existe una sección con ese nombre.");

            context.Execute("INSERT INTO Seccion (Nombre) VALUES (@Nombre)", new { Nombre = nombre });

            return Ok(new { Mensaje = "Sección creada correctamente." });
        }

        // PUT api/Admin/CambiarEstadoSeccionAPI/5
        // Una sección inactiva deja de ofrecerse al registrar estudiantes/asignaturas/avisos nuevos;
        // los datos que ya existen en ella se conservan.
        [HttpPut("CambiarEstadoSeccionAPI/{id:int}")]
        public IActionResult CambiarEstadoSeccionAPI(int id, CambiarEstadoCatalogoRequestModel model)
        {
            using var context = Conexion;

            var actualizados = context.Execute(
                "UPDATE Seccion SET Activa = @Activa WHERE ConsecutivoSeccion = @Id", new { model.Activa, Id = id });

            if (actualizados == 0) return NotFound("Sección no encontrada.");

            return Ok(new { Mensaje = model.Activa ? "Sección activada." : "Sección desactivada." });
        }

        // =====================================================================
        //  AULAS (catálogo usado por el calendario de horarios)
        // =====================================================================

        // GET api/Admin/ConsultarAulasAPI  -> todas, con cantidad de bloques de horario
        [HttpGet("ConsultarAulasAPI")]
        public IActionResult ConsultarAulasAPI()
        {
            using var context = Conexion;

            var aulas = context.Query<AulaResponseModel>(@"
                SELECT a.ConsecutivoAula, a.Nombre, a.Activa,
                       (SELECT COUNT(*) FROM Horario h
                         INNER JOIN Materia m ON m.ConsecutivoMateria = h.ConsecutivoMateria
                         WHERE h.ConsecutivoAula = a.ConsecutivoAula AND m.Activa = 1) AS TotalBloques
                FROM Aula a
                ORDER BY a.Activa DESC, a.Nombre");

            return Ok(aulas);
        }

        // POST api/Admin/RegistrarAulaAPI
        [HttpPost("RegistrarAulaAPI")]
        public IActionResult RegistrarAulaAPI(RegistrarAulaRequestModel model)
        {
            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);

            if (nombre.Length == 0) return BadRequest("El nombre del aula es obligatorio.");
            if (nombre.Length > 50) return BadRequest("El nombre del aula es demasiado largo (máximo 50 caracteres).");

            using var context = Conexion;

            var existe = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Aula WHERE Nombre = @Nombre", new { Nombre = nombre });

            if (existe > 0) return Conflict("Ya existe un aula con ese nombre.");

            context.Execute("INSERT INTO Aula (Nombre) VALUES (@Nombre)", new { Nombre = nombre });

            return Ok(new { Mensaje = "Aula creada correctamente." });
        }

        // PUT api/Admin/CambiarEstadoAulaAPI/5
        [HttpPut("CambiarEstadoAulaAPI/{id:int}")]
        public IActionResult CambiarEstadoAulaAPI(int id, CambiarEstadoCatalogoRequestModel model)
        {
            using var context = Conexion;

            var actualizados = context.Execute(
                "UPDATE Aula SET Activa = @Activa WHERE ConsecutivoAula = @Id", new { model.Activa, Id = id });

            if (actualizados == 0) return NotFound("Aula no encontrada.");

            return Ok(new { Mensaje = model.Activa ? "Aula activada." : "Aula desactivada." });
        }

        // =====================================================================
        //  BITÁCORA
        // =====================================================================

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
