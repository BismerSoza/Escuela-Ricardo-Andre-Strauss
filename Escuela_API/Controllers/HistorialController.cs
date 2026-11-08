using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Escuela_API.Models;
using Escuela_API.Services;
using System.Globalization;

namespace Escuela_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HistorialController(IConfiguration _config, IUtilesService _utiles) : ControllerBase
    {
        private SqlConnection Conexion => new(_config["ConnectionStrings:DefaultConnection"]);

        // GET api/Historial/ConsultarHistorialAPI
        // Estudiante -> su propio historial. Docente/Director -> requieren ?estudianteId=
        [HttpGet("ConsultarHistorialAPI")]
        public IActionResult ConsultarHistorialAPI([FromQuery] int? estudianteId)
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();

            if (rol == "Estudiante")
            {
                return Ok(ObtenerHistorialEstudiante(context, _utiles.ObtenerConsecutivoToken()));
            }

            if (estudianteId == null)
            {
                return BadRequest("Debe indicar el parámetro estudianteId.");
            }

            return Ok(ObtenerHistorialEstudiante(context, estudianteId.Value));
        }

        private static IEnumerable<HistorialResponseModel> ObtenerHistorialEstudiante(SqlConnection context, int estudianteId)
        {
            return context.Query<HistorialResponseModel>(@"
                SELECT h.ConsecutivoHistorial, h.Materia, h.Periodo, h.Nota, h.Observacion, h.FechaCreacion,
                       d.Nombre AS Docente, e.Nombre AS Estudiante
                FROM HistorialAcademico h
                INNER JOIN Usuario d ON d.ConsecutivoUsuario = h.ConsecutivoDocente
                INNER JOIN Usuario e ON e.ConsecutivoUsuario = h.ConsecutivoEstudiante
                WHERE h.ConsecutivoEstudiante = @Id
                ORDER BY h.FechaCreacion DESC", new { Id = estudianteId });
        }

        // POST api/Historial/RegistrarHistorialAPI  (solo Docente)
        [Authorize(Roles = "Docente")]
        [HttpPost("RegistrarHistorialAPI")]
        public IActionResult RegistrarHistorialAPI(RegistrarHistorialRequestModel model)
        {
            if (model.ConsecutivoEstudiante == 0 || string.IsNullOrWhiteSpace(model.Materia)
                || string.IsNullOrWhiteSpace(model.Periodo))
            {
                return BadRequest("Estudiante, materia y periodo son obligatorios.");
            }

            using var context = Conexion;

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO HistorialAcademico
                    (ConsecutivoEstudiante, ConsecutivoDocente, ConsecutivoMateria, Materia, Periodo, Nota, Observacion)
                OUTPUT INSERTED.ConsecutivoHistorial
                VALUES (@Estudiante, @Docente, @ConsecutivoMateria, @Materia, @Periodo, @Nota, @Observacion)",
                new
                {
                    Estudiante = model.ConsecutivoEstudiante,
                    Docente = _utiles.ObtenerConsecutivoToken(),
                    model.ConsecutivoMateria,
                    model.Materia,
                    model.Periodo,
                    model.Nota,
                    model.Observacion
                });

            return Ok(new { Mensaje = "Registro académico agregado.", ConsecutivoHistorial = nuevoId });
        }

        // POST api/Historial/ImportarCsvAPI  (Docente sube un archivo CSV con calificaciones masivas)
        // Columnas esperadas: CorreoEstudiante,Materia,Periodo,Nota,Observacion
        [Authorize(Roles = "Docente")]
        [HttpPost("ImportarCsvAPI")]
        public async Task<IActionResult> ImportarCsvAPI(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest("Debe adjuntar un archivo CSV.");
            }

            var resultado = new ImportarCalificacionesResultModel();
            using var context = Conexion;
            var docenteId = _utiles.ObtenerConsecutivoToken();

            using var lector = new StreamReader(archivo.OpenReadStream());
            using var csv = new CsvReader(lector, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                resultado.FilasProcesadas++;
                try
                {
                    var correo = csv.GetField("CorreoEstudiante")?.Trim();
                    var materia = csv.GetField("Materia")?.Trim();
                    var periodo = csv.GetField("Periodo")?.Trim();
                    var notaTexto = csv.GetField("Nota")?.Trim();
                    var observacion = csv.GetField("Observacion")?.Trim();

                    if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(materia) || string.IsNullOrWhiteSpace(periodo))
                    {
                        resultado.Errores.Add($"Fila {resultado.FilasProcesadas}: correo, materia y periodo son obligatorios.");
                        continue;
                    }

                    var estudianteId = context.QueryFirstOrDefault<int?>(
                        "SELECT ConsecutivoUsuario FROM Usuario WHERE CorreoElectronico = @Correo AND Rol = 'Estudiante'",
                        new { Correo = correo });

                    if (estudianteId == null)
                    {
                        resultado.Errores.Add($"Fila {resultado.FilasProcesadas}: no existe un estudiante con el correo '{correo}'.");
                        continue;
                    }

                    decimal? nota = decimal.TryParse(notaTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null;

                    context.Execute(@"
                        INSERT INTO HistorialAcademico (ConsecutivoEstudiante, ConsecutivoDocente, Materia, Periodo, Nota, Observacion)
                        VALUES (@Estudiante, @Docente, @Materia, @Periodo, @Nota, @Observacion)",
                        new { Estudiante = estudianteId, Docente = docenteId, Materia = materia, Periodo = periodo, Nota = nota, Observacion = observacion });

                    resultado.FilasInsertadas++;
                }
                catch (Exception ex)
                {
                    resultado.Errores.Add($"Fila {resultado.FilasProcesadas}: {ex.Message}");
                }
            }

            return Ok(resultado);
        }

        // GET api/Historial/ExportarPdfAPI?estudianteId=3  -> genera el reporte de calificaciones en PDF
        [HttpGet("ExportarPdfAPI")]
        public IActionResult ExportarPdfAPI([FromQuery] int? estudianteId)
        {
            using var context = Conexion;
            var rol = _utiles.ObtenerRolToken();
            var idConsultado = rol == "Estudiante" ? _utiles.ObtenerConsecutivoToken() : estudianteId;

            if (idConsultado == null) return BadRequest("Debe indicar el parámetro estudianteId.");

            var estudiante = context.QueryFirstOrDefault(
                "SELECT Nombre, Seccion FROM Usuario WHERE ConsecutivoUsuario = @Id", new { Id = idConsultado });

            if (estudiante == null) return NotFound("Estudiante no encontrado.");

            var registros = ObtenerHistorialEstudiante(context, idConsultado.Value).ToList();

            var documentoPdf = Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.Letter);
                    pagina.Margin(35);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                    pagina.Header().Column(columna =>
                    {
                        columna.Item().Text("Escuela Ricardo André Strauss").FontSize(18).Bold();
                        columna.Item().Text("Reporte de Calificaciones — MiniCampus").FontSize(12).FontColor(Colors.Grey.Darken1);
                        columna.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    pagina.Content().PaddingVertical(15).Column(columna =>
                    {
                        columna.Item().Text($"Estudiante: {estudiante.Nombre}").Bold();
                        columna.Item().Text($"Sección: {estudiante.Seccion}");
                        columna.Item().Text($"Fecha de emisión: {DateTime.Now:dd/MM/yyyy HH:mm}");
                        columna.Item().PaddingTop(10);

                        columna.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(columnas =>
                            {
                                columnas.RelativeColumn(3);
                                columnas.RelativeColumn(2);
                                columnas.RelativeColumn(1);
                                columnas.RelativeColumn(3);
                                columnas.RelativeColumn(2);
                            });

                            tabla.Header(encabezado =>
                            {
                                foreach (var titulo in new[] { "Materia", "Periodo", "Nota", "Observación", "Docente" })
                                {
                                    encabezado.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                        .Text(titulo).FontColor(Colors.White).Bold();
                                }
                            });

                            foreach (var registro in registros)
                            {
                                tabla.Cell().Padding(5).Text(registro.Materia);
                                tabla.Cell().Padding(5).Text(registro.Periodo);
                                tabla.Cell().Padding(5).Text(registro.Nota?.ToString("0.##") ?? "-");
                                tabla.Cell().Padding(5).Text(registro.Observacion ?? "-");
                                tabla.Cell().Padding(5).Text(registro.Docente);
                            }
                        });

                        if (registros.Count > 0)
                        {
                            var promedio = registros.Where(r => r.Nota.HasValue).Select(r => r.Nota!.Value).DefaultIfEmpty(0).Average();
                            columna.Item().PaddingTop(15).Text($"Promedio general: {promedio:0.##}").Bold().FontSize(13);
                        }
                    });

                    pagina.Footer().AlignCenter().Text(texto =>
                    {
                        texto.Span("Documento generado automáticamente por MiniCampus — ").FontSize(9);
                        texto.Span(DateTime.Now.Year.ToString()).FontSize(9);
                    });
                });
            }).GeneratePdf();

            var nombreArchivo = $"Calificaciones_{estudiante.Nombre.ToString().Replace(" ", "_")}.pdf";
            return File(documentoPdf, "application/pdf", nombreArchivo);
        }
    }
}
