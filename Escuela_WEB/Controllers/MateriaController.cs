using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class MateriaController(ApiClienteService _api) : Controller
    {
        // GET /Materia
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Rol = HttpContext.Session.GetString("Rol");

            var (exito, materias, error) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");

            if (!exito)
            {
                TempData["Error"] = error;
                materias = new List<MateriaViewModel>();
            }

            return View(materias);
        }

        // Listas desplegables del formulario: secciones (catálogo) y, para el Director, los docentes activos.
        private async Task CargarListas()
        {
            var (exitoS, secciones, _) = await _api.GetAsync<List<string>>("api/Seccion/ConsultarActivasAPI");
            ViewBag.Secciones = exitoS && secciones != null ? secciones : new List<string>();

            if (HttpContext.Session.GetString("Rol") == "Director")
            {
                var (exito, docentes, _) = await _api.GetAsync<List<UsuarioAdminViewModel>>("api/Admin/ConsultarUsuariosAPI?rol=Docente");
                ViewBag.Docentes = exito && docentes != null ? docentes.Where(d => d.Activo).ToList() : new List<UsuarioAdminViewModel>();
            }
        }

        // GET /Materia/Registrar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            await CargarListas();
            return View(new RegistrarMateriaViewModel());
        }

        // POST /Materia/Registrar
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarMateriaViewModel model)
        {
            var esDirector = HttpContext.Session.GetString("Rol") == "Director";

            if (esDirector && model.ConsecutivoDocente == null)
            {
                ModelState.AddModelError(nameof(model.ConsecutivoDocente), "Seleccione el docente a cargo.");
            }

            if (!ModelState.IsValid)
            {
                await CargarListas();
                return View(model);
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Materia/RegistrarMateriaAPI", new
            {
                Nombre = model.Nombre,
                Seccion = model.Seccion,
                ConsecutivoDocente = esDirector ? model.ConsecutivoDocente : null
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible registrar la asignatura.");
                await CargarListas();
                return View(model);
            }

            TempData["Mensaje"] = "Asignatura registrada correctamente.";
            return RedirectToAction("Index");
        }

        // POST /Materia/Eliminar/5  (desactiva)
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            var (exito, error) = await _api.DeleteAsync($"api/Materia/EliminarMateriaAPI/{id}");
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Asignatura desactivada correctamente." : error;
            return RedirectToAction("Index");
        }

        // POST /Materia/Reactivar/5
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Reactivar(int id)
        {
            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Materia/ReactivarMateriaAPI/{id}", new { });
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Asignatura reactivada correctamente." : error;
            return RedirectToAction("Index");
        }
    }
}
