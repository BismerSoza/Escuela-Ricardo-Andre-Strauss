using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class AsistenciaController(ApiClienteService _api) : Controller
    {
        // GET /Asistencia
        [HttpGet]
        public async Task<IActionResult> Index(int? materiaId, DateTime? fecha)
        {
            var rol = HttpContext.Session.GetString("Rol");

            if (rol == "Estudiante")
            {
                var (exito, datos, error) = await _api.GetAsync<JsonElement>("api/Asistencia/ConsultarMiAsistenciaAPI");

                if (!exito)
                {
                    TempData["Error"] = error;
                    return View("IndexEstudiante", (JsonElement?)null);
                }

                return View("IndexEstudiante", datos);
            }

            // Director / Docente
            var model = new PanelAsistenciaViewModel { Fecha = fecha ?? DateTime.Today };

            var (exitoMaterias, materias, errorMaterias) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");
            model.Materias = exitoMaterias ? materias ?? new() : new();
            if (!exitoMaterias) TempData["Error"] = errorMaterias;

            model.MateriaSeleccionada = materiaId ?? model.Materias.FirstOrDefault()?.ConsecutivoMateria;

            if (model.MateriaSeleccionada != null)
            {
                var (exitoLista, estudiantes, errorLista) = await _api.GetAsync<List<EstudianteAsistenciaViewModel>>(
                    $"api/Asistencia/ConsultarPorMateriaAPI?materiaId={model.MateriaSeleccionada}&fecha={model.Fecha:yyyy-MM-dd}");

                model.Estudiantes = exitoLista ? estudiantes ?? new() : new();
                if (!exitoLista) TempData["Error"] = errorLista;
            }

            ViewBag.PuedeRegistrar = rol == "Docente";

            return View("IndexDocente", model);
        }

        // POST /Asistencia/Registrar  (solo Docente)
        [SessionRol("Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(int consecutivoMateria, DateTime fecha, List<int> estudianteId, List<string> estado, List<string?> observacion)
        {
            var registros = new List<object>();
            for (int i = 0; i < estudianteId.Count; i++)
            {
                registros.Add(new
                {
                    ConsecutivoEstudiante = estudianteId[i],
                    Estado = i < estado.Count ? estado[i] : "Presente",
                    Observacion = i < observacion.Count ? observacion[i] : null
                });
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Asistencia/RegistrarLoteAPI", new
            {
                ConsecutivoMateria = consecutivoMateria,
                Fecha = fecha,
                Registros = registros
            });

            TempData[exito ? "Mensaje" : "Error"] = exito ? "Asistencia registrada correctamente." : error;
            return RedirectToAction("Index", new { materiaId = consecutivoMateria, fecha = fecha.ToString("yyyy-MM-dd") });
        }
    }
}
