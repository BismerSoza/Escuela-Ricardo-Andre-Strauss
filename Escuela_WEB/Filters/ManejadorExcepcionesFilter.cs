using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Escuela_WEB.Filters
{
    // Cualquier excepción no controlada que ocurra dentro de un Controller/Action
    // (por ejemplo, si la API está caída y algo no maneja el error correctamente)
    // se captura aquí. En vez de mostrar la pantalla amarilla de ASP.NET o
    // sacar al usuario de la aplicación, se le redirige a una vista de error
    // amigable, y el detalle se registra en el log del servidor.
    public class ManejadorExcepcionesFilter(ILogger<ManejadorExcepcionesFilter> _logger) : IExceptionFilter
    {
        public void OnException(ExceptionContext contexto)
        {
            _logger.LogError(contexto.Exception, "Error no controlado en {Ruta}", contexto.HttpContext.Request.Path);

            contexto.Result = new ViewResult
            {
                ViewName = "Error",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    contexto.ModelState)
                {
                    ["Mensaje"] = "Ocurrió un error inesperado. Nuestro equipo ya fue notificado; por favor intente nuevamente."
                }
            };

            contexto.ExceptionHandled = true;
        }
    }
}
