using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    // Catálogo de secciones (grados). La administración (crear / activar / desactivar)
    // la hace el Director desde AdminController; aquí solo se CONSULTA.
    [Route("api/[controller]")]
    [ApiController]
    public class SeccionController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Seccion/ConsultarPublicasAPI
        // Sin sesión: alimenta la lista desplegable de la pantalla "Crear cuenta".
        [AllowAnonymous]
        [HttpGet("ConsultarPublicasAPI")]
        public IActionResult ConsultarPublicasAPI()
        {
            using var context = Conexion;
            return Ok(context.Query<string>("SELECT Nombre FROM Seccion WHERE Activa = 1 ORDER BY Nombre"));
        }

        // GET api/Seccion/ConsultarActivasAPI  -> todas las secciones activas (para crear asignaturas)
        [Authorize]
        [HttpGet("ConsultarActivasAPI")]
        public IActionResult ConsultarActivasAPI()
        {
            using var context = Conexion;
            return Ok(context.Query<string>("SELECT Nombre FROM Seccion WHERE Activa = 1 ORDER BY Nombre"));
        }

        // GET api/Seccion/ConsultarMisSeccionesAPI
        // Director -> todas las activas. Docente -> donde dicta alguna asignatura. Estudiante -> la suya.
        [Authorize]
        [HttpGet("ConsultarMisSeccionesAPI")]
        public IActionResult ConsultarMisSeccionesAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            if (rol == "Director")
            {
                return Ok(context.Query<string>("SELECT Nombre FROM Seccion WHERE Activa = 1 ORDER BY Nombre"));
            }

            if (rol == "Docente")
            {
                return Ok(AccesoHelper.SeccionesDelDocente(context, _utiles.ObtenerConsecutivoToken()));
            }

            var seccion = _utiles.ObtenerSeccionToken();
            return Ok(string.IsNullOrWhiteSpace(seccion) ? new List<string>() : new List<string> { seccion });
        }
    }
}
