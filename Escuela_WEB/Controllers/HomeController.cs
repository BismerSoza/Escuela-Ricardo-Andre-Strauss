using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    public class HomeController(ApiClienteService _api) : Controller
    {
        // GET /  -> pantalla de inicio de sesion
        [HttpGet]
        public IActionResult Index(int expirada = 0)
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Token")))
            {
                return RedirectToAction("Principal");
            }

            if (expirada == 1)
            {
                ViewBag.Aviso = "Su sesión terminó o su cuenta ya no tiene acceso. Inicie sesión nuevamente.";
            }

            return View(new InicioSesionViewModel());
        }

        // POST /Home/Index
        [HttpPost]
        public async Task<IActionResult> Index(InicioSesionViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Sin token previo: un 401 del login es "credenciales incorrectas", no "sesión vencida".
            HttpContext.Session.Clear();

            var (exito, datos, error) = await _api.PostAsync<JsonElement>("api/Usuario/IniciarSesionAPI", new
            {
                CorreoElectronico = model.CorreoElectronico,
                Contrasenna = model.Contrasenna
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible iniciar sesión.");
                return View(model);
            }

            var token = datos.GetProperty("token").GetString();
            var usuario = datos.GetProperty("usuario");

            HttpContext.Session.SetString("Token", token ?? string.Empty);
            HttpContext.Session.SetString("Nombre", usuario.GetProperty("nombre").GetString() ?? string.Empty);
            HttpContext.Session.SetString("Correo", usuario.GetProperty("correoElectronico").GetString() ?? string.Empty);
            HttpContext.Session.SetString("Rol", usuario.GetProperty("rol").GetString() ?? string.Empty);
            HttpContext.Session.SetString("ConsecutivoUsuario", usuario.GetProperty("consecutivoUsuario").GetInt32().ToString());
            HttpContext.Session.Remove("UltimaLecturaAvisos");

            if (usuario.TryGetProperty("seccion", out var seccionProp) && seccionProp.ValueKind == JsonValueKind.String)
            {
                HttpContext.Session.SetString("Seccion", seccionProp.GetString() ?? string.Empty);
            }
            else
            {
                HttpContext.Session.Remove("Seccion");
            }

            return RedirectToAction("Principal");
        }

        // GET /Home/Principal -> tablero segun el rol (Director / Docente / Estudiante)
        [SessionAuthorize]
        [HttpGet]
        public async Task<IActionResult> Principal()
        {
            var rol = HttpContext.Session.GetString("Rol");
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Rol = rol;

            var (exitoResumen, resumen, errorResumen) = await _api.GetAsync<JsonElement>("api/Dashboard/ConsultarResumenAPI");

            if (!exitoResumen)
            {
                // Si la API rechaza la sesion o falla, se vuelve al login con un mensaje claro
                // (antes la vista intentaba mostrarse sin datos y fallaba).
                HttpContext.Session.Clear();
                TempData["Error"] = "No fue posible cargar su información. Inicie sesión nuevamente. " + errorResumen;
                return RedirectToAction("Index");
            }

            // Avisos recientes (todos los roles)
            var (exitoAvisos, avisos, _) = await _api.GetAsync<List<AvisoViewModel>>("api/Aviso/ConsultarAvisosAPI");
            ViewBag.Avisos = exitoAvisos && avisos != null ? avisos.Take(4).ToList() : new List<AvisoViewModel>();

            // Clases de hoy (docente y estudiante)
            var dia = ObtenerDiaEnEspanol(DateTime.Now.DayOfWeek);
            ViewBag.DiaHoy = dia;
            ViewBag.ClasesHoy = new List<HorarioViewModel>();

            if (rol != "Director")
            {
                var (exitoHorario, horario, _) = await _api.GetAsync<List<HorarioViewModel>>("api/Horario/ConsultarHorarioAPI");
                if (exitoHorario && horario != null)
                {
                    ViewBag.ClasesHoy = horario.Where(h => h.DiaSemana == dia).OrderBy(h => h.HoraInicio).ToList();
                }
            }

            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (rol == "Docente")
            {
                var modelo = JsonSerializer.Deserialize<DashboardDocenteViewModel>(resumen.GetRawText(), opciones);
                return View("PrincipalDocente", modelo);
            }

            if (rol == "Director")
            {
                var modelo = JsonSerializer.Deserialize<DashboardDirectorViewModel>(resumen.GetRawText(), opciones);
                return View("PrincipalDirector", modelo);
            }

            var modeloEstudiante = JsonSerializer.Deserialize<DashboardEstudianteViewModel>(resumen.GetRawText(), opciones);
            return View("PrincipalEstudiante", modeloEstudiante);
        }

        // GET /Home/Notificaciones -> alimenta la campanita (JSON consumido por site.js)
        [SessionAuthorize]
        [HttpGet]
        public async Task<IActionResult> Notificaciones()
        {
            var ultimaLectura = ObtenerUltimaLectura();

            var (exitoAvisos, avisos, _) = await _api.GetAsync<List<AvisoViewModel>>("api/Aviso/ConsultarAvisosAPI");
            var nuevos = (exitoAvisos && avisos != null ? avisos : new List<AvisoViewModel>())
                .Where(a => a.FechaCreacion > ultimaLectura)
                .OrderByDescending(a => a.FechaCreacion)
                .ToList();

            var (exitoMensajes, mensajes, _) = await _api.GetAsync<JsonElement>("api/Mensaje/ConsultarNoLeidosAPI");
            var mensajesSinLeer = 0;
            if (exitoMensajes && mensajes.ValueKind == JsonValueKind.Object && mensajes.TryGetProperty("total", out var total))
            {
                mensajesSinLeer = total.GetInt32();
            }

            return Json(new
            {
                total = nuevos.Count + mensajesSinLeer,
                mensajes = mensajesSinLeer,
                avisos = nuevos.Take(5).Select(a => new
                {
                    titulo = a.Titulo,
                    prioridad = a.Prioridad,
                    autor = a.Autor,
                    fecha = a.FechaCreacion.ToString("dd/MM/yyyy HH:mm")
                })
            });
        }

        // POST /Home/MarcarLeidas -> los avisos actuales dejan de contarse como nuevos
        [SessionAuthorize]
        [HttpPost]
        public IActionResult MarcarLeidas()
        {
            HttpContext.Session.SetString("UltimaLecturaAvisos", DateTime.Now.Ticks.ToString());
            return Ok();
        }

        // POST /Home/CerrarSesion
        [HttpPost]
        public IActionResult CerrarSesion()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        private DateTime ObtenerUltimaLectura()
        {
            var guardado = HttpContext.Session.GetString("UltimaLecturaAvisos");
            if (long.TryParse(guardado, out var ticks)) return new DateTime(ticks);

            // Primera vez en la sesion: se consideran "nuevos" los avisos de los ultimos 7 dias.
            return DateTime.Now.AddDays(-7);
        }

        private static string ObtenerDiaEnEspanol(DayOfWeek dia) => dia switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miércoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            _ => "Fin de semana"
        };
    }
}
