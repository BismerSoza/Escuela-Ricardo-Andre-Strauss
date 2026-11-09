using Dapper;
using System.Data;

namespace Escuela_API.Services
{
    // Reglas de visibilidad compartidas por todos los controladores.
    //
    // Idea central: un DOCENTE no tiene "una sección" propia. Las secciones que
    // le corresponden son aquellas donde tiene al menos una asignatura ACTIVA
    // (tabla Materia). Así, cuando la dirección le asigna una asignatura en
    // "Quinto Grado", el docente automáticamente ve a los estudiantes, avisos y
    // asistencia de Quinto Grado, sin tener que escribir ninguna sección a mano.
    public static class AccesoHelper
    {
        public static List<string> SeccionesDelDocente(IDbConnection context, int docenteId)
        {
            return context.Query<string>(@"
                SELECT DISTINCT Seccion FROM Materia
                WHERE ConsecutivoDocente = @Id AND Activa = 1
                ORDER BY Seccion", new { Id = docenteId }).ToList();
        }

        public static bool DocenteDictaSeccion(IDbConnection context, int docenteId, string? seccion)
        {
            if (string.IsNullOrWhiteSpace(seccion)) return false;

            return context.QueryFirstOrDefault<int>(@"
                SELECT COUNT(*) FROM Materia
                WHERE ConsecutivoDocente = @Id AND Activa = 1 AND Seccion = @Seccion",
                new { Id = docenteId, Seccion = seccion }) > 0;
        }

        // ¿El estudiante pertenece a alguna sección donde el docente dicta clases?
        public static bool DocenteTieneEstudiante(IDbConnection context, int docenteId, int estudianteId)
        {
            return context.QueryFirstOrDefault<int>(@"
                SELECT COUNT(*) FROM Usuario u
                INNER JOIN Materia m ON m.Seccion = u.Seccion
                WHERE u.ConsecutivoUsuario = @Estudiante AND u.Rol = 'Estudiante'
                  AND m.ConsecutivoDocente = @Docente AND m.Activa = 1",
                new { Estudiante = estudianteId, Docente = docenteId }) > 0;
        }

        // Valida que el nombre corresponda a una sección existente. Devuelve el nombre
        // tal como está guardado en el catálogo (respeta mayúsculas/acentos oficiales).
        public static string? NombreSeccionValido(IDbConnection context, string? seccion, bool soloActivas = true)
        {
            if (string.IsNullOrWhiteSpace(seccion)) return null;

            return context.QueryFirstOrDefault<string?>(@"
                SELECT Nombre FROM Seccion
                WHERE Nombre = @Nombre AND (@SoloActivas = 0 OR Activa = 1)",
                new { Nombre = seccion.Trim(), SoloActivas = soloActivas ? 1 : 0 });
        }
    }
}
