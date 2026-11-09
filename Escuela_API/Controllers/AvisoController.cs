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
        // Director   -> todos los avisos.
        // Docente    -> generales + los de las secciones donde dicta clases + los que él publicó.
        // Estudiante -> generales + los de su sección.
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

            IEnumerable<AvisoResponseModel> avisos;

            if (rol == "Director")
            {
                avisos = context.Query<AvisoResponseModel>(baseSql + "ORDER BY a.FechaCreacion DESC");
            }
            else if (rol == "Docente")
            {
                avisos = context.Query<AvisoResponseModel>(baseSql + @"
                    WHERE a.Seccion IS NULL OR a.ConsecutivoAutor = @Id
                       OR a.Seccion IN (SELECT Seccion FROM Materia WHERE ConsecutivoDocente = @Id AND Activa = 1)
                    ORDER BY a.FechaCreacion DESC",
                    new { Id = _utiles.ObtenerConsecutivoToken() });
            }
            else
            {
                avisos = context.Query<AvisoResponseModel>(
                    baseSql + "WHERE a.Seccion IS NULL OR a.Seccion = @Seccion ORDER BY a.FechaCreacion DESC",
                    new { Seccion = _utiles.ObtenerSeccionToken() });
            }

            return Ok(avisos);
        }

        // POST api/Aviso/RegistrarAvisoAPI  (Director o Docente)
        // Director -> a toda la institución (sin sección) o a una sección.
        // Docente  -> obligatoriamente a una sección donde dicta clases.
        [Authorize(Roles = "Director,Docente")]
        [HttpPost("RegistrarAvisoAPI")]
        public IActionResult RegistrarAvisoAPI(RegistrarAvisoRequestModel model)
        {
            var titulo = (model.Titulo ?? string.Empty).Trim();
            var contenido = (model.Contenido ?? string.Empty).Trim();

            if (titulo.Length == 0 || contenido.Length == 0)
            {
                return BadRequest("Título y contenido son obligatorios.");
            }

            if (titulo.Length > 200) return BadRequest("El título no puede superar 200 caracteres.");

            var prioridad = new[] { "Importante", "Información", "General" }.Contains(model.Prioridad)
                ? model.Prioridad : "General";

            using var context = Conexion;
            var esDirector = _utiles.ObtenerRolToken() == "Director";

            string? seccion = null;
            if (!string.IsNullOrWhiteSpace(model.Seccion))
            {
                seccion = AccesoHelper.NombreSeccionValido(context, model.Seccion);
                if (seccion == null) return BadRequest("Seleccione una sección válida.");

                if (!esDirector && !AccesoHelper.DocenteDictaSeccion(context, _utiles.ObtenerConsecutivoToken(), seccion))
                {
                    return BadRequest("Solo puede publicar avisos en secciones donde dicta clases.");
                }
            }
            else if (!esDirector)
            {
                return BadRequest("Seleccione la sección a la que va dirigido el aviso.");
            }

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Aviso (Titulo, Contenido, Prioridad, ConsecutivoAutor, Seccion)
                OUTPUT INSERTED.ConsecutivoAviso
                VALUES (@Titulo, @Contenido, @Prioridad, @Autor, @Seccion)",
                new { Titulo = titulo, Contenido = contenido, Prioridad = prioridad, Autor = _utiles.ObtenerConsecutivoToken(), Seccion = seccion });

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
