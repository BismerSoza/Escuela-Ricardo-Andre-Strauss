using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Escuela_API.Hubs;
using Escuela_API.Models;
using Escuela_API.Services;

namespace Escuela_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MensajeController(IConfiguration _config, IUtilesService _utiles, IHubContext<ChatHub> _hub) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Mensaje/ConsultarContactosAPI
        // Docente   -> estudiantes de su seccion + Director.
        // Estudiante -> docentes de su seccion + Director.
        // Director  -> todos los docentes y estudiantes de la institucion.
        [HttpGet("ConsultarContactosAPI")]
        public IActionResult ConsultarContactosAPI()
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();
            var seccion = _utiles.ObtenerSeccionToken();
            var propioId = _utiles.ObtenerConsecutivoToken();

            IEnumerable<ContactoResponseModel> contactos;

            if (rol == "Director")
            {
                contactos = context.Query<ContactoResponseModel>(@"
                    SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion
                    FROM Usuario WHERE Rol IN ('Docente','Estudiante') AND Activo = 1
                    ORDER BY Rol, Nombre");
            }
            else if (rol == "Docente")
            {
                contactos = context.Query<ContactoResponseModel>(@"
                    SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion
                    FROM Usuario
                    WHERE (Rol = 'Estudiante' AND (Seccion = @Seccion OR @Seccion IS NULL)) OR Rol = 'Director'
                    ORDER BY Rol, Nombre",
                    new { Seccion = seccion });
            }
            else
            {
                contactos = context.Query<ContactoResponseModel>(@"
                    SELECT ConsecutivoUsuario, Nombre, CorreoElectronico, Rol, Seccion
                    FROM Usuario WHERE Rol = 'Docente' OR Rol = 'Director'
                    ORDER BY Rol, Nombre");
            }

            return Ok(contactos.Where(c => c.ConsecutivoUsuario != propioId));
        }

        // GET api/Mensaje/ConsultarConversacionAPI/5
        [HttpGet("ConsultarConversacionAPI/{contactoId:int}")]
        public IActionResult ConsultarConversacionAPI(int contactoId)
        {
            using var context = Conexion;
            var propioId = _utiles.ObtenerConsecutivoToken();

            var mensajes = context.Query<MensajeResponseModel>(@"
                SELECT ConsecutivoMensaje, ConsecutivoEmisor, ConsecutivoReceptor, Contenido, Leido, FechaCreacion
                FROM Mensaje
                WHERE (ConsecutivoEmisor = @Propio AND ConsecutivoReceptor = @Contacto)
                   OR (ConsecutivoEmisor = @Contacto AND ConsecutivoReceptor = @Propio)
                ORDER BY FechaCreacion ASC",
                new { Propio = propioId, Contacto = contactoId });

            context.Execute(@"
                UPDATE Mensaje SET Leido = 1
                WHERE ConsecutivoReceptor = @Propio AND ConsecutivoEmisor = @Contacto AND Leido = 0",
                new { Propio = propioId, Contacto = contactoId });

            return Ok(mensajes);
        }

        // GET api/Mensaje/ConsultarNoLeidosAPI  -> cantidad de mensajes recibidos sin leer (campanita)
        [HttpGet("ConsultarNoLeidosAPI")]
        public IActionResult ConsultarNoLeidosAPI()
        {
            using var context = Conexion;

            var total = context.QueryFirstOrDefault<int>(
                "SELECT COUNT(*) FROM Mensaje WHERE ConsecutivoReceptor = @Id AND Leido = 0",
                new { Id = _utiles.ObtenerConsecutivoToken() });

            return Ok(new { Total = total });
        }

        // POST api/Mensaje/EnviarMensajeAPI
        [HttpPost("EnviarMensajeAPI")]
        public async Task<IActionResult> EnviarMensajeAPI(EnviarMensajeRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Contenido))
            {
                return BadRequest("El contenido del mensaje es obligatorio.");
            }

            using var context = Conexion;
            var emisorId = _utiles.ObtenerConsecutivoToken();

            var destinatarioExiste = context.QueryFirstOrDefault<int?>(
                "SELECT ConsecutivoUsuario FROM Usuario WHERE ConsecutivoUsuario = @Id",
                new { Id = model.ConsecutivoReceptor });

            if (destinatarioExiste == null) return NotFound("El destinatario no existe.");

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Mensaje (ConsecutivoEmisor, ConsecutivoReceptor, Contenido)
                OUTPUT INSERTED.ConsecutivoMensaje
                VALUES (@Emisor, @Receptor, @Contenido)",
                new { Emisor = emisorId, Receptor = model.ConsecutivoReceptor, model.Contenido });

            // Notificacion en tiempo real via SignalR a los clientes conectados al chat del destinatario.
            await _hub.Clients.Group($"usuario-{model.ConsecutivoReceptor}")
                .SendAsync("MensajeRecibido", new
                {
                    ConsecutivoMensaje = nuevoId,
                    ConsecutivoEmisor = emisorId,
                    ConsecutivoReceptor = model.ConsecutivoReceptor,
                    model.Contenido,
                    FechaCreacion = DateTime.Now
                });

            return Ok(new { Mensaje = "Mensaje enviado.", ConsecutivoMensaje = nuevoId });
        }
    }
}
