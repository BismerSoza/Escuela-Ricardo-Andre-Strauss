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
        // Director -> todas. Docente -> solo las propias. Estudiante -> las de su sección.
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
                materias = context.Query<MateriaResponseModel>(baseSql + "ORDER BY m.Seccion, m.Nombre");
            }
            else if (rol == "Docente")
            {
                materias = context.Query<MateriaResponseModel>(
                    baseSql + "WHERE m.ConsecutivoDocente = @Id ORDER BY m.Nombre",
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
            if (string.IsNullOrWhiteSpace(model.Nombre) || string.IsNullOrWhiteSpace(model.Seccion))
            {
                return BadRequest("Nombre y sección son obligatorios.");
            }

            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            // El Docente solo puede crear materias a su propio nombre.
            var docenteId = rol == "Director" && model.ConsecutivoDocente.HasValue
                ? model.ConsecutivoDocente.Value
                : _utiles.ObtenerConsecutivoToken();

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Materia (Nombre, Seccion, ConsecutivoDocente)
                OUTPUT INSERTED.ConsecutivoMateria
                VALUES (@Nombre, @Seccion, @Docente)",
                new { model.Nombre, model.Seccion, Docente = docenteId });

            return Ok(new { Mensaje = "Asignatura registrada correctamente.", ConsecutivoMateria = nuevoId });
        }

        // PUT api/Materia/ActualizarMateriaAPI/5
        [Authorize(Roles = "Director,Docente")]
        [HttpPut("ActualizarMateriaAPI/{id:int}")]
        public IActionResult ActualizarMateriaAPI(int id, RegistrarMateriaRequestModel model)
        {
            using var context = Conexion;

            if (!TienePermisoSobreMateria(context, id))
            {
                return Forbid();
            }

            context.Execute(@"
                UPDATE Materia SET Nombre = @Nombre, Seccion = @Seccion
                WHERE ConsecutivoMateria = @Id",
                new { model.Nombre, model.Seccion, Id = id });

            return Ok(new { Mensaje = "Asignatura actualizada correctamente." });
        }

        // DELETE api/Materia/EliminarMateriaAPI/5  (desactiva, no borra en duro)
        [Authorize(Roles = "Director,Docente")]
        [HttpDelete("EliminarMateriaAPI/{id:int}")]
        public IActionResult EliminarMateriaAPI(int id)
        {
            using var context = Conexion;

            if (!TienePermisoSobreMateria(context, id))
            {
                return Forbid();
            }

            context.Execute("UPDATE Materia SET Activa = 0 WHERE ConsecutivoMateria = @Id", new { Id = id });

            return Ok(new { Mensaje = "Asignatura desactivada correctamente." });
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
