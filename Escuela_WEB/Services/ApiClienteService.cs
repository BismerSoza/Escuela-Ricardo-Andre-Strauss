using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Escuela_WEB.Services
{
    // Se lanza cuando la API rechaza el token (401): la sesión venció, o el Director
    // desactivó la cuenta / cambió su rol. El filtro global la convierte en un regreso al login.
    public class SesionExpiradaException : Exception
    {
        public SesionExpiradaException() : base("La sesión ya no es válida.") { }
    }

    // Encapsula las llamadas HTTP hacia Escuela_API, agregando automaticamente
    // el token JWT guardado en la sesion del usuario autenticado.
    public class ApiClienteService(IHttpClientFactory _httpClientFactory, IHttpContextAccessor _httpContext, IConfiguration _config)
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private HttpClient CrearCliente()
        {
            var cliente = _httpClientFactory.CreateClient();
            cliente.BaseAddress = new Uri(_config["Api:BaseUrl"]!);

            var token = _httpContext.HttpContext?.Session.GetString("Token");
            if (!string.IsNullOrEmpty(token))
            {
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return cliente;
        }

        public async Task<(bool Exito, T Datos, string? Error)> GetAsync<T>(string ruta)
        {
            var cliente = CrearCliente();
            var respuesta = await cliente.GetAsync(ruta);
            return await ProcesarRespuesta<T>(respuesta);
        }

        public async Task<(bool Exito, T Datos, string? Error)> PostAsync<T>(string ruta, object cuerpo)
        {
            var cliente = CrearCliente();
            var respuesta = await cliente.PostAsJsonAsync(ruta, cuerpo);
            return await ProcesarRespuesta<T>(respuesta);
        }

        public async Task<(bool Exito, T Datos, string? Error)> PutAsync<T>(string ruta, object cuerpo)
        {
            var cliente = CrearCliente();
            var respuesta = await cliente.PutAsJsonAsync(ruta, cuerpo);
            return await ProcesarRespuesta<T>(respuesta);
        }

        public async Task<(bool Exito, string? Error)> DeleteAsync(string ruta)
        {
            var cliente = CrearCliente();
            var respuesta = await cliente.DeleteAsync(ruta);
            VerificarSesion(respuesta);

            if (respuesta.IsSuccessStatusCode) return (true, null);

            var contenido = await respuesta.Content.ReadAsStringAsync();
            return (false, ExtraerMensajeError(contenido));
        }

        // Descarga un archivo binario (ej. PDF) generado por la API.
        public async Task<(bool Exito, byte[]? Contenido, string? NombreArchivo, string? Error)> DescargarArchivoAsync(string ruta)
        {
            var cliente = CrearCliente();
            var respuesta = await cliente.GetAsync(ruta);
            VerificarSesion(respuesta);

            if (!respuesta.IsSuccessStatusCode)
            {
                var contenidoError = await respuesta.Content.ReadAsStringAsync();
                return (false, null, null, ExtraerMensajeError(contenidoError));
            }

            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            var nombreArchivo = respuesta.Content.Headers.ContentDisposition?.FileNameStar
                ?? respuesta.Content.Headers.ContentDisposition?.FileName
                ?? "descarga.pdf";

            return (true, bytes, nombreArchivo.Trim('"'), null);
        }

        // Sube un archivo (ej. CSV de calificaciones) como multipart/form-data.
        public async Task<(bool Exito, T Datos, string? Error)> SubirArchivoAsync<T>(string ruta, IFormFile archivo, string nombreCampo = "archivo")
        {
            var cliente = CrearCliente();
            using var contenido = new MultipartFormDataContent();
            using var streamArchivo = archivo.OpenReadStream();
            using var contenidoArchivo = new StreamContent(streamArchivo);
            contenidoArchivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                archivo.ContentType == "" ? "application/octet-stream" : archivo.ContentType);

            contenido.Add(contenidoArchivo, nombreCampo, archivo.FileName);

            var respuesta = await cliente.PostAsync(ruta, contenido);
            return await ProcesarRespuesta<T>(respuesta);
        }

        // 401 con un token enviado = sesión inválida. (Un 401 del login no lleva token y se muestra como error normal.)
        private static void VerificarSesion(HttpResponseMessage respuesta)
        {
            if (respuesta.StatusCode == System.Net.HttpStatusCode.Unauthorized
                && respuesta.RequestMessage?.Headers.Authorization != null)
            {
                throw new SesionExpiradaException();
            }
        }

        private static async Task<(bool, T, string?)> ProcesarRespuesta<T>(HttpResponseMessage respuesta)
        {
            VerificarSesion(respuesta);

            var contenido = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
            {
                return (false, default!, ExtraerMensajeError(contenido));
            }

            if (string.IsNullOrWhiteSpace(contenido))
            {
                return (true, default!, null);
            }

            try
            {
                var datos = JsonSerializer.Deserialize<T>(contenido, _jsonOptions);
                return (true, datos!, null);
            }
            catch
            {
                return (true, default!, null);
            }
        }

        private static string ExtraerMensajeError(string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido)) return "Ocurrió un error al comunicarse con el servidor.";

            try
            {
                using var documento = JsonDocument.Parse(contenido);
                if (documento.RootElement.TryGetProperty("title", out var titulo))
                    return titulo.GetString() ?? contenido;
            }
            catch
            {
                // El contenido no es JSON (ej. texto plano de BadRequest("...")); se devuelve tal cual.
            }

            return contenido.Trim('"');
        }
    }
}
