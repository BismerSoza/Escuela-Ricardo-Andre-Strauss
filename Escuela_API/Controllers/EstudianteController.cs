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
        // Docente -> solo los de su seccion a cargo. Director -> todos los estudiantes de la institucion.
        [HttpGet("ConsultarEstudiantesAPI")]
        public IActionResult ConsultarEstudiantesAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();
            var seccion = rol == "Director" ? null : _utiles.ObtenerSeccionToken();

            var estudiantes = context.Query<ContactoResponseModel>(@"
                SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion
                FROM Usuario
                WHERE Rol = 'Estudiante' AND (Seccion = @Seccion OR @Seccion IS NULL)
                ORDER BY Nombre",
                new { Seccion = seccion });

            return Ok(estudiantes);
        }
    }
}
