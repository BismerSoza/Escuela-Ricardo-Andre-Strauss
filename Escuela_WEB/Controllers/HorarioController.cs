using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class HorarioController(ApiClienteService _api) : Controller
    {
        private static readonly string[] Dias = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes" };

        // GET /Horario
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rol = HttpContext.Session.GetString("Rol");
            ViewBag.Rol = rol;
            ViewBag.Dias = Dias;
            ViewBag.PuedeGestionar = rol == "Director" || rol == "Docente";

            var (exito, horario, error) = await _api.GetAsync<List<HorarioViewModel>>("api/Horario/ConsultarHorarioAPI");

            if (!exito)
            {
                TempData["Error"] = error;
                horario = new List<HorarioViewModel>();
            }

            return View(horario);
        }

        // GET /Horario/Registrar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            var (exito, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");
            ViewBag.Materias = exito ? materias : new List<MateriaViewModel>();
            ViewBag.Dias = Dias;

            return View(new RegistrarHorarioViewModel());
        }

        // POST /Horario/Registrar
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarHorarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var (exito, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");
                ViewBag.Materias = exito ? materias : new List<MateriaViewModel>();
                ViewBag.Dias = Dias;
                return View(model);
            }

            var (exitoRegistro, _, error) = await _api.PostAsync<JsonElement>("api/Horario/RegistrarHorarioAPI", model);

            TempData[exitoRegistro ? "Mensaje" : "Error"] = exitoRegistro ? "Bloque de horario agregado correctamente." : error;
            return RedirectToAction("Index");
        }

        // POST /Horario/Eliminar/5
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id)
        {
            var (exito, error) = await _api.DeleteAsync($"api/Horario/EliminarHorarioAPI/{id}");
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Bloque de horario eliminado." : error;
            return RedirectToAction("Index");
        }
    }
}
