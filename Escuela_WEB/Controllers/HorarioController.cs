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

        // GET /Horario?tipo=seccion&valor=Sexto Grado
        // Calendario semanal. Siempre se muestra UN solo contexto (una sección, un aula o un docente)
        // para que los bloques jamás se encimen visualmente.
        [HttpGet]
        public async Task<IActionResult> Index(string? tipo, string? valor)
        {
            var rol = HttpContext.Session.GetString("Rol") ?? string.Empty;

            var model = new PanelHorarioViewModel
            {
                Rol = rol,
                ConsecutivoUsuario = int.TryParse(HttpContext.Session.GetString("ConsecutivoUsuario"), out var id) ? id : 0
            };

            if (rol != "Estudiante")
            {
                var (exitoS, secciones, _) = await _api.GetAsync<List<string>>("api/Seccion/ConsultarActivasAPI");
                model.Secciones = exitoS && secciones != null ? secciones : new();

                var (exitoA, aulas, _) = await _api.GetAsync<List<AulaViewModel>>("api/Horario/ConsultarAulasAPI");
                model.Aulas = exitoA && aulas != null ? aulas : new();

                if (rol == "Director")
                {
                    var (exitoD, docentes, _) = await _api.GetAsync<List<UsuarioAdminViewModel>>("api/Admin/ConsultarUsuariosAPI?rol=Docente");
                    model.Docentes = exitoD && docentes != null ? docentes.Where(d => d.Activo).ToList() : new();
                }

                tipo = (tipo ?? string.Empty).ToLowerInvariant();

                // Contexto por defecto: el docente ve su propio horario; el Director, la primera sección.
                if (tipo != "mio" && tipo != "seccion" && tipo != "aula" && tipo != "docente")
                {
                    tipo = rol == "Docente" ? "mio" : "seccion";
                }

                if (rol == "Docente" && tipo == "docente") tipo = "mio";

                // Sin filtro elegido, el Director abre la primera sección que ya tiene clases
                // (en vez de una sección vacía), para que el calendario nunca arranque en blanco.
                if (rol == "Director" && tipo == "seccion" && string.IsNullOrWhiteSpace(valor))
                {
                    var (exitoT, todos, _) = await _api.GetAsync<List<HorarioViewModel>>("api/Horario/ConsultarHorarioAPI");
                    valor = exitoT && todos != null
                        ? todos.Select(b => b.Seccion).FirstOrDefault(sec => model.Secciones.Contains(sec))
                        : null;
                }

                if (string.IsNullOrWhiteSpace(valor))
                {
                    valor = tipo switch
                    {
                        "seccion" => model.Secciones.FirstOrDefault(),
                        "aula" => model.Aulas.FirstOrDefault()?.Nombre,
                        "docente" => model.Docentes.FirstOrDefault()?.ConsecutivoUsuario.ToString(),
                        _ => null
                    };
                }

                model.Tipo = tipo;
                model.Valor = valor;
            }

            var ruta = "api/Horario/ConsultarHorarioAPI";
            if (rol != "Estudiante" && !string.IsNullOrEmpty(model.Tipo))
            {
                ruta += $"?tipo={Uri.EscapeDataString(model.Tipo)}&valor={Uri.EscapeDataString(model.Valor ?? string.Empty)}";
            }

            // Si no hay nada que filtrar (ej. Director sin secciones) no se consulta todo el horario.
            var hayContexto = rol == "Estudiante" || model.Tipo == "mio" || !string.IsNullOrEmpty(model.Valor);

            if (hayContexto)
            {
                var (exito, horario, error) = await _api.GetAsync<List<HorarioViewModel>>(ruta);
                if (!exito) TempData["Error"] = error;
                model.Bloques = exito && horario != null ? horario : new();
            }

            return View(model);
        }

        private async Task CargarListasFormulario()
        {
            var (exito, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");
            ViewBag.Materias = (exito && materias != null ? materias : new()).Where(m => m.Activa).ToList();

            var (exitoA, aulas, _) = await _api.GetAsync<List<AulaViewModel>>("api/Horario/ConsultarAulasAPI");
            ViewBag.Aulas = exitoA && aulas != null ? aulas : new List<AulaViewModel>();

            ViewBag.Dias = Dias;
        }

        // GET /Horario/Registrar  (Director o Docente)
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            await CargarListasFormulario();
            return View(new RegistrarHorarioViewModel());
        }

        // POST /Horario/Registrar
        // Si el espacio ya está ocupado, la API responde con el detalle del choque y aquí se
        // vuelve a mostrar el formulario con los datos escritos y el mensaje, sin perder nada.
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Registrar(RegistrarHorarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarListasFormulario();
                return View(model);
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Horario/RegistrarHorarioAPI", new
            {
                model.ConsecutivoMateria,
                model.DiaSemana,
                model.HoraInicio,
                model.HoraFin,
                model.ConsecutivoAula
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible agregar el bloque.");
                await CargarListasFormulario();
                return View(model);
            }

            TempData["Mensaje"] = "Bloque de horario agregado correctamente.";
            return RedirectToAction("Index", "Horario");
        }

        // GET /Horario/Verificar?...  -> JSON { disponible, conflictos[] } para el aviso en vivo del formulario.
        [SessionRol("Director", "Docente")]
        [HttpGet]
        public async Task<IActionResult> Verificar(int consecutivoMateria, string diaSemana, string horaInicio, string horaFin, int? consecutivoAula)
        {
            var ruta = "api/Horario/VerificarDisponibilidadAPI"
                       + $"?consecutivoMateria={consecutivoMateria}&diaSemana={Uri.EscapeDataString(diaSemana ?? string.Empty)}"
                       + $"&horaInicio={Uri.EscapeDataString(horaInicio ?? string.Empty)}&horaFin={Uri.EscapeDataString(horaFin ?? string.Empty)}"
                       + (consecutivoAula.HasValue ? $"&consecutivoAula={consecutivoAula}" : string.Empty);

            var (exito, datos, error) = await _api.GetAsync<JsonElement>(ruta);

            if (!exito) return BadRequest(error);

            return Json(datos);
        }

        // POST /Horario/Eliminar/5
        [SessionRol("Director", "Docente")]
        [HttpPost]
        public async Task<IActionResult> Eliminar(int id, string? tipo, string? valor)
        {
            var (exito, error) = await _api.DeleteAsync($"api/Horario/EliminarHorarioAPI/{id}");
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Bloque de horario eliminado." : error;
            return RedirectToAction("Index", new { tipo, valor });
        }
    }
}
