using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Escuela_WEB.Filters;
using Escuela_WEB.Models;
using Escuela_WEB.Services;

namespace Escuela_WEB.Controllers
{
    [SessionRol("Director")]
    public class AdminController(ApiClienteService _api) : Controller
    {
        private async Task CargarSecciones()
        {
            var (exito, secciones, _) = await _api.GetAsync<List<string>>("api/Seccion/ConsultarActivasAPI");
            ViewBag.Secciones = exito && secciones != null ? secciones : new List<string>();
        }

        // =====================================================================
        //  USUARIOS
        // =====================================================================

        // GET /Admin/Usuarios
        [HttpGet]
        public async Task<IActionResult> Usuarios(string? rol)
        {
            ViewBag.FiltroRol = rol;
            ViewBag.IdPropio = HttpContext.Session.GetString("ConsecutivoUsuario");

            var ruta = string.IsNullOrWhiteSpace(rol) ? "api/Admin/ConsultarUsuariosAPI" : $"api/Admin/ConsultarUsuariosAPI?rol={Uri.EscapeDataString(rol)}";
            var (exito, usuarios, error) = await _api.GetAsync<List<UsuarioAdminViewModel>>(ruta);

            if (!exito)
            {
                TempData["Error"] = error;
                usuarios = new List<UsuarioAdminViewModel>();
            }

            return View(usuarios);
        }

        // GET /Admin/RegistrarUsuario
        [HttpGet]
        public async Task<IActionResult> RegistrarUsuario()
        {
            await CargarSecciones();
            return View(new RegistrarUsuarioAdminViewModel());
        }

        // POST /Admin/RegistrarUsuario
        [HttpPost]
        public async Task<IActionResult> RegistrarUsuario(RegistrarUsuarioAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarSecciones();
                return View(model);
            }

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Admin/RegistrarUsuarioAPI", new
            {
                model.Nombre,
                model.CorreoElectronico,
                model.Contrasenna,
                model.Rol,
                Seccion = model.Rol == "Estudiante" ? model.Seccion : null
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible crear el usuario.");
                await CargarSecciones();
                return View(model);
            }

            TempData["Mensaje"] = "Usuario creado correctamente.";
            return RedirectToAction("Usuarios");
        }

        // POST /Admin/Activar/5  y  POST /Admin/Desactivar/5
        // Dos acciones separadas (sin parámetro booleano en el formulario) para que no
        // dependan de cómo se escriba un true/false en el HTML.
        [HttpPost]
        public Task<IActionResult> Activar(int id) => CambiarEstado(id, true);

        [HttpPost]
        public Task<IActionResult> Desactivar(int id) => CambiarEstado(id, false);

        private async Task<IActionResult> CambiarEstado(int id, bool activo)
        {
            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Admin/CambiarEstadoUsuarioAPI/{id}", new { Activo = activo });

            if (exito) TempData["Mensaje"] = activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente.";
            else TempData["Error"] = error;

            return RedirectToAction("Usuarios");
        }

        // GET /Admin/EditarUsuario/5
        [HttpGet]
        public async Task<IActionResult> EditarUsuario(int id)
        {
            var (exito, usuarios, error) = await _api.GetAsync<List<UsuarioAdminViewModel>>("api/Admin/ConsultarUsuariosAPI");
            var usuario = exito ? usuarios?.FirstOrDefault(u => u.ConsecutivoUsuario == id) : null;

            if (usuario == null)
            {
                TempData["Error"] = error ?? "Usuario no encontrado.";
                return RedirectToAction("Usuarios");
            }

            await CargarSecciones();
            return View(new EditarUsuarioViewModel
            {
                ConsecutivoUsuario = usuario.ConsecutivoUsuario,
                Nombre = usuario.Nombre,
                CorreoElectronico = usuario.CorreoElectronico,
                Rol = usuario.Rol,
                Seccion = usuario.Seccion
            });
        }

        // POST /Admin/EditarUsuario
        [HttpPost]
        public async Task<IActionResult> EditarUsuario(EditarUsuarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarSecciones();
                return View(model);
            }

            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Admin/CambiarRolUsuarioAPI/{model.ConsecutivoUsuario}", new
            {
                model.Rol,
                Seccion = model.Rol == "Estudiante" ? model.Seccion : null
            });

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible actualizar el usuario.");
                await CargarSecciones();
                return View(model);
            }

            TempData["Mensaje"] = "Usuario actualizado correctamente.";
            return RedirectToAction("Usuarios");
        }

        // POST /Admin/RestablecerClave/5
        // La contraseña temporal se muestra UNA vez al Director para que se la entregue al usuario.
        [HttpPost]
        public async Task<IActionResult> RestablecerClave(int id)
        {
            var (exito, datos, error) = await _api.PutAsync<JsonElement>($"api/Admin/RestablecerContrasennaAPI/{id}", new { });

            if (!exito)
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["ClaveUsuario"] = datos.GetProperty("nombre").GetString();
                TempData["ClaveTemporal"] = datos.GetProperty("contrasennaTemporal").GetString();
            }

            return RedirectToAction("Usuarios");
        }

        // =====================================================================
        //  SECCIONES
        // =====================================================================

        // GET /Admin/Secciones
        [HttpGet]
        public async Task<IActionResult> Secciones()
        {
            var (exito, secciones, error) = await _api.GetAsync<List<SeccionAdminViewModel>>("api/Admin/ConsultarSeccionesAPI");
            var (exitoMaterias, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");

            if (!exito) TempData["Error"] = error;

            ViewBag.Materias = exitoMaterias ? materias ?? new() : new();
            return View(secciones ?? new List<SeccionAdminViewModel>());
        }

        // POST /Admin/RegistrarSeccion
        [HttpPost]
        public async Task<IActionResult> RegistrarSeccion(string nombre)
        {
            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Admin/RegistrarSeccionAPI", new { Nombre = nombre });

            if (exito) TempData["Mensaje"] = "Sección creada correctamente.";
            else TempData["Error"] = error;

            return RedirectToAction("Secciones");
        }

        [HttpPost]
        public Task<IActionResult> ActivarSeccion(int id) => CambiarEstadoSeccion(id, true);

        [HttpPost]
        public Task<IActionResult> DesactivarSeccion(int id) => CambiarEstadoSeccion(id, false);

        private async Task<IActionResult> CambiarEstadoSeccion(int id, bool activa)
        {
            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Admin/CambiarEstadoSeccionAPI/{id}", new { Activa = activa });

            if (exito) TempData["Mensaje"] = activa ? "Sección activada." : "Sección desactivada.";
            else TempData["Error"] = error;

            return RedirectToAction("Secciones");
        }

        // =====================================================================
        //  AULAS
        // =====================================================================

        // GET /Admin/Aulas
        [HttpGet]
        public async Task<IActionResult> Aulas()
        {
            var (exito, aulas, error) = await _api.GetAsync<List<AulaViewModel>>("api/Admin/ConsultarAulasAPI");
            if (!exito) TempData["Error"] = error;

            return View(aulas ?? new List<AulaViewModel>());
        }

        // POST /Admin/RegistrarAula
        [HttpPost]
        public async Task<IActionResult> RegistrarAula(string nombre)
        {
            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Admin/RegistrarAulaAPI", new { Nombre = nombre });

            if (exito) TempData["Mensaje"] = "Aula creada correctamente.";
            else TempData["Error"] = error;

            return RedirectToAction("Aulas");
        }

        [HttpPost]
        public Task<IActionResult> ActivarAula(int id) => CambiarEstadoAula(id, true);

        [HttpPost]
        public Task<IActionResult> DesactivarAula(int id) => CambiarEstadoAula(id, false);

        private async Task<IActionResult> CambiarEstadoAula(int id, bool activa)
        {
            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Admin/CambiarEstadoAulaAPI/{id}", new { Activa = activa });

            if (exito) TempData["Mensaje"] = activa ? "Aula activada." : "Aula desactivada.";
            else TempData["Error"] = error;

            return RedirectToAction("Aulas");
        }

        // =====================================================================
        //  BITÁCORA
        // =====================================================================

        // GET /Admin/Errores  -> bitácora de errores no controlados (RegistroError, ver Middleware de la API)
        [HttpGet]
        public async Task<IActionResult> Errores()
        {
            var (exito, errores, error) = await _api.GetAsync<List<RegistroErrorViewModel>>("api/Admin/ConsultarErroresAPI");

            if (!exito) TempData["Error"] = error;

            return View(errores ?? new List<RegistroErrorViewModel>());
        }
    }
}
