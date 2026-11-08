using Microsoft.AspNetCore.Mvc;

namespace Escuela_WEB.Controllers
{
    // Cubre errores que ocurren fuera del pipeline de MVC (por ejemplo, en
    // middleware) cuando la aplicación corre en modo Production. Los errores
    // dentro de un Action ya son capturados antes por ManejadorExcepcionesFilter.
    public class ErrorController : Controller
    {
        [Route("/Error")]
        public IActionResult Index()
        {
            ViewData["Mensaje"] = "Ocurrió un error inesperado. Por favor intente nuevamente.";
            return View("Error");
        }
    }
}
