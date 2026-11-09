using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionAuthorize]
    public class HistorialController(ApiClienteService _api) : Controller
    {
        // GET /Historial
        [HttpGet]
        public async Task<IActionResult> Index(int? estudianteId)
        {
            var rol = HttpContext.Session.GetString("Rol");

            if (rol == "Estudiante")
            {
                var (exito, registros, error) = await _api.GetAsync<List<HistorialViewModel>>("api/Historial/ConsultarHistorialAPI");
                if (!exito) TempData["Error"] = error;
                return View("IndexEstudiante", registros ?? new List<HistorialViewModel>());
            }

            // Rol Docente o Director: seleccionar estudiante y mostrar/registrar historial
            var model = new PanelHistorialViewModel();

            var (exitoEstudiantes, estudiantes, errorEstudiantes) = await _api.GetAsync<List<ContactoViewModel>>("api/Estudiante/ConsultarEstudiantesAPI");
            model.Estudiantes = exitoEstudiantes ? estudiantes ?? new() : new();
            if (!exitoEstudiantes) TempData["Error"] = errorEstudiantes;

            model.EstudianteSeleccionado = estudianteId ?? model.Estudiantes.FirstOrDefault()?.ConsecutivoUsuario;

            // Solo se pueden calificar asignaturas ACTIVAS del docente en la sección del estudiante elegido.
            var seccionEstudiante = model.Estudiantes
                .FirstOrDefault(e => e.ConsecutivoUsuario == model.EstudianteSeleccionado)?.Seccion;

            var (exitoMaterias, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");
            model.MateriasDisponibles = (exitoMaterias ? materias ?? new() : new())
                .Where(m => m.Activa && m.Seccion == seccionEstudiante).ToList();

            if (model.EstudianteSeleccionado != null)
            {
                var (exitoHist, registros, errorHist) = await _api.GetAsync<List<HistorialViewModel>>(
                    $"api/Historial/ConsultarHistorialAPI?estudianteId={model.EstudianteSeleccionado}");

                model.Registros = exitoHist ? registros ?? new() : new();
                if (!exitoHist) TempData["Error"] = errorHist;
            }

            ViewBag.PuedeRegistrar = rol == "Docente";

            return View("IndexDocente", model);
        }

        // POST /Historial/Registrar  (solo Docente)
        [SessionRol("Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarHistorialViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
                return RedirectToAction("Index", new { estudianteId = model.ConsecutivoEstudiante });
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Historial/RegistrarHistorialAPI", new
            {
                ConsecutivoEstudiante = model.ConsecutivoEstudiante,
                ConsecutivoMateria = model.ConsecutivoMateria,
                Periodo = model.Periodo,
                Nota = model.Nota,
                Observacion = model.Observacion
            });

            TempData[exito ? "Mensaje" : "Error"] = exito ? "Registro académico agregado correctamente." : error;

            return RedirectToAction("Index", new { estudianteId = model.ConsecutivoEstudiante });
        }

        // GET /Historial/ExportarPdf?estudianteId=3  -> descarga el reporte de calificaciones
        [HttpGet]
        public async Task<IActionResult> ExportarPdf(int? estudianteId)
        {
            var rol = HttpContext.Session.GetString("Rol");
            var ruta = rol == "Estudiante"
                ? "api/Historial/ExportarPdfAPI"
                : $"api/Historial/ExportarPdfAPI?estudianteId={estudianteId}";

            var (exito, contenido, nombreArchivo, error) = await _api.DescargarArchivoAsync(ruta);

            if (!exito || contenido == null)
            {
                TempData["Error"] = error ?? "No fue posible generar el PDF.";
                return RedirectToAction("Index", new { estudianteId });
            }

            return File(contenido, "application/pdf", nombreArchivo ?? "Calificaciones.pdf");
        }

        // GET /Historial/ImportarCsv  (solo Docente)
        [SessionRol("Docente")]
        [HttpGet]
        public IActionResult ImportarCsv() => View();

        // POST /Historial/ImportarCsv
        [SessionRol("Docente")]
        [HttpPost]
        public async Task<IActionResult> ImportarCsv(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Debe seleccionar un archivo CSV.";
                return RedirectToAction("ImportarCsv");
            }

            var (exito, resultado, error) = await _api.SubirArchivoAsync<JsonElement>("api/Historial/ImportarCsvAPI", archivo);

            if (!exito)
            {
                TempData["Error"] = error ?? "No fue posible importar el archivo.";
                return RedirectToAction("ImportarCsv");
            }

            var insertadas = resultado.GetProperty("filasInsertadas").GetInt32();
            var procesadas = resultado.GetProperty("filasProcesadas").GetInt32();
            TempData["Mensaje"] = $"Importación completada: {insertadas} de {procesadas} filas insertadas correctamente.";

            if (resultado.TryGetProperty("errores", out var errores) && errores.GetArrayLength() > 0)
            {
                var total = errores.GetArrayLength();
                var detalle = string.Join(" | ", errores.EnumerateArray().Select(e => e.GetString()).Take(4));
                TempData["Error"] = total > 4 ? $"{detalle} | ... y {total - 4} error(es) más." : detalle;
            }

            return RedirectToAction("Index");
        }
    }
}
