using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    public class UsuarioController(ApiClienteService _api) : Controller
    {
        // Las secciones que se ofrecen al registrarse salen de la tabla Seccion (catálogo
        // que mantiene el Director en Administración > Secciones), no de texto libre.
        private async Task CargarSecciones()
        {
            var (exito, secciones, _) = await _api.GetAsync<List<string>>("api/Seccion/ConsultarPublicasAPI");
            ViewBag.Secciones = exito && secciones != null ? secciones : new List<string>();
        }

        // GET /Usuario/Registro
        [HttpGet]
        public async Task<IActionResult> Registro()
        {
            await CargarSecciones();
            return View(new RegistroViewModel());
        }

        // POST /Usuario/Registro
        [HttpPost]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarSecciones();
                return View(model);
            }

            var (exito, datos, error) = await _api.PostAsync<JsonElement>("api/Usuario/RegistrarUsuarioAPI", new
            {
                Nombre = model.Nombre,
                CorreoElectronico = model.CorreoElectronico,
                Contrasenna = model.Contrasenna,
                Rol = model.Rol,
                Seccion = model.Rol == "Estudiante" ? model.Seccion : null
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible crear la cuenta.");
                await CargarSecciones();
                return View(model);
            }

            var pendiente = datos.ValueKind == JsonValueKind.Object
                            && datos.TryGetProperty("pendiente", out var p) && p.ValueKind == JsonValueKind.True;

            TempData["Mensaje"] = pendiente
                ? "Solicitud enviada. La dirección debe aprobar su cuenta de docente; cuando lo haga podrá iniciar sesión."
                : "Cuenta creada correctamente. Ahora puede iniciar sesión.";

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

            ViewBag.Mensaje = datos.ValueKind == JsonValueKind.Object && datos.TryGetProperty("mensaje", out var m)
                ? m.GetString()
                : "Solicitud recibida.";

            return View(new RecuperarAccesoViewModel());
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
                Rol = perfil.GetProperty("rol").GetString() ?? string.Empty,
                Seccion = perfil.TryGetProperty("seccion", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null
            };

            return View(model);
        }

        // POST /Usuario/Configuracion
        [SessionAuthorize]
        [HttpPost]
        public async Task<IActionResult> Configuracion(ConfiguracionViewModel model)
        {
            // Los campos de solo lectura no viajan en el formulario: se rellenan desde la sesión.
            model.CorreoElectronico = HttpContext.Session.GetString("Correo") ?? string.Empty;
            model.Rol = HttpContext.Session.GetString("Rol") ?? string.Empty;
            model.Seccion = HttpContext.Session.GetString("Seccion");

            if (!ModelState.IsValid) return View(model);

            var (exitoPerfil, _, errorPerfil) = await _api.PutAsync<JsonElement>("api/Usuario/ActualizarPerfilAPI", new
            {
                Nombre = model.Nombre
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
                    ContrasennaActual = model.ContrasennaActual,
                    ContrasennaNueva = model.ContrasennaNueva
                });

                if (!exitoClave)
                {
                    ModelState.AddModelError(nameof(model.ContrasennaActual), errorClave ?? "No fue posible actualizar la contraseña.");
                    return View(model);
                }
            }

            HttpContext.Session.SetString("Nombre", model.Nombre.Trim());

            TempData["Mensaje"] = "Perfil actualizado correctamente.";
            return RedirectToAction("Configuracion");
        }
    }
}
