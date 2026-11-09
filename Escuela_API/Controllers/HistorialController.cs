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
        // Estudiante -> su propio historial. Director -> cualquiera.
        // Docente -> solo estudiantes de las secciones donde dicta clases (requiere ?estudianteId=).
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

            if (!PuedeVerEstudiante(context, rol, estudianteId.Value)) return Forbid();

            return Ok(ObtenerHistorialEstudiante(context, estudianteId.Value));
        }

        private bool PuedeVerEstudiante(SqlConnection context, string rol, int estudianteId)
        {
            if (rol == "Director") return true;
            if (rol == "Estudiante") return estudianteId == _utiles.ObtenerConsecutivoToken();

            return AccesoHelper.DocenteTieneEstudiante(context, _utiles.ObtenerConsecutivoToken(), estudianteId);
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
        // La asignatura debe ser del docente y el estudiante debe estar en la sección de esa asignatura.
        [Authorize(Roles = "Docente")]
        [HttpPost("RegistrarHistorialAPI")]
        public IActionResult RegistrarHistorialAPI(RegistrarHistorialRequestModel model)
        {
            var periodo = ValidacionHelper.LimpiarNombre(model.Periodo);

            if (model.ConsecutivoEstudiante == 0 || model.ConsecutivoMateria == null || periodo.Length == 0)
            {
                return BadRequest("Estudiante, asignatura y periodo son obligatorios.");
            }

            if (periodo.Length > 50) return BadRequest("El periodo es demasiado largo (máximo 50 caracteres).");

            if (model.Nota != null && (model.Nota < 0 || model.Nota > 100))
            {
                return BadRequest("La nota debe estar entre 0 y 100.");
            }

            var observacion = string.IsNullOrWhiteSpace(model.Observacion) ? null : model.Observacion.Trim();
            if (observacion != null && observacion.Length > 400)
            {
                return BadRequest("La observación no puede superar 400 caracteres.");
            }

            using var context = Conexion;
            var docenteId = _utiles.ObtenerConsecutivoToken();

            var materia = context.QueryFirstOrDefault<MateriaBasica>(@"
                SELECT ConsecutivoMateria, Nombre, Seccion, ConsecutivoDocente, Activa
                FROM Materia
                WHERE ConsecutivoMateria = @Id AND ConsecutivoDocente = @Docente AND Activa = 1",
                new { Id = model.ConsecutivoMateria.Value, Docente = docenteId });

            if (materia == null) return BadRequest("La asignatura no existe o no está a su cargo.");

            var seccionEstudiante = context.QueryFirstOrDefault<string?>(
                "SELECT Seccion FROM Usuario WHERE ConsecutivoUsuario = @Id AND Rol = 'Estudiante' AND Activo = 1",
                new { Id = model.ConsecutivoEstudiante });

            if (seccionEstudiante == null || seccionEstudiante != materia.Seccion)
            {
                return BadRequest("El estudiante no pertenece a la sección de esa asignatura.");
            }

            var nuevoId = context.QueryFirstOrDefault<int>(@"
                INSERT INTO HistorialAcademico
                    (ConsecutivoEstudiante, ConsecutivoDocente, ConsecutivoMateria, Materia, Periodo, Nota, Observacion)
                OUTPUT INSERTED.ConsecutivoHistorial
                VALUES (@Estudiante, @Docente, @ConsecutivoMateria, @Materia, @Periodo, @Nota, @Observacion)",
                new
                {
                    Estudiante = model.ConsecutivoEstudiante,
                    Docente = docenteId,
                    ConsecutivoMateria = materia.ConsecutivoMateria,
                    Materia = materia.Nombre,
                    Periodo = periodo,
                    model.Nota,
                    Observacion = observacion
                });

            return Ok(new { Mensaje = "Registro académico agregado.", ConsecutivoHistorial = nuevoId });
        }

        // POST api/Historial/ImportarCsvAPI  (Docente sube un archivo CSV con calificaciones masivas)
        // Columnas esperadas: CorreoEstudiante,Materia,Periodo,Nota,Observacion
        // Acepta separador coma, punto y coma o tabulador (Excel en español usa ";").
        [Authorize(Roles = "Docente")]
        [HttpPost("ImportarCsvAPI")]
        public async Task<IActionResult> ImportarCsvAPI(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                return BadRequest("Debe adjuntar un archivo CSV.");
            }

            if (archivo.Length > 2 * 1024 * 1024)
            {
                return BadRequest("El archivo supera el máximo permitido de 2 MB.");
            }

            var resultado = new ImportarCalificacionesResultModel();
            using var context = Conexion;
            var docenteId = _utiles.ObtenerConsecutivoToken();

            // Solo se aceptan asignaturas ACTIVAS del propio docente.
            var materiasDocente = context.Query<MateriaBasica>(@"
                SELECT ConsecutivoMateria, Nombre, Seccion, ConsecutivoDocente, Activa
                FROM Materia WHERE ConsecutivoDocente = @Id AND Activa = 1",
                new { Id = docenteId }).ToList();

            using var lector = new StreamReader(archivo.OpenReadStream());
            using var csv = new CsvReader(lector, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                DetectDelimiter = true,
                DetectDelimiterValues = new[] { ",", ";", "\t" }
            });

            if (!await csv.ReadAsync() || !csv.ReadHeader() || csv.HeaderRecord == null)
            {
                return BadRequest("El archivo está vacío o no tiene encabezado.");
            }

            var faltantes = new[] { "CorreoEstudiante", "Materia", "Periodo", "Nota" }
                .Where(c => !csv.HeaderRecord.Any(h => string.Equals(h?.Trim(), c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (faltantes.Count > 0)
            {
                return BadRequest($"Faltan columnas en el encabezado: {string.Join(", ", faltantes)}.");
            }

            while (await csv.ReadAsync())
            {
                resultado.FilasProcesadas++;
                var fila = resultado.FilasProcesadas;

                try
                {
                    var correo = csv.GetField("CorreoEstudiante")?.Trim();
                    var nombreMateria = csv.GetField("Materia")?.Trim();
                    var periodo = csv.GetField("Periodo")?.Trim();
                    var notaTexto = csv.GetField("Nota")?.Trim();
                    var observacion = csv.GetField("Observacion")?.Trim();

                    if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(nombreMateria) || string.IsNullOrWhiteSpace(periodo))
                    {
                        resultado.Errores.Add($"Fila {fila}: correo, materia y periodo son obligatorios.");
                        continue;
                    }

                    if (periodo.Length > 50)
                    {
                        resultado.Errores.Add($"Fila {fila}: el periodo supera 50 caracteres.");
                        continue;
                    }

                    var estudiante = context.QueryFirstOrDefault<EstudianteCsv>(
                        "SELECT ConsecutivoUsuario AS Id, Seccion FROM Usuario WHERE CorreoElectronico = @Correo AND Rol = 'Estudiante' AND Activo = 1",
                        new { Correo = correo });

                    if (estudiante == null)
                    {
                        resultado.Errores.Add($"Fila {fila}: no existe un estudiante activo con el correo '{correo}'.");
                        continue;
                    }

                    // La asignatura se identifica por nombre + la sección del estudiante.
                    var materia = materiasDocente.FirstOrDefault(m =>
                        string.Equals(m.Nombre, nombreMateria, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(m.Seccion, estudiante.Seccion, StringComparison.OrdinalIgnoreCase));

                    if (materia == null)
                    {
                        resultado.Errores.Add($"Fila {fila}: usted no dicta '{nombreMateria}' en la sección de {correo}.");
                        continue;
                    }

                    decimal? nota = null;
                    if (!string.IsNullOrWhiteSpace(notaTexto))
                    {
                        if (!decimal.TryParse(notaTexto.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                        {
                            resultado.Errores.Add($"Fila {fila}: la nota '{notaTexto}' no es un número válido.");
                            continue;
                        }

                        if (n < 0 || n > 100)
                        {
                            resultado.Errores.Add($"Fila {fila}: la nota debe estar entre 0 y 100.");
                            continue;
                        }

                        nota = n;
                    }

                    if (!string.IsNullOrEmpty(observacion) && observacion.Length > 400)
                    {
                        resultado.Errores.Add($"Fila {fila}: la observación supera 400 caracteres.");
                        continue;
                    }

                    context.Execute(@"
                        INSERT INTO HistorialAcademico (ConsecutivoEstudiante, ConsecutivoDocente, ConsecutivoMateria, Materia, Periodo, Nota, Observacion)
                        VALUES (@Estudiante, @Docente, @ConsecutivoMateria, @Materia, @Periodo, @Nota, @Observacion)",
                        new
                        {
                            Estudiante = estudiante.Id,
                            Docente = docenteId,
                            materia.ConsecutivoMateria,
                            Materia = materia.Nombre,
                            Periodo = periodo,
                            Nota = nota,
                            Observacion = string.IsNullOrEmpty(observacion) ? null : observacion
                        });

                    resultado.FilasInsertadas++;
                }
                catch (Exception ex)
                {
                    resultado.Errores.Add($"Fila {fila}: {ex.Message}");
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

            if (!PuedeVerEstudiante(context, rol, idConsultado.Value)) return Forbid();

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
                        columna.Item().Text($"Sección: {estudiante.Seccion ?? "-"}");
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

        private class EstudianteCsv
        {
            public int Id { get; set; }
            public string? Seccion { get; set; }
        }
    }
}
