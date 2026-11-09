/* =====================================================================
   MiniCampus — Migración de la base de datos v2 -> v3
   Úsese SOLO si ya tiene la base Escuela_BD creada con la versión anterior
   y quiere conservar sus datos. Es seguro ejecutarlo más de una vez.

   Qué hace:
     1. Crea el catálogo Seccion a partir de las secciones que ya existen.
     2. Crea el catálogo Aula a partir de las aulas escritas en Horario.
     3. Convierte Horario.Aula (texto) en Horario.ConsecutivoAula (FK).
     4. Agrega a Usuario las columnas Pendiente y ClaveSolicitada.
     5. Deja de usar Seccion en los docentes (se deduce de sus asignaturas).
   ===================================================================== */
USE Escuela_BD;
GO

-- 1) Usuario: nuevas columnas ------------------------------------------------
IF COL_LENGTH('Usuario', 'Pendiente') IS NULL
    ALTER TABLE Usuario ADD Pendiente BIT NOT NULL CONSTRAINT DF_Usuario_Pendiente DEFAULT 0;
GO
IF COL_LENGTH('Usuario', 'ClaveSolicitada') IS NULL
    ALTER TABLE Usuario ADD ClaveSolicitada DATETIME NULL;
GO

-- 2) Catálogos ---------------------------------------------------------------
IF OBJECT_ID('Seccion', 'U') IS NULL
    CREATE TABLE Seccion (
        ConsecutivoSeccion   INT IDENTITY(1,1) PRIMARY KEY,
        Nombre               NVARCHAR(80)    NOT NULL UNIQUE,
        Activa               BIT             NOT NULL DEFAULT 1,
        FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
    );
GO
IF OBJECT_ID('Aula', 'U') IS NULL
    CREATE TABLE Aula (
        ConsecutivoAula      INT IDENTITY(1,1) PRIMARY KEY,
        Nombre               NVARCHAR(50)    NOT NULL UNIQUE,
        Activa               BIT             NOT NULL DEFAULT 1,
        FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
    );
GO

-- 3) Limpieza de textos de sección (espacios, vacíos) -------------------------
UPDATE Usuario SET Seccion = NULLIF(LTRIM(RTRIM(Seccion)), '') WHERE Seccion IS NOT NULL;
UPDATE Materia SET Seccion = LTRIM(RTRIM(Seccion)) WHERE Seccion IS NOT NULL;
UPDATE Aviso   SET Seccion = NULLIF(LTRIM(RTRIM(Seccion)), '') WHERE Seccion IS NOT NULL;
GO

-- Los docentes ya no tienen sección propia: se deduce de sus asignaturas.
UPDATE Usuario SET Seccion = NULL WHERE Rol <> 'Estudiante' AND Seccion IS NOT NULL;
GO

-- 4) Poblar Seccion con todo lo que ya existía --------------------------------
INSERT INTO Seccion (Nombre)
SELECT x.Seccion
FROM (
    SELECT Seccion FROM Usuario WHERE Seccion IS NOT NULL
    UNION
    SELECT Seccion FROM Materia WHERE Seccion IS NOT NULL
    UNION
    SELECT Seccion FROM Aviso WHERE Seccion IS NOT NULL
) x
WHERE NOT EXISTS (SELECT 1 FROM Seccion s WHERE s.Nombre = x.Seccion);
GO

-- 5) Llaves foráneas hacia Seccion -------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuario_Seccion')
    ALTER TABLE Usuario ADD CONSTRAINT FK_Usuario_Seccion FOREIGN KEY (Seccion) REFERENCES Seccion(Nombre);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Materia_Seccion')
    ALTER TABLE Materia ADD CONSTRAINT FK_Materia_Seccion FOREIGN KEY (Seccion) REFERENCES Seccion(Nombre);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Aviso_Seccion')
    ALTER TABLE Aviso ADD CONSTRAINT FK_Aviso_Seccion FOREIGN KEY (Seccion) REFERENCES Seccion(Nombre);
GO

-- 6) Horario: de texto libre a catálogo de aulas ------------------------------
IF COL_LENGTH('Horario', 'ConsecutivoAula') IS NULL
    ALTER TABLE Horario ADD ConsecutivoAula INT NULL;
GO

IF COL_LENGTH('Horario', 'Aula') IS NOT NULL
BEGIN
    EXEC('
        INSERT INTO Aula (Nombre)
        SELECT DISTINCT LTRIM(RTRIM(h.Aula))
        FROM Horario h
        WHERE h.Aula IS NOT NULL AND LTRIM(RTRIM(h.Aula)) <> ''''
          AND NOT EXISTS (SELECT 1 FROM Aula a WHERE a.Nombre = LTRIM(RTRIM(h.Aula)));
    ');
    EXEC('
        UPDATE h SET ConsecutivoAula = a.ConsecutivoAula
        FROM Horario h
        INNER JOIN Aula a ON a.Nombre = LTRIM(RTRIM(h.Aula));
    ');
    EXEC('ALTER TABLE Horario DROP COLUMN Aula;');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Horario_Aula')
    ALTER TABLE Horario ADD CONSTRAINT FK_Horario_Aula FOREIGN KEY (ConsecutivoAula) REFERENCES Aula(ConsecutivoAula);
GO

-- Aulas básicas por si el catálogo quedó vacío o incompleto
INSERT INTO Aula (Nombre)
SELECT v.Nombre FROM (VALUES ('Aula 1'), ('Aula 2'), ('Aula 3'), ('Laboratorio'), ('Sala de Cómputo'), ('Biblioteca')) v(Nombre)
WHERE NOT EXISTS (SELECT 1 FROM Aula a WHERE a.Nombre = v.Nombre);
GO

PRINT 'Migración a la v3 completada correctamente.';
