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
    public class AsistenciaController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Asistencia/ConsultarPorMateriaAPI?materiaId=1&fecha=2026-05-04
        // Uso: Docente/Director pasan lista de estudiantes para marcar asistencia de un dia.
        [Authorize(Roles = "Director,Docente")]
        [HttpGet("ConsultarPorMateriaAPI")]
        public IActionResult ConsultarPorMateriaAPI(int materiaId, DateTime fecha)
        {
            using var context = Conexion;

            var estudiantes = context.Query(@"
                SELECT u.ConsecutivoUsuario, u.Nombre,
                       a.ConsecutivoAsistencia, a.Estado, a.Observacion
                FROM Usuario u
                INNER JOIN Materia m ON m.Seccion = u.Seccion
                LEFT JOIN Asistencia a ON a.ConsecutivoEstudiante = u.ConsecutivoUsuario
                    AND a.ConsecutivoMateria = @MateriaId AND a.Fecha = @Fecha
                WHERE u.Rol = 'Estudiante' AND m.ConsecutivoMateria = @MateriaId
                ORDER BY u.Nombre",
                new { MateriaId = materiaId, Fecha = fecha.Date });

            return Ok(estudiantes);
        }

        // POST api/Asistencia/RegistrarLoteAPI  (Docente marca asistencia de todo un grupo en una fecha)
        [Authorize(Roles = "Docente")]
        [HttpPost("RegistrarLoteAPI")]
        public IActionResult RegistrarLoteAPI(RegistrarAsistenciaLoteRequestModel model)
        {
            if (model.Registros.Count == 0)
            {
                return BadRequest("Debe incluir al menos un registro de asistencia.");
            }

            using var context = Conexion;
            var docenteId = _utiles.ObtenerConsecutivoToken();

            foreach (var registro in model.Registros)
            {
                context.Execute(@"
                    MERGE Asistencia AS destino
                    USING (SELECT @Estudiante AS ConsecutivoEstudiante, @Materia AS ConsecutivoMateria, @Fecha AS Fecha) AS origen
                        ON destino.ConsecutivoEstudiante = origen.ConsecutivoEstudiante
                       AND destino.ConsecutivoMateria = origen.ConsecutivoMateria
                       AND destino.Fecha = origen.Fecha
                    WHEN MATCHED THEN
                        UPDATE SET Estado = @Estado, Observacion = @Observacion
                    WHEN NOT MATCHED THEN
                        INSERT (ConsecutivoEstudiante, ConsecutivoMateria, ConsecutivoDocente, Fecha, Estado, Observacion)
                        VALUES (@Estudiante, @Materia, @Docente, @Fecha, @Estado, @Observacion);",
                    new
                    {
                        Estudiante = registro.ConsecutivoEstudiante,
                        Materia = model.ConsecutivoMateria,
                        Docente = docenteId,
                        Fecha = model.Fecha.Date,
                        registro.Estado,
                        registro.Observacion
                    });
            }

            return Ok(new { Mensaje = "Asistencia registrada correctamente.", Total = model.Registros.Count });
        }

        // GET api/Asistencia/ConsultarMiAsistenciaAPI  (Estudiante ve su propio historial)
        [Authorize(Roles = "Estudiante")]
        [HttpGet("ConsultarMiAsistenciaAPI")]
        public IActionResult ConsultarMiAsistenciaAPI()
        {
            using var context = Conexion;

            var registros = context.Query<AsistenciaResponseModel>(@"
                SELECT a.ConsecutivoAsistencia, a.ConsecutivoEstudiante, u.Nombre AS Estudiante,
                       a.ConsecutivoMateria, m.Nombre AS Materia, a.Fecha, a.Estado, a.Observacion
                FROM Asistencia a
                INNER JOIN Materia m ON m.ConsecutivoMateria = a.ConsecutivoMateria
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = a.ConsecutivoEstudiante
                WHERE a.ConsecutivoEstudiante = @Id
                ORDER BY a.Fecha DESC",
                new { Id = _utiles.ObtenerConsecutivoToken() });

            var resumen = CalcularResumen(registros);

            return Ok(new { Resumen = resumen, Registros = registros });
        }

        private static ResumenAsistenciaResponseModel CalcularResumen(IEnumerable<AsistenciaResponseModel> registros)
        {
            var lista = registros.ToList();
            var total = lista.Count;
            var presentes = lista.Count(r => r.Estado == "Presente");
            var ausentes = lista.Count(r => r.Estado == "Ausente");
            var tardias = lista.Count(r => r.Estado == "Tardía");
            var justificadas = lista.Count(r => r.Estado == "Justificada");

            return new ResumenAsistenciaResponseModel
            {
                TotalRegistros = total,
                Presentes = presentes,
                Ausentes = ausentes,
                Tardias = tardias,
                Justificadas = justificadas,
                PorcentajeAsistencia = total == 0 ? 100 : Math.Round((presentes + tardias * 0.5) / total * 100, 1)
            };
        }
    }
}
