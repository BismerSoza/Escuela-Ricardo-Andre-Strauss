using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class AvisoController(ApiClienteService _api) : Controller
    {
        // GET /Aviso
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rol = HttpContext.Session.GetString("Rol");
            ViewBag.Rol = rol;
            ViewBag.ConsecutivoUsuario = HttpContext.Session.GetString("ConsecutivoUsuario");
            ViewBag.PuedePublicar = rol == "Docente" || rol == "Director";

            var (exito, avisos, error) = await _api.GetAsync<List<AvisoViewModel>>("api/Aviso/ConsultarAvisosAPI");

            if (!exito)
            {
                TempData["Error"] = error;
                avisos = new List<AvisoViewModel>();
            }

            return View(avisos);
        }

        // Secciones a las que puede dirigir un aviso: el Director todas; el Docente solo donde dicta clases.
        private async Task CargarSecciones()
        {
            ViewBag.EsDirector = HttpContext.Session.GetString("Rol") == "Director";
            var (exito, secciones, _) = await _api.GetAsync<List<string>>("api/Seccion/ConsultarMisSeccionesAPI");
            ViewBag.Secciones = exito && secciones != null ? secciones : new List<string>();
        }

        // GET /Aviso/Publicar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Publicar()
        {
            await CargarSecciones();
            return View(new RegistrarAvisoViewModel());
        }

        // POST /Aviso/Publicar
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Publicar(RegistrarAvisoViewModel model)
        {
            var esDirector = HttpContext.Session.GetString("Rol") == "Director";

            if (!esDirector && string.IsNullOrWhiteSpace(model.Seccion))
            {
                ModelState.AddModelError(nameof(model.Seccion), "Seleccione la sección a la que va dirigido el aviso.");
            }

            if (!ModelState.IsValid)
            {
                await CargarSecciones();
                return View(model);
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Aviso/RegistrarAvisoAPI", new
            {
                Titulo = model.Titulo,
                Contenido = model.Contenido,
                Prioridad = model.Prioridad,
                Seccion = string.IsNullOrWhiteSpace(model.Seccion) ? null : model.Seccion
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible publicar el aviso.");
                await CargarSecciones();
                return View(model);
            }

            TempData["Mensaje"] = "Aviso publicado correctamente.";
            return RedirectToAction("Index");
        }

        // POST /Aviso/Eliminar/5
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            var (exito, error) = await _api.DeleteAsync($"api/Aviso/EliminarAvisoAPI/{id}");
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Aviso eliminado correctamente." : error;
            return RedirectToAction("Index");
        }
    }
}
