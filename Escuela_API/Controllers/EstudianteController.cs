using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    [Authorize(Roles = "Director,Docente")]
    [Route("api/[controller]")]
    [ApiController]
    public class EstudianteController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Estudiante/ConsultarEstudiantesAPI
        // Docente  -> estudiantes activos de las secciones donde dicta alguna asignatura.
        // Director -> todos los estudiantes activos de la institución.
        [HttpGet("ConsultarEstudiantesAPI")]
        public IActionResult ConsultarEstudiantesAPI()
        {
            using var context = Conexion;
            var esDirector = _utiles.ObtenerRolToken() == "Director";

            var estudiantes = context.Query<ContactoResponseModel>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion
                FROM Usuario
                WHERE Rol = 'Estudiante' AND Activo = 1
                  AND (@EsDirector = 1 OR Seccion IN (
                        SELECT Seccion FROM Materia WHERE ConsecutivoDocente = @Docente AND Activa = 1))
                ORDER BY Seccion, Nombre",
                new { EsDirector = esDirector ? 1 : 0, Docente = _utiles.ObtenerConsecutivoToken() });

            return Ok(estudiantes);
        }
    }
}
