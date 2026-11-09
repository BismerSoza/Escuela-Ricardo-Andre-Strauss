using Microsoft.AspNetCore.Mvc;

namespace Escuela_WEB.Controllers
{
    // Cubre errores que ocurren fuera del pipeline de MVC (middleware, rutas inexistentes).
    // Los errores dentro de un Action ya los captura ManejadorExcepcionesFilter.
    public class ErrorController : Controller
    {
        [Route("/Error/{codigo?}")]
        public IActionResult Index(int? codigo)
        {
            ViewData["Mensaje"] = codigo == 404
                ? "La página que busca no existe o fue movida."
                : "Ocurrió un error inesperado. Por favor intente nuevamente.";
            return View("Error");
        }
    }
}
