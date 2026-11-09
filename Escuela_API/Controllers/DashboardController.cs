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
    public class DashboardController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Dashboard/ConsultarResumenAPI -> arma un resumen distinto segun el rol del token.
        [HttpGet("ConsultarResumenAPI")]
        public IActionResult ConsultarResumenAPI()
        {
            var rol = _utiles.ObtenerRolToken();

            return rol switch
            {
                "Estudiante" => Ok(ResumenEstudiante()),
                "Docente" => Ok(ResumenDocente()),
                "Director" => Ok(ResumenDirector()),
                _ => Forbid()
            };
        }

        private DashboardEstudianteResponseModel ResumenEstudiante()
        {
            using var context = Conexion;
            var id = _utiles.ObtenerConsecutivoToken();
            var seccion = _utiles.ObtenerSeccionToken();

            var totalAsignaturas = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Materia WHERE Seccion = @Seccion AND Activa = 1", new { Seccion = seccion });

            var promedios = context.Query<ParPromedio>(@"
                SELECT Materia AS Etiqueta, AVG(CAST(Nota AS FLOAT)) AS Promedio FROM HistorialAcademico
                WHERE ConsecutivoEstudiante = @Id AND Nota IS NOT NULL
                GROUP BY Materia", new { Id = id }).ToList();

            var promedioGeneral = promedios.Count > 0 ? Math.Round(promedios.Average(p => p.Promedio), 1) : 0;

            var avisosNuevos = context.QueryFirstOrDefault<int>(@"
                SELECT COUNT(*) FROM Aviso
                WHERE (Seccion IS NULL OR Seccion = @Seccion) AND FechaCreacion >= DATEADD(day, -7, GETDATE())",
                new { Seccion = seccion });

            var asistencia = context.Query<string>(
                "SELECT Estado FROM Asistencia WHERE ConsecutivoEstudiante = @Id", new { Id = id }).ToList();
            // Misma fórmula que la pantalla de Asistencia: Presente = 1, Tardía = 0.5.
            var porcentajeAsistencia = asistencia.Count == 0 ? 100 :
                Math.Round((asistencia.Count(e => e == "Presente") + asistencia.Count(e => e == "Tardía") * 0.5) / asistencia.Count * 100, 1);

            var ultimasCalificaciones = context.Query<HistorialResponseModel>(@"
                SELECT TOP 5 h.ConsecutivoHistorial, h.Materia, h.Periodo, h.Nota, h.Observacion, h.FechaCreacion,
                       d.Nombre AS Docente
                FROM HistorialAcademico h
                INNER JOIN Usuario d ON d.ConsecutivoUsuario = h.ConsecutivoDocente
                WHERE h.ConsecutivoEstudiante = @Id
                ORDER BY h.FechaCreacion DESC", new { Id = id }).ToList();

            return new DashboardEstudianteResponseModel
            {
                TotalAsignaturas = totalAsignaturas,
                PromedioGeneral = promedioGeneral,
                AvisosNuevos = avisosNuevos,
                PorcentajeAsistencia = porcentajeAsistencia,
                UltimasCalificaciones = ultimasCalificaciones,
                Materias = promedios.Select(p => p.Etiqueta).ToList(),
                PromediosPorMateria = promedios.Select(p => Math.Round(p.Promedio, 1)).ToList()
            };
        }

        private DashboardDocenteResponseModel ResumenDocente()
        {
            using var context = Conexion;
            var id = _utiles.ObtenerConsecutivoToken();

            var totalMaterias = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Materia WHERE ConsecutivoDocente = @Id AND Activa = 1", new { Id = id });

            var totalEstudiantes = context.QueryFirstOrDefault<int>(@"
                SELECT COUNT(DISTINCT u.ConsecutivoUsuario) FROM Usuario u
                INNER JOIN Materia m ON m.Seccion = u.Seccion
                WHERE u.Rol = 'Estudiante' AND u.Activo = 1 AND m.Activa = 1 AND m.ConsecutivoDocente = @Id", new { Id = id });

            var promedios = context.Query<ParPromedio>(@"
                SELECT Materia AS Etiqueta, AVG(CAST(Nota AS FLOAT)) AS Promedio FROM HistorialAcademico
                WHERE ConsecutivoDocente = @Id AND Nota IS NOT NULL
                GROUP BY Materia", new { Id = id }).ToList();

            var avisosPublicados = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Aviso WHERE ConsecutivoAutor = @Id", new { Id = id });

            return new DashboardDocenteResponseModel
            {
                TotalMaterias = totalMaterias,
                TotalEstudiantes = totalEstudiantes,
                PromedioGrupo = promedios.Count > 0 ? Math.Round(promedios.Average(p => p.Promedio), 1) : 0,
                AvisosPublicados = avisosPublicados,
                Materias = promedios.Select(p => p.Etiqueta).ToList(),
                PromediosPorMateria = promedios.Select(p => Math.Round(p.Promedio, 1)).ToList()
            };
        }

        private DashboardDirectorResponseModel ResumenDirector()
        {
            using var context = Conexion;

            var totalUsuarios = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Usuario WHERE Activo = 1");
            var totalDocentes = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Usuario WHERE Rol = 'Docente' AND Activo = 1");
            var totalEstudiantes = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Usuario WHERE Rol = 'Estudiante' AND Activo = 1");
            var totalMaterias = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Materia WHERE Activa = 1");

            var promedios = context.Query<ParPromedio>(@"
                SELECT u.Seccion AS Etiqueta, AVG(CAST(h.Nota AS FLOAT)) AS Promedio FROM HistorialAcademico h
                INNER JOIN Usuario u ON u.ConsecutivoUsuario = h.ConsecutivoEstudiante
                WHERE h.Nota IS NOT NULL AND u.Seccion IS NOT NULL
                GROUP BY u.Seccion").ToList();

            var avisosRecientes = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Aviso WHERE FechaCreacion >= DATEADD(day, -30, GETDATE())");

            var pendientes = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Usuario WHERE Pendiente = 1 AND Activo = 0");
            var clavesSolicitadas = context.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM Usuario WHERE ClaveSolicitada IS NOT NULL AND Activo = 1");

            return new DashboardDirectorResponseModel
            {
                TotalUsuarios = totalUsuarios,
                TotalDocentes = totalDocentes,
                TotalEstudiantes = totalEstudiantes,
                TotalMaterias = totalMaterias,
                PromedioInstitucional = promedios.Count > 0 ? Math.Round(promedios.Average(p => p.Promedio), 1) : 0,
                AvisosRecientes = avisosRecientes,
                UsuariosPendientes = pendientes,
                ClavesSolicitadas = clavesSolicitadas,
                Secciones = promedios.Select(p => p.Etiqueta).ToList(),
                PromediosPorSeccion = promedios.Select(p => Math.Round(p.Promedio, 1)).ToList()
            };
        }
    }

    // Clase auxiliar interna para mapear resultados de Dapper (evita depender
    // del soporte de ValueTuple del proveedor de base de datos).
    internal class ParPromedio
    {
        public string Etiqueta { get; set; } = string.Empty;
        public double Promedio { get; set; }
    }
}
