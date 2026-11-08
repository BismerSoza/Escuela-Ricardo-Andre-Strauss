using Microsoft.Data.SqlClient;
using System.Net;
using System.Text.Json;

namespace Escuela_API.Middleware
{
    // Captura CUALQUIER excepción no controlada en toda la API (fallo de SQL,
    // referencia nula, etc.) y en lugar de tumbar la aplicación o devolver el
    // HTML/stack-trace de ASP.NET, responde SIEMPRE con un JSON consistente
    // { exito:false, mensaje: "..." } que el frontend (WEB) puede interpretar
    // y mostrar como una alerta amigable, sin sacar al usuario de la pantalla.
    public class ManejadorErroresMiddleware(RequestDelegate _siguiente, ILogger<ManejadorErroresMiddleware> _logger, IConfiguration _config)
    {
        public async Task InvokeAsync(HttpContext contexto)
        {
            try
            {
                await _siguiente(contexto);
            }
            catch (Exception excepcion)
            {
                _logger.LogError(excepcion, "Error no controlado en {Ruta}", contexto.Request.Path);
                await RegistrarErrorEnBaseDeDatos(contexto.Request.Path, excepcion);
                await EscribirRespuestaError(contexto, excepcion);
            }
        }

        private async Task RegistrarErrorEnBaseDeDatos(string ruta, Exception excepcion)
        {
            try
            {
                using var conexion = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]);
                await conexion.OpenAsync();
                using var comando = new SqlCommand(
                    "INSERT INTO RegistroError (Ruta, Mensaje, Detalle) VALUES (@Ruta, @Mensaje, @Detalle)", conexion);
                comando.Parameters.AddWithValue("@Ruta", ruta);
                comando.Parameters.AddWithValue("@Mensaje", excepcion.Message);
                comando.Parameters.AddWithValue("@Detalle", excepcion.ToString());
                await comando.ExecuteNonQueryAsync();
            }
            catch
            {
                // Si ni siquiera se puede escribir el log (ej. la BD está caída),
                // no se debe lanzar una segunda excepción: ya se registró en ILogger.
            }
        }

        private static async Task EscribirRespuestaError(HttpContext contexto, Exception excepcion)
        {
            contexto.Response.ContentType = "application/json";

            var (codigo, mensaje) = ClasificarError(excepcion);
            contexto.Response.StatusCode = codigo;

            var cuerpo = JsonSerializer.Serialize(new
            {
                exito = false,
                mensaje
            });

            await contexto.Response.WriteAsync(cuerpo);
        }

        private static (int Codigo, string Mensaje) ClasificarError(Exception excepcion)
        {
            return excepcion switch
            {
                SqlException => ((int)HttpStatusCode.ServiceUnavailable,
                    "No fue posible conectar con la base de datos. Intente nuevamente en unos minutos."),
                TimeoutException => ((int)HttpStatusCode.RequestTimeout,
                    "La operación tardó demasiado tiempo. Intente nuevamente."),
                UnauthorizedAccessException => ((int)HttpStatusCode.Forbidden,
                    "No tiene permisos para realizar esta acción."),
                _ => ((int)HttpStatusCode.InternalServerError,
                    "Ocurrió un error inesperado al procesar la solicitud. Ya fue registrado para su revisión.")
            };
        }
    }

    public static class ManejadorErroresMiddlewareExtensions
    {
        public static IApplicationBuilder UseManejadorErrores(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ManejadorErroresMiddleware>();
        }
    }
}
