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
    public class AvisoController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Aviso/ConsultarAvisosAPI
        // Director -> ve todos los avisos de la institucion.
        // Docente/Estudiante -> ven los generales (Seccion NULL) mas los de su propia seccion.
        [HttpGet("ConsultarAvisosAPI")]
        public IActionResult ConsultarAvisosAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            const string baseSql = @"
                SELECT a.ConsecutivoAviso, a.Titulo, a.Contenido, a.Prioridad, a.Seccion, a.FechaCreacion,
                       a.ConsecutivoAutor, u.Nombre AS Autor
                FROM Aviso a
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = a.ConsecutivoAutor ";

            IEnumerable<AvisoResponseModel> avisos = rol == "Director"
                ? context.Query<AvisoResponseModel>(baseSql + "ORDER BY a.FechaCreacion DESC")
                : context.Query<AvisoResponseModel>(
                    baseSql + "WHERE a.Seccion IS NULL OR a.Seccion = @Seccion ORDER BY a.FechaCreacion DESC",
                    new { Seccion = _utiles.ObtenerSeccionToken() });

            return Ok(avisos);
        }

        // POST api/Aviso/RegistrarAvisoAPI  (Director o Docente)
        [Authorize(Roles = "Director,Docente")]
        [HttpPost("RegistrarAvisoAPI")]
        public IActionResult RegistrarAvisoAPI(RegistrarAvisoRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Titulo) || string.IsNullOrWhiteSpace(model.Contenido))
            {
                return BadRequest("Título y contenido son obligatorios.");
            }

            var prioridad = new[] { "Importante", "Información", "General" }.Contains(model.Prioridad)
                ? model.Prioridad : "General";

            using var context = Conexion;

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Aviso (Titulo, Contenido, Prioridad, ConsecutivoAutor, Seccion)
                OUTPUT INSERTED.ConsecutivoAviso
                VALUES (@Titulo, @Contenido, @Prioridad, @Autor, @Seccion)",
                new { model.Titulo, model.Contenido, Prioridad = prioridad, Autor = _utiles.ObtenerConsecutivoToken(), model.Seccion });

            return Ok(new { Mensaje = "Aviso publicado correctamente.", ConsecutivoAviso = nuevoId });
        }

        // DELETE api/Aviso/EliminarAvisoAPI/5  (el autor, o el Director sobre cualquier aviso)
        [Authorize(Roles = "Director,Docente")]
        [HttpDelete("EliminarAvisoAPI/{id:int}")]
        public IActionResult EliminarAvisoAPI(int id)
        {
            using var context = Conexion;

            var autor = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoAutor FROM Aviso WHERE ConsecutivoAviso = @Id", new { Id = id });

            if (autor == null) return NotFound("Aviso no encontrado.");

            var esDirector = _utiles.ObtenerRolToken() == "Director";
            if (!esDirector && autor != _utiles.ObtenerConsecutivoToken()) return Forbid();

            context.Execute("DELETE FROM Aviso WHERE ConsecutivoAviso = @Id", new { Id = id });

            return Ok(new { Mensaje = "Aviso eliminado correctamente." });
        }
    }
}
