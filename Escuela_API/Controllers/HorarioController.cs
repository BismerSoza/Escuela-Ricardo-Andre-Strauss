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
    public class HorarioController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Horario/ConsultarHorarioAPI
        [HttpGet("ConsultarHorarioAPI")]
        public IActionResult ConsultarHorarioAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            const string baseSql = @"
                SELECT h.ConsecutivoHorario, h.ConsecutivoMateria, m.Nombre AS Materia,
                       h.DiaSemana, h.HoraInicio, h.HoraFin, h.Aula
                FROM Horario h
                INNER JOIN Materia m ON m.ConsecutivoMateria = h.ConsecutivoMateria ";

            IEnumerable<HorarioResponseModel> horario;

            if (rol == "Director")
            {
                horario = context.Query<HorarioResponseModel>(baseSql + "ORDER BY h.DiaSemana, h.HoraInicio");
            }
            else if (rol == "Docente")
            {
                horario = context.Query<HorarioResponseModel>(
                    baseSql + "WHERE m.ConsecutivoDocente = @Id ORDER BY h.DiaSemana, h.HoraInicio",
                    new { Id = _utiles.ObtenerConsecutivoToken() });
            }
            else
            {
                horario = context.Query<HorarioResponseModel>(
                    baseSql + "WHERE m.Seccion = @Seccion ORDER BY h.DiaSemana, h.HoraInicio",
                    new { Seccion = _utiles.ObtenerSeccionToken() });
            }

            return Ok(horario);
        }

        // POST api/Horario/RegistrarHorarioAPI  (Director o Docente dueño de la materia)
        [Authorize(Roles = "Director,Docente")]
        [HttpPost("RegistrarHorarioAPI")]
        public IActionResult RegistrarHorarioAPI(RegistrarHorarioRequestModel model)
        {
            using var context = Conexion;

            if (!PuedeGestionar(context, model.ConsecutivoMateria)) return Forbid();

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Horario (ConsecutivoMateria, DiaSemana, HoraInicio, HoraFin, Aula)
                OUTPUT INSERTED.ConsecutivoHorario
                VALUES (@ConsecutivoMateria, @DiaSemana, @HoraInicio, @HoraFin, @Aula)", model);

            return Ok(new { Mensaje = "Bloque de horario agregado.", ConsecutivoHorario = nuevoId });
        }

        // DELETE api/Horario/EliminarHorarioAPI/5
        [Authorize(Roles = "Director,Docente")]
        [HttpDelete("EliminarHorarioAPI/{id:int}")]
        public IActionResult EliminarHorarioAPI(int id)
        {
            using var context = Conexion;

            var materiaId = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoMateria FROM Horario WHERE ConsecutivoHorario = @Id", new { Id = id });

            if (materiaId == null) return NotFound("Bloque de horario no encontrado.");
            if (!PuedeGestionar(context, materiaId.Value)) return Forbid();

            context.Execute("DELETE FROM Horario WHERE ConsecutivoHorario = @Id", new { Id = id });
            return Ok(new { Mensaje = "Bloque de horario eliminado." });
        }

        private bool PuedeGestionar(SqlConnection context, int consecutivoMateria)
        {
            if (_utiles.ObtenerRolToken() == "Director") return true;

            var docenteDueno = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoDocente FROM Materia WHERE ConsecutivoMateria = @Id", new { Id = consecutivoMateria });

            return docenteDueno == _utiles.ObtenerConsecutivoToken();
        }
    }
}
