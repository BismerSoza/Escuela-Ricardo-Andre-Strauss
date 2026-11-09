using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace Escuela_API.Controllers
{
    // Horario semanal de clases.
    //
    // REGLA ANTI-CHOQUE: dos bloques no pueden solaparse en el mismo día si comparten
    //   1) el AULA            (no se puede usar el mismo espacio a la vez),
    //   2) el DOCENTE         (nadie puede dar dos clases a la vez),
    //   3) la SECCIÓN         (un grupo no puede tener dos materias a la vez).
    // Dos bloques se solapan cuando  inicioA < finB  Y  finA > inicioB
    // (por eso 07:00-08:00 y 08:00-09:00 NO chocan: pueden ir seguidos).
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HorarioController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private static readonly string[] Dias = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes" };
        private static readonly Regex FormatoHora = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

        private const string HoraMinima = "06:00";
        private const string HoraMaxima = "18:00";
        private const int DuracionMinimaMinutos = 20;

        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Horario/ConsultarHorarioAPI?tipo=seccion&valor=Sexto Grado
        //   tipo = mio | seccion | aula | docente   (valor: nombre de sección, nombre de aula o id de docente)
        // Estudiante -> siempre el horario de su sección (ignora los filtros).
        // Docente    -> por defecto el propio; puede consultar cualquier aula o sección para coordinar.
        // Director   -> sin filtro devuelve todo; normalmente se consulta por sección, aula o docente.
        [HttpGet("ConsultarHorarioAPI")]
        public IActionResult ConsultarHorarioAPI(string? tipo, string? valor)
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            var sql = new StringBuilder(@"
                SELECT h.ConsecutivoHorario, h.ConsecutivoMateria, m.Nombre AS Materia, m.Seccion,
                       m.ConsecutivoDocente, u.Nombre AS Docente,
                       h.DiaSemana, h.HoraInicio, h.HoraFin, h.ConsecutivoAula, a.Nombre AS Aula
                FROM Horario h
                INNER JOIN Materia m ON m.ConsecutivoMateria = h.ConsecutivoMateria
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = m.ConsecutivoDocente
                LEFT JOIN Aula a ON a.ConsecutivoAula = h.ConsecutivoAula
                WHERE m.Activa = 1 ");

            var parametros = new DynamicParameters();

            if (rol == "Estudiante")
            {
                sql.Append("AND m.Seccion = @Seccion ");
                parametros.Add("Seccion", _utiles.ObtenerSeccionToken());
            }
            else
            {
                switch ((tipo ?? string.Empty).ToLowerInvariant())
                {
                    case "seccion":
                        sql.Append("AND m.Seccion = @Valor ");
                        parametros.Add("Valor", valor);
                        break;
                    case "aula":
                        sql.Append("AND a.Nombre = @Valor ");
                        parametros.Add("Valor", valor);
                        break;
                    case "docente":
                        if (!int.TryParse(valor, out var docenteId)) return BadRequest("Docente inválido.");
                        sql.Append("AND m.ConsecutivoDocente = @DocenteId ");
                        parametros.Add("DocenteId", docenteId);
                        break;
                    case "mio":
                        sql.Append("AND m.ConsecutivoDocente = @Yo ");
                        parametros.Add("Yo", _utiles.ObtenerConsecutivoToken());
                        break;
                    default:
                        if (rol == "Docente")
                        {
                            sql.Append("AND m.ConsecutivoDocente = @Yo ");
                            parametros.Add("Yo", _utiles.ObtenerConsecutivoToken());
                        }
                        break;
                }
            }

            sql.Append(@"ORDER BY CASE h.DiaSemana WHEN 'Lunes' THEN 1 WHEN 'Martes' THEN 2 WHEN 'Miércoles' THEN 3
                                                   WHEN 'Jueves' THEN 4 ELSE 5 END, h.HoraInicio");

            return Ok(context.Query<HorarioResponseModel>(sql.ToString(), parametros));
        }

        // GET api/Horario/ConsultarAulasAPI  -> aulas activas (el Director puede pedir también las inactivas)
        [HttpGet("ConsultarAulasAPI")]
        public IActionResult ConsultarAulasAPI(bool incluirInactivas = false)
        {
            using var context = Conexion;
            var todas = incluirInactivas && _utiles.ObtenerRolToken() == "Director";

            var aulas = context.Query<AulaResponseModel>(@"
                SELECT ConsecutivoAula, Nombre, Activa, 0 AS TotalBloques
                FROM Aula
                WHERE (@Todas = 1 OR Activa = 1)
                ORDER BY Nombre", new { Todas = todas ? 1 : 0 });

            return Ok(aulas);
        }

        // GET api/Horario/VerificarDisponibilidadAPI?consecutivoMateria=1&diaSemana=Lunes&horaInicio=07:00&horaFin=08:00&consecutivoAula=3
        // Lo usa la pantalla "Agregar bloque" para avisar EN VIVO si el espacio está libre.
        [Authorize(Roles = "Director,Docente")]
        [HttpGet("VerificarDisponibilidadAPI")]
        public IActionResult VerificarDisponibilidadAPI([FromQuery] RegistrarHorarioRequestModel model)
        {
            using var context = Conexion;

            var materia = ObtenerMateriaGestionable(context, model.ConsecutivoMateria, out var errorMateria);
            if (materia == null) return errorMateria!;

            var errorFormato = ValidarBloque(model.DiaSemana, model.HoraInicio, model.HoraFin);
            if (errorFormato != null)
            {
                return Ok(new DisponibilidadResponseModel { Disponible = false, Conflictos = new List<string> { errorFormato } });
            }

            var conflictos = BuscarConflictos(context, null, materia, model);

            return Ok(new DisponibilidadResponseModel { Disponible = conflictos.Count == 0, Conflictos = conflictos });
        }

        // POST api/Horario/RegistrarHorarioAPI  (Director o Docente dueño de la materia)
        [Authorize(Roles = "Director,Docente")]
        [HttpPost("RegistrarHorarioAPI")]
        public IActionResult RegistrarHorarioAPI(RegistrarHorarioRequestModel model)
        {
            using var context = Conexion;

            var materia = ObtenerMateriaGestionable(context, model.ConsecutivoMateria, out var errorMateria);
            if (materia == null) return errorMateria!;

            var errorFormato = ValidarBloque(model.DiaSemana, model.HoraInicio, model.HoraFin);
            if (errorFormato != null) return BadRequest(errorFormato);

            if (model.ConsecutivoAula == null) return BadRequest("Seleccione el aula del bloque.");

            var aulaActiva = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Aula WHERE ConsecutivoAula = @Id AND Activa = 1",
                new { Id = model.ConsecutivoAula.Value });

            if (aulaActiva == 0) return BadRequest("El aula seleccionada no existe o está desactivada.");

            // Verificar + insertar dentro de una transacción SERIALIZABLE: si dos personas
            // intentan reservar el mismo espacio al mismo tiempo, solo una lo logra.
            context.Open();
            using var tx = context.BeginTransaction(IsolationLevel.Serializable);

            var conflictos = BuscarConflictos(context, tx, materia, model);
            if (conflictos.Count > 0)
            {
                tx.Rollback();
                return Conflict(string.Join(" ", conflictos));
            }

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Horario (ConsecutivoMateria, DiaSemana, HoraInicio, HoraFin, ConsecutivoAula)
                OUTPUT INSERTED.ConsecutivoHorario
                VALUES (@ConsecutivoMateria, @DiaSemana, @HoraInicio, @HoraFin, @ConsecutivoAula)",
                new
                {
                    model.ConsecutivoMateria,
                    model.DiaSemana,
                    model.HoraInicio,
                    model.HoraFin,
                    model.ConsecutivoAula
                }, tx);

            tx.Commit();

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

        // ---------------------------------------------------------------------
        //  Validaciones internas
        // ---------------------------------------------------------------------

        private static string? ValidarBloque(string dia, string inicio, string fin)
        {
            if (!Dias.Contains(dia)) return "Seleccione un día válido (de lunes a viernes).";

            if (!FormatoHora.IsMatch(inicio ?? string.Empty) || !FormatoHora.IsMatch(fin ?? string.Empty))
            {
                return "Las horas deben tener el formato HH:mm (por ejemplo 07:00).";
            }

            if (string.CompareOrdinal(inicio, fin) >= 0) return "La hora de fin debe ser posterior a la hora de inicio.";

            if (string.CompareOrdinal(inicio, HoraMinima) < 0 || string.CompareOrdinal(fin, HoraMaxima) > 0)
            {
                return $"El horario lectivo va de {HoraMinima} a {HoraMaxima}.";
            }

            var minutos = (TimeSpan.Parse(fin!) - TimeSpan.Parse(inicio!)).TotalMinutes;
            if (minutos < DuracionMinimaMinutos) return $"Cada bloque debe durar al menos {DuracionMinimaMinutos} minutos.";

            return null;
        }

        // Devuelve la lista de choques en lenguaje claro (vacía = el espacio está libre).
        private static List<string> BuscarConflictos(SqlConnection context, IDbTransaction? tx, MateriaBasica materia, RegistrarHorarioRequestModel bloque)
        {
            var conflictos = new List<string>();

            const string baseSql = @"
                SELECT m.Nombre AS Materia, m.Seccion, u.Nombre AS Docente, h.HoraInicio, h.HoraFin, a.Nombre AS Aula
                FROM Horario h
                INNER JOIN Materia m ON m.ConsecutivoMateria = h.ConsecutivoMateria
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = m.ConsecutivoDocente
                LEFT JOIN Aula a ON a.ConsecutivoAula = h.ConsecutivoAula
                WHERE m.Activa = 1 AND h.DiaSemana = @Dia
                  AND h.HoraInicio < @Fin AND h.HoraFin > @Inicio ";

            var parametros = new
            {
                Dia = bloque.DiaSemana,
                Inicio = bloque.HoraInicio,
                Fin = bloque.HoraFin,
                Aula = bloque.ConsecutivoAula,
                Docente = materia.ConsecutivoDocente,
                materia.Seccion
            };

            if (bloque.ConsecutivoAula != null)
            {
                var porAula = context.Query<ChoqueFila>(baseSql + "AND h.ConsecutivoAula = @Aula", parametros, tx).ToList();
                foreach (var c in porAula)
                {
                    conflictos.Add($"El espacio «{c.Aula}» ya está ocupado el {bloque.DiaSemana} de {c.HoraInicio} a {c.HoraFin} por {c.Materia} ({c.Seccion}).");
                }
            }

            var porDocente = context.Query<ChoqueFila>(baseSql + "AND m.ConsecutivoDocente = @Docente", parametros, tx).ToList();
            foreach (var c in porDocente)
            {
                conflictos.Add($"{c.Docente} ya tiene clase el {bloque.DiaSemana} de {c.HoraInicio} a {c.HoraFin} ({c.Materia}, {c.Seccion}).");
            }

            var porSeccion = context.Query<ChoqueFila>(baseSql + "AND m.Seccion = @Seccion", parametros, tx).ToList();
            foreach (var c in porSeccion)
            {
                conflictos.Add($"{materia.Seccion} ya tiene {c.Materia} el {bloque.DiaSemana} de {c.HoraInicio} a {c.HoraFin}.");
            }

            return conflictos;
        }

        // Carga la asignatura y comprueba que exista, esté activa y que el usuario pueda gestionarla.
        private MateriaBasica? ObtenerMateriaGestionable(SqlConnection context, int consecutivoMateria, out IActionResult? error)
        {
            error = null;

            var materia = context.QueryFirstOrDefault<MateriaBasica>(@"
                SELECT ConsecutivoMateria, Nombre, Seccion, ConsecutivoDocente, Activa
                FROM Materia WHERE ConsecutivoMateria = @Id", new { Id = consecutivoMateria });

            if (materia == null || !materia.Activa)
            {
                error = BadRequest("Seleccione una asignatura activa.");
                return null;
            }

            if (_utiles.ObtenerRolToken() != "Director" && materia.ConsecutivoDocente != _utiles.ObtenerConsecutivoToken())
            {
                error = Forbid();
                return null;
            }

            return materia;
        }

        private bool PuedeGestionar(SqlConnection context, int consecutivoMateria)
        {
            if (_utiles.ObtenerRolToken() == "Director") return true;

            var docenteDueno = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoDocente FROM Materia WHERE ConsecutivoMateria = @Id", new { Id = consecutivoMateria });

            return docenteDueno == _utiles.ObtenerConsecutivoToken();
        }

        private class ChoqueFila
        {
            public string Materia { get; set; } = string.Empty;
            public string Seccion { get; set; } = string.Empty;
            public string Docente { get; set; } = string.Empty;
            public string HoraInicio { get; set; } = string.Empty;
            public string HoraFin { get; set; } = string.Empty;
            public string? Aula { get; set; }
        }
    }
}
