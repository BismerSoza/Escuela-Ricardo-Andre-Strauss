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
        // GET /Admin/Usuarios
        [HttpGet]
        public async Task<IActionResult> Usuarios(string? rol)
        {
            ViewBag.FiltroRol = rol;

            var ruta = string.IsNullOrWhiteSpace(rol) ? "api/Admin/ConsultarUsuariosAPI" : $"api/Admin/ConsultarUsuariosAPI?rol={rol}";
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
        public IActionResult RegistrarUsuario() => View(new RegistrarUsuarioAdminViewModel());

        // POST /Admin/RegistrarUsuario
        [HttpPost]
        public async Task<IActionResult> RegistrarUsuario(RegistrarUsuarioAdminViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (exito, _, error) = await _api.PostAsync<JsonElement>("api/Admin/RegistrarUsuarioAPI", model);

            if (!exito)
            {
                ModelState.AddModelError(string.Empty, error ?? "No fue posible crear el usuario.");
                return View(model);
            }

            TempData["Mensaje"] = "Usuario creado correctamente.";
            return RedirectToAction("Usuarios");
        }

        // POST /Admin/CambiarEstado
        [HttpPost]
        public async Task<IActionResult> CambiarEstado(int id, bool activo)
        {
            var (exito, _, error) = await _api.PutAsync<JsonElement>($"api/Admin/CambiarEstadoUsuarioAPI/{id}", new { Activo = activo });
            TempData[exito ? "Mensaje" : "Error"] = exito ? "Estado actualizado correctamente." : error;
            return RedirectToAction("Usuarios");
        }

        // GET /Admin/Secciones
        [HttpGet]
        public async Task<IActionResult> Secciones()
        {
            var (exito, secciones, error) = await _api.GetAsync<List<string>>("api/Admin/ConsultarSeccionesAPI");
            var (exitoMaterias, materias, _) = await _api.GetAsync<List<MateriaViewModel>>("api/Materia/ConsultarMateriasAPI");

            if (!exito) TempData["Error"] = error;

            ViewBag.Materias = exitoMaterias ? materias ?? new() : new();
            return View(secciones ?? new List<string>());
        }

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
