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

        // GET /Materia/Registrar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            if (HttpContext.Session.GetString("Rol") == "Director")
            {
                var (exito, docentes, _) = await _api.GetAsync<List<UsuarioAdminViewModel>>("api/Admin/ConsultarUsuariosAPI?rol=Docente");
                ViewBag.Docentes = exito ? docentes : new List<UsuarioAdminViewModel>();
            }

            return View(new RegistrarMateriaViewModel());
        }

        // POST /Materia/Registrar
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarMateriaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Materia/RegistrarMateriaAPI", new
            {
                Nombre = model.Nombre,
                Seccion = model.Seccion,
                ConsecutivoDocente = model.ConsecutivoDocente
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible registrar la asignatura.");
                return View(model);
            }

            TempData["Mensaje"] = "Asignatura registrada correctamente.";
            return RedirectToAction("Index");
        }

        // POST /Materia/Eliminar/5
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            var (exito, error) = await _api.DeleteAsync($"api/Materia/EliminarMateriaAPI/{id}");
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Asignatura desactivada correctamente." : error;
            return RedirectToAction("Index");
        }
    }
}
