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
        // Docente    -> estudiantes de las secciones donde dicta clases + Director.
        // Estudiante -> docentes que dictan en su sección + Director.
        // Director   -> todos los docentes y estudiantes activos.
        [HttpGet("ConsultarContactosAPI")]
        public IActionResult ConsultarContactosAPI()
        {
            using var context = Conexion;
            return Ok(ObtenerContactos(context));
        }

        private List<ContactoResponseModel> ObtenerContactos(SqlConnection context)
        {
            var rol = _utiles.ObtenerRolToken();
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
                    WHERE Activo = 1 AND (
                        Rol = 'Director'
                        OR (Rol = 'Estudiante' AND Seccion IN (
                              SELECT Seccion FROM Materia WHERE ConsecutivoDocente = @Id AND Activa = 1)))
                    ORDER BY Rol, Nombre",
                    new { Id = propioId });
            }
            else
            {
                contactos = context.Query<ContactoResponseModel>(@"
                    SELECT u.ConsecutivoUsuario, u.Nombre, u.CorreoElectronico, u.Rol, u.Seccion
                    FROM Usuario u
                    WHERE u.Activo = 1 AND (
                        u.Rol = 'Director'
                        OR (u.Rol = 'Docente' AND EXISTS (
                              SELECT 1 FROM Materia m
                              WHERE m.ConsecutivoDocente = u.ConsecutivoUsuario AND m.Activa = 1 AND m.Seccion = @Seccion)))
                    ORDER BY u.Rol, u.Nombre",
                    new { Seccion = _utiles.ObtenerSeccionToken() });
            }

            return contactos.Where(c => c.ConsecutivoUsuario != propioId).ToList();
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

            if (model.Contenido.Length > 2000) return BadRequest("El mensaje no puede superar 2000 caracteres.");

            // Solo se puede escribir a personas que aparecen en la lista de contactos del usuario.
            if (!ObtenerContactos(context).Any(c => c.ConsecutivoUsuario == model.ConsecutivoReceptor))
            {
                return BadRequest("No puede enviar mensajes a ese destinatario.");
            }

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO Mensaje (ConsecutivoEmisor, ConsecutivoReceptor, Contenido)
                OUTPUT INSERTED.ConsecutivoMensaje
                VALUES (@Emisor, @Receptor, @Contenido)",
                new { Emisor = emisorId, Receptor = model.ConsecutivoReceptor, Contenido = model.Contenido.Trim() });

            // Notificacion en tiempo real via SignalR a los clientes conectados al chat del destinatario.
            await _hub.Clients.Group($"usuario-{model.ConsecutivoReceptor}")
                .SendAsync("MensajeRecibido", new
                {
                    ConsecutivoMensaje = nuevoId,
                    ConsecutivoEmisor = emisorId,
                    ConsecutivoReceptor = model.ConsecutivoReceptor,
                    Contenido = model.Contenido.Trim(),
                    FechaCreacion = DateTime.Now
                });

            return Ok(new { Mensaje = "Mensaje enviado.", ConsecutivoMensaje = nuevoId });
        }
    }
}
