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

        // GET /Aviso/Publicar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public IActionResult Publicar() => View(new RegistrarAvisoViewModel());

        // POST /Aviso/Publicar
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Publicar(RegistrarAvisoViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Aviso/RegistrarAvisoAPI", new
            {
                Titulo = model.Titulo,
                Contenido = model.Contenido,
                Prioridad = model.Prioridad,
                Seccion = model.Seccion
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible publicar el aviso.");
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
