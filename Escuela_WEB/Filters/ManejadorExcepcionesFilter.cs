using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Escuela_WEB.Services;

namespace Escuela_WEB.Filters
{
    // Cualquier excepción no controlada que ocurra dentro de un Controller/Action
    // (por ejemplo, si la API está caída) se captura aquí. En vez de mostrar la
    // pantalla amarilla de ASP.NET, se le muestra al usuario una vista amigable
    // y el detalle se registra en el log del servidor.
    public class ManejadorExcepcionesFilter(ILogger<ManejadorExcepcionesFilter> _logger) : IExceptionFilter
    {
        public void OnException(ExceptionContext contexto)
        {
            var peticion = contexto.HttpContext.Request;
            var esAjax = peticion.Headers["X-Requested-With"] == "fetch"
                         || peticion.Headers.Accept.ToString().Contains("application/json");

            // La cuenta fue desactivada, cambió de rol o venció la sesión: se cierra la sesión local.
            if (contexto.Exception is SesionExpiradaException)
            {
                contexto.HttpContext.Session.Clear();

                contexto.Result = esAjax
                    ? new StatusCodeResult(StatusCodes.Status401Unauthorized)
                    : new RedirectToActionResult("Index", "Home", new { expirada = 1 });

                contexto.ExceptionHandled = true;
                return;
            }

            _logger.LogError(contexto.Exception, "Error no controlado en {Ruta}", peticion.Path);

            if (esAjax)
            {
                contexto.Result = new ObjectResult("Ocurrió un error inesperado. Intente nuevamente.") { StatusCode = 500 };
                contexto.ExceptionHandled = true;
                return;
            }

            contexto.Result = new ViewResult
            {
                ViewName = "Error",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    contexto.ModelState)
                {
                    ["Mensaje"] = "Ocurrió un error inesperado. Por favor intente nuevamente."
                }
            };

            contexto.ExceptionHandled = true;
        }
    }
}
