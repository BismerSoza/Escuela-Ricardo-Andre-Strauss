using Microsoft.AspNetCore.Mvc;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class MensajeController(ApiClienteService _api, IConfiguration _config) : Controller
    {
        // GET /Mensaje/Chat
        [HttpGet]
        public async Task<IActionResult> Chat()
        {
            var (exito, contactos, error) = await _api.GetAsync<List<ContactoViewModel>>("api/Mensaje/ConsultarContactosAPI");

            if (!exito)
            {
                TempData["Error"] = error;
                contactos = new List<ContactoViewModel>();
            }

            // Datos necesarios para que la vista se conecte directamente al Hub de SignalR de la API.
            ViewBag.Token = HttpContext.Session.GetString("Token");
            ViewBag.UrlApi = _config["Api:BaseUrl"];
            ViewBag.ConsecutivoUsuario = HttpContext.Session.GetString("ConsecutivoUsuario");

            return View(contactos);
        }

        // GET /Mensaje/ConsultarConversacion?contactoId=5  (usado por JS via fetch para refrescar el chat)
        [HttpGet]
        public async Task<IActionResult> ConsultarConversacion(int contactoId)
        {
            var (exito, mensajes, error) = await _api.GetAsync<List<MensajeViewModel>>(
                $"api/Mensaje/ConsultarConversacionAPI/{contactoId}");

            if (!exito) return BadRequest(error);

            return Json(mensajes);
        }

        // POST /Mensaje/Enviar  (usado por JS via fetch)
        [HttpPost]
        public async Task<IActionResult> Enviar([FromBody] EnviarMensajeInput input)
        {
            var (exito, _, error) = await _api.PostAsync<object>("api/Mensaje/EnviarMensajeAPI", new
            {
                ConsecutivoReceptor = input.ConsecutivoReceptor,
                Contenido = input.Contenido
            });

            if (!exito) return BadRequest(error);

            return Ok();
        }
    }

    public class EnviarMensajeInput
    {
        public int ConsecutivoReceptor { get; set; }
        public string Contenido { get; set; } = string.Empty;
    }
}
