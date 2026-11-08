using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    public class UsuarioController(ApiClienteService _api) : Controller
    {
        // GET /Usuario/Registro
        [HttpGet]
        public IActionResult Registro() => View(new RegistroViewModel());

        // POST /Usuario/Registro
        [HttpPost]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Usuario/RegistrarUsuarioAPI", new
            {
                Nombre = model.Nombre,
                CorreoElectronico = model.CorreoElectronico,
                Contrasenna = model.Contrasenna,
                Rol = model.Rol,
                Seccion = model.Seccion
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible crear la cuenta.");
                return View(model);
            }

            TempData["Mensaje"] = "Cuenta creada correctamente. Ahora puede iniciar sesión.";
            return RedirectToAction("Index", "Home");
        }

        // GET /Usuario/RecuperarAcceso
        [HttpGet]
        public IActionResult RecuperarAcceso() => View(new RecuperarAccesoViewModel());

        // POST /Usuario/RecuperarAcceso
        [HttpPost]
        public async Task<IActionResult> RecuperarAcceso(RecuperarAccesoViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exito, datos, error) = await _api.PostAsync<JsonElement>("api/Usuario/RecuperarAccesoAPI", new
            {
                CorreoElectronico = model.CorreoElectronico
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible procesar la solicitud.");
                return View(model);
            }

            ViewBag.Mensaje = datos.GetProperty("mensaje").GetString();

            if (datos.TryGetProperty("contrasennaTemporal", out var temporal))
            {
                ViewBag.ContrasennaTemporal = temporal.GetString();
            }

            return View(model);
        }

        // GET /Usuario/Configuracion
        [SessionAuthorize]
        [HttpGet]
        public async Task<IActionResult> Configuracion()
        {
            var (exito, perfil, error) = await _api.GetAsync<JsonElement>("api/Usuario/ConsultarPerfilAPI");

            if (!exito)
            {
                TempData["Error"] = error;
                return RedirectToAction("Principal", "Home");
            }

            var model = new ConfiguracionViewModel
            {
                Nombre = perfil.GetProperty("nombre").GetString() ?? string.Empty,
                CorreoElectronico = perfil.GetProperty("correoElectronico").GetString() ?? string.Empty,
                Seccion = perfil.TryGetProperty("seccion", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null
            };

            return View(model);
        }

        // POST /Usuario/Configuracion
        [SessionAuthorize]
        [HttpPost]
        public async Task<IActionResult> Configuracion(ConfiguracionViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exitoPerfil, _, errorPerfil) = await _api.PutAsync<JsonElement>("api/Usuario/ActualizarPerfilAPI", new
            {
                Nombre = model.Nombre,
                Seccion = model.Seccion
            });

            if (!exitoPerfil)
            {
                ModelState.AddModelError(string.Empty, errorPerfil ?? "No fue posible actualizar el perfil.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.ContrasennaNueva))
            {
                var (exitoClave, _, errorClave) = await _api.PutAsync<JsonElement>("api/Usuario/CambiarContrasennaAPI", new
                {
                    ContrasennaNueva = model.ContrasennaNueva
                });

                if (!exitoClave)
                {
                    ModelState.AddModelError(string.Empty, errorClave ?? "No fue posible actualizar la contraseña.");
                    return View(model);
                }
            }

            HttpContext.Session.SetString("Nombre", model.Nombre);
            if (!string.IsNullOrEmpty(model.Seccion))
            {
                HttpContext.Session.SetString("Seccion", model.Seccion);
            }

            TempData["Mensaje"] = "Perfil actualizado correctamente.";
            return RedirectToAction("Configuracion");
        }
    }
}
