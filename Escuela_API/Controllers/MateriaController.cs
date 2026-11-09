using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MateriaController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Materia/ConsultarMateriasAPI
        // Director -> todas. Docente -> solo las propias. Estudiante -> las activas de su sección.
        [HttpGet("ConsultarMateriasAPI")]
        public IActionResult ConsultarMateriasAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            const string baseSql = @"
                SELECT m.ConsecutivoMateria, m.Nombre, m.Seccion, m.ConsecutivoDocente,
                       u.Nombre AS Docente, m.Activa, m.FechaCreacion
                FROM Materia m
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = m.ConsecutivoDocente ";

            IEnumerable<MateriaResponseModel> materias;

            if (rol == "Director")
            {
                materias = context.Query<MateriaResponseModel>(baseSql + "ORDER BY m.Activa DESC, m.Seccion, m.Nombre");
            }
            else if (rol == "Docente")
            {
                materias = context.Query<MateriaResponseModel>(
                    baseSql + "WHERE m.ConsecutivoDocente = @Id ORDER BY m.Activa DESC, m.Seccion, m.Nombre",
                    new { Id = _utiles.ObtenerConsecutivoToken() });
            }
            else
            {
                materias = context.Query<MateriaResponseModel>(
                    baseSql + "WHERE m.Seccion = @Seccion AND m.Activa = 1 ORDER BY m.Nombre",
                    new { Seccion = _utiles.ObtenerSeccionToken() });
            }

            return Ok(materias);
        }

        // POST api/Materia/RegistrarMateriaAPI  (Director o Docente)
        [Authorize(Roles = "Director,Docente")]
        [HttpPost("RegistrarMateriaAPI")]
        public IActionResult RegistrarMateriaAPI(RegistrarMateriaRequestModel model)
        {
            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);

            if (nombre.Length == 0 || string.IsNullOrWhiteSpace(model.Seccion))
            {
                return BadRequest("Nombre y sección son obligatorios.");
            }

            if (nombre.Length > 100) return BadRequest("El nombre de la asignatura es demasiado largo.");

            using var context = Conexion;

            var seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
            if (seccion == null) return BadRequest("Seleccione una sección válida de la lista.");

            int docenteId;
            if (_utiles.ObtenerRolToken() == "Director")
            {
                // El Director debe indicar a qué docente se le asigna la asignatura.
                if (model.ConsecutivoDocente == null) return BadRequest("Seleccione el docente a cargo.");

                var esDocenteActivo = context.QueryFirstOrDefault<int>(@"
                    SELECT COUNT(*) FROM Usuario
                    WHERE ConsecutivoUsuario = @Id AND Rol = 'Docente' AND Activo = 1",
                    new { Id = model.ConsecutivoDocente.Value }) > 0;

                if (!esDocenteActivo) return BadRequest("El docente seleccionado no existe o no está activo.");

                docenteId = model.ConsecutivoDocente.Value;
            }
            else
            {
                // El Docente solo puede crear asignaturas a su propio nombre.
                docenteId = _utiles.ObtenerConsecutivoToken();
            }

            if (ExisteAsignaturaActiva(context, nombre, seccion, null))
            {
                return Conflict($"Ya existe la asignatura {nombre} en {seccion}.");
            }

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Materia (Nombre, Seccion, ConsecutivoDocente)
                OUTPUT INSERTED.ConsecutivoMateria
                VALUES (@Nombre, @Seccion, @Docente)",
                new { Nombre = nombre, Seccion = seccion, Docente = docenteId });

            return Ok(new { Mensaje = "Asignatura registrada correctamente.", ConsecutivoMateria = nuevoId });
        }

        // PUT api/Materia/ActualizarMateriaAPI/5
        [Authorize(Roles = "Director,Docente")]
        [HttpPut("ActualizarMateriaAPI/{id:int}")]
        public IActionResult ActualizarMateriaAPI(int id, RegistrarMateriaRequestModel model)
        {
            using var context = Conexion;

            if (!TienePermisoSobreMateria(context, id)) return Forbid();

            var nombre = ValidacionHelper.LimpiarNombre(model.Nombre);
            if (nombre.Length == 0 || nombre.Length > 100) return BadRequest("El nombre de la asignatura no es válido.");

            var seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
            if (seccion == null) return BadRequest("Seleccione una sección válida de la lista.");

            if (ExisteAsignaturaActiva(context, nombre, seccion, id))
            {
                return Conflict($"Ya existe la asignatura {nombre} en {seccion}.");
            }

            context.Execute(@"
                UPDATE Materia SET Nombre = @Nombre, Seccion = @Seccion
                WHERE ConsecutivoMateria = @Id",
                new { Nombre = nombre, Seccion = seccion, Id = id });

            return Ok(new { Mensaje = "Asignatura actualizada correctamente." });
        }

        // DELETE api/Materia/EliminarMateriaAPI/5  (desactiva, no borra en duro)
        [Authorize(Roles = "Director,Docente")]
        [HttpDelete("EliminarMateriaAPI/{id:int}")]
        public IActionResult EliminarMateriaAPI(int id)
        {
            using var context = Conexion;

            if (!TienePermisoSobreMateria(context, id)) return Forbid();

            context.Execute("UPDATE Materia SET Activa = 0 WHERE ConsecutivoMateria = @Id", new { Id = id });

            return Ok(new { Mensaje = "Asignatura desactivada correctamente." });
        }

        // PUT api/Materia/ReactivarMateriaAPI/5
        [Authorize(Roles = "Director,Docente")]
        [HttpPut("ReactivarMateriaAPI/{id:int}")]
        public IActionResult ReactivarMateriaAPI(int id)
        {
            using var context = Conexion;

            if (!TienePermisoSobreMateria(context, id)) return Forbid();

            var materia = context.QueryFirstOrDefault<MateriaBasica>(@"
                SELECT ConsecutivoMateria, Nombre, Seccion, ConsecutivoDocente, Activa
                FROM Materia WHERE ConsecutivoMateria = @Id", new { Id = id });

            if (materia == null) return NotFound("Asignatura no encontrada.");

            if (ExisteAsignaturaActiva(context, materia.Nombre, materia.Seccion, id))
            {
                return Conflict($"Ya existe otra asignatura {materia.Nombre} activa en {materia.Seccion}.");
            }

            context.Execute("UPDATE Materia SET Activa = 1 WHERE ConsecutivoMateria = @Id", new { Id = id });

            return Ok(new { Mensaje = "Asignatura reactivada correctamente." });
        }

        private static bool ExisteAsignaturaActiva(SqlConnection context, string nombre, string seccion, int? excluirId)
        {
            return context.QueryFirstOrDefault<int>(@"
                SELECT COUNT(*) FROM Materia
                WHERE Nombre = @Nombre AND Seccion = @Seccion AND Activa = 1
                  AND (@Excluir IS NULL OR ConsecutivoMateria <> @Excluir)",
                new { Nombre = nombre, Seccion = seccion, Excluir = excluirId }) > 0;
        }

        private bool TienePermisoSobreMateria(SqlConnection context, int consecutivoMateria)
        {
            var rol = _utiles.ObtenerRolToken();
            if (rol == "Director") return true;

            var docenteDueno = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoDocente FROM Materia WHERE ConsecutivoMateria = @Id", new { Id = consecutivoMateria });

            return docenteDueno == _utiles.ObtenerConsecutivoToken();
        }
    }
}
