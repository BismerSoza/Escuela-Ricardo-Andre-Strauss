/* =====================================================================
   MiniCampus — Escuela Ricardo André Strauss
   Plataforma de Gestión Académica Integral — Proyecto TCU
   Script de creación de base de datos para SQL Server (v3)

   Novedades de la v3:
     - Tabla Seccion  : catálogo oficial de grados/secciones (ya no es texto libre).
     - Tabla Aula     : catálogo de aulas, usado por el calendario de horarios.
     - Horario        : ahora referencia un Aula (FK) para poder detectar choques.
     - Usuario        : columnas Pendiente (cuenta de docente por aprobar) y
                        ClaveSolicitada (el usuario pidió restablecer su clave).

   IMPORTANTE: este script CREA la base desde cero. Si ya tiene una base
   Escuela_BD con datos de la v2, NO lo ejecute: use MigracionV3.sql.
   ===================================================================== */

CREATE DATABASE Escuela_BD;
GO

USE Escuela_BD;
GO

-- =========================================================
-- TABLA: Seccion (catálogo de grados / secciones)
-- Los estudiantes se matriculan en una sección; las asignaturas
-- se dictan en una sección; los avisos pueden dirigirse a una sección.
-- =========================================================
CREATE TABLE Seccion (
    ConsecutivoSeccion   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(80)    NOT NULL UNIQUE,
    Activa               BIT             NOT NULL DEFAULT 1,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Aula (catálogo de aulas / espacios físicos)
-- =========================================================
CREATE TABLE Aula (
    ConsecutivoAula      INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(50)    NOT NULL UNIQUE,
    Activa               BIT             NOT NULL DEFAULT 1,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Usuario (Director, Docente, Estudiante)
--   Seccion        : solo aplica a Estudiante. La(s) sección(es) de un docente
--                    se deducen de las asignaturas que tiene asignadas.
--   Pendiente      : 1 = cuenta de docente auto-registrada que espera aprobación.
--   ClaveSolicitada: fecha en que el usuario pidió restablecer su contraseña.
-- =========================================================
CREATE TABLE Usuario (
    ConsecutivoUsuario   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(150)   NOT NULL,
    CorreoElectronico    NVARCHAR(150)   NOT NULL UNIQUE,
    Contrasenna          NVARCHAR(256)   NOT NULL,       -- hash BCrypt
    Rol                  NVARCHAR(20)    NOT NULL
        CONSTRAINT CK_Usuario_Rol CHECK (Rol IN ('Director','Docente','Estudiante')),
    Seccion              NVARCHAR(80)    NULL
        CONSTRAINT FK_Usuario_Seccion REFERENCES Seccion(Nombre),
    Activo               BIT             NOT NULL DEFAULT 1,
    Pendiente            BIT             NOT NULL DEFAULT 0,
    ClaveSolicitada      DATETIME        NULL,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Materia (asignaturas / cursos que imparte cada docente)
-- =========================================================
CREATE TABLE Materia (
    ConsecutivoMateria   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(100)   NOT NULL,
    Seccion              NVARCHAR(80)    NOT NULL
        CONSTRAINT FK_Materia_Seccion REFERENCES Seccion(Nombre),
    ConsecutivoDocente   INT             NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    Activa               BIT             NOT NULL DEFAULT 1,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Horario (bloques de clase de cada materia)
--   La API garantiza que dos bloques no se solapen en la misma aula,
--   con el mismo docente ni en la misma sección (ver HorarioController).
-- =========================================================
CREATE TABLE Horario (
    ConsecutivoHorario   INT IDENTITY(1,1) PRIMARY KEY,
    ConsecutivoMateria   INT             NOT NULL REFERENCES Materia(ConsecutivoMateria),
    DiaSemana            NVARCHAR(15)    NOT NULL
        CONSTRAINT CK_Horario_Dia CHECK (DiaSemana IN ('Lunes','Martes','Miércoles','Jueves','Viernes')),
    HoraInicio           VARCHAR(5)      NOT NULL,        -- formato "HH:mm"
    HoraFin              VARCHAR(5)      NOT NULL,
    ConsecutivoAula      INT             NULL REFERENCES Aula(ConsecutivoAula),
    CONSTRAINT CK_Horario_Horas CHECK (HoraInicio < HoraFin)
);
GO

-- =========================================================
-- TABLA: Aviso (comunicados institucionales)
-- =========================================================
CREATE TABLE Aviso (
    ConsecutivoAviso     INT IDENTITY(1,1) PRIMARY KEY,
    Titulo               NVARCHAR(200)   NOT NULL,
    Contenido            NVARCHAR(MAX)   NOT NULL,
    Prioridad            NVARCHAR(15)    NOT NULL DEFAULT 'General'
        CONSTRAINT CK_Aviso_Prioridad CHECK (Prioridad IN ('Importante','Información','General')),
    ConsecutivoAutor     INT             NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    Seccion              NVARCHAR(80)    NULL
        CONSTRAINT FK_Aviso_Seccion REFERENCES Seccion(Nombre),   -- NULL = para toda la institución
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Mensaje (mensajería interna)
-- =========================================================
CREATE TABLE Mensaje (
    ConsecutivoMensaje   INT IDENTITY(1,1) PRIMARY KEY,
    ConsecutivoEmisor    INT             NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    ConsecutivoReceptor  INT             NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    Contenido            NVARCHAR(MAX)   NOT NULL,
    Leido                BIT             NOT NULL DEFAULT 0,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: HistorialAcademico (calificaciones por materia/periodo)
-- =========================================================
CREATE TABLE HistorialAcademico (
    ConsecutivoHistorial  INT IDENTITY(1,1) PRIMARY KEY,
    ConsecutivoEstudiante INT            NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    ConsecutivoDocente    INT            NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    ConsecutivoMateria    INT            NULL REFERENCES Materia(ConsecutivoMateria),
    Materia               NVARCHAR(100) NOT NULL,
    Periodo               NVARCHAR(50)  NOT NULL,
    Nota                  DECIMAL(5,2) NULL
        CONSTRAINT CK_Historial_Nota CHECK (Nota IS NULL OR (Nota >= 0 AND Nota <= 100)),
    Observacion           NVARCHAR(400) NULL,
    FechaCreacion         DATETIME    NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Asistencia (control diario por materia)
-- =========================================================
CREATE TABLE Asistencia (
    ConsecutivoAsistencia INT IDENTITY(1,1) PRIMARY KEY,
    ConsecutivoEstudiante INT            NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    ConsecutivoMateria    INT            NOT NULL REFERENCES Materia(ConsecutivoMateria),
    ConsecutivoDocente    INT            NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    Fecha                 DATE           NOT NULL,
    Estado                NVARCHAR(15)   NOT NULL
        CONSTRAINT CK_Asistencia_Estado CHECK (Estado IN ('Presente','Ausente','Tardía','Justificada')),
    Observacion           NVARCHAR(300) NULL,
    FechaCreacion         DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Asistencia UNIQUE (ConsecutivoEstudiante, ConsecutivoMateria, Fecha)
);
GO

-- =========================================================
-- TABLA: RegistroError (bitácora de errores no controlados de la API)
-- =========================================================
CREATE TABLE RegistroError (
    ConsecutivoError     INT IDENTITY(1,1) PRIMARY KEY,
    Ruta                 NVARCHAR(300)   NULL,
    Mensaje              NVARCHAR(MAX)   NULL,
    Detalle              NVARCHAR(MAX)   NULL,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- DATOS DE EJEMPLO
-- Contraseñas de referencia (texto plano antes del hash):
--   director123     -> Director
--   docente123      -> Docentes
--   estudiante123   -> Estudiantes
-- Los hashes fueron generados con BCrypt (compatibles con BCrypt.Net-Next).
-- =========================================================
INSERT INTO Seccion (Nombre) VALUES
('Cuarto Grado'),
('Quinto Grado'),
('Sexto Grado');
GO

INSERT INTO Aula (Nombre) VALUES
('Aula 1'),            -- 1
('Aula 2'),            -- 2
('Aula 3'),            -- 3
('Laboratorio'),       -- 4
('Sala de Cómputo'),   -- 5
('Biblioteca');        -- 6
GO

INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion) VALUES
('María Elena Vargas Solano', 'director@escuela.cr',
 '$2b$10$RyDwXmfc0fxJTtsvJJ5ZCOwLF7zWHRI7o.PUpIvtSgM.Rl0doZMhC', 'Director', NULL),          -- 1
('Christopher Cáseres Madrigal', 'docente@escuela.cr',
 '$2b$10$u0pzKia9yUU6/7PxzK6C2OZ6RGuWnOck0sbGH3xzys/sPPgtHueUO', 'Docente', NULL),          -- 2
('Ana Rodríguez Solís', 'estudiante1@escuela.cr',
 '$2b$10$smxTOb8MsNG3H.gQlotsE.vZpQXRnQUSKYHZ2yhqyQP5lt0vECoMq', 'Estudiante', 'Sexto Grado'), -- 3
('Luis Fernández Mora', 'estudiante2@escuela.cr',
 '$2b$10$smxTOb8MsNG3H.gQlotsE.vZpQXRnQUSKYHZ2yhqyQP5lt0vECoMq', 'Estudiante', 'Sexto Grado'), -- 4
('Laura Jiménez Soto', 'docente2@escuela.cr',
 '$2b$10$u0pzKia9yUU6/7PxzK6C2OZ6RGuWnOck0sbGH3xzys/sPPgtHueUO', 'Docente', NULL),          -- 5
('Mateo Chaves Quesada', 'estudiante3@escuela.cr',
 '$2b$10$smxTOb8MsNG3H.gQlotsE.vZpQXRnQUSKYHZ2yhqyQP5lt0vECoMq', 'Estudiante', 'Quinto Grado'); -- 6
GO

INSERT INTO Materia (Nombre, Seccion, ConsecutivoDocente) VALUES
('Matemática',        'Sexto Grado', 2),   -- 1
('Español',           'Sexto Grado', 2),   -- 2
('Ciencias',          'Sexto Grado', 2),   -- 3
('Estudios Sociales', 'Sexto Grado', 2),   -- 4
('Matemática',        'Quinto Grado', 5),  -- 5
('Español',           'Quinto Grado', 5);  -- 6
GO

INSERT INTO Horario (ConsecutivoMateria, DiaSemana, HoraInicio, HoraFin, ConsecutivoAula) VALUES
(1, 'Lunes',     '07:00', '08:00', 3),   -- Matemática 6°  - Aula 3
(2, 'Lunes',     '08:00', '09:00', 2),   -- Español 6°     - Aula 2
(3, 'Lunes',     '09:20', '10:20', 4),   -- Ciencias 6°    - Laboratorio
(4, 'Lunes',     '10:40', '11:40', 1),   -- Est. Sociales  - Aula 1
(1, 'Martes',    '07:00', '08:00', 3),
(2, 'Miércoles', '08:00', '09:00', 2),
(5, 'Lunes',     '07:00', '08:00', 1),   -- Matemática 5°  - Aula 1
(6, 'Lunes',     '08:00', '09:00', 1),   -- Español 5°     - Aula 1
(5, 'Martes',    '08:00', '09:00', 2);
GO

INSERT INTO Aviso (Titulo, Contenido, Prioridad, ConsecutivoAutor, Seccion) VALUES
('Reunión de Padres de Familia', 'Se les informa que la reunión será el día 15 de mayo a las 6:00 PM.', 'Importante', 1, NULL),
('Feria Científica 2026', 'Participa en la feria científica de la escuela. Inscripciones abiertas.', 'Información', 1, NULL),
('Bienvenida al curso lectivo', 'Se les informa a los estudiantes de Sexto Grado que las clases inician el próximo lunes.', 'General', 2, 'Sexto Grado');
GO

INSERT INTO HistorialAcademico (ConsecutivoEstudiante, ConsecutivoDocente, ConsecutivoMateria, Materia, Periodo, Nota, Observacion) VALUES
(3, 2, 1, 'Matemática', 'I Trimestre 2026', 92, 'Buen desempeño en clase.'),
(3, 2, 2, 'Español', 'I Trimestre 2026', 85, 'Debe reforzar ortografía.'),
(3, 2, 3, 'Ciencias', 'I Trimestre 2026', 95, 'Excelente participación.'),
(3, 2, 4, 'Estudios Sociales', 'I Trimestre 2026', 88, NULL),
(4, 2, 1, 'Matemática', 'I Trimestre 2026', 78, 'Necesita repaso de fracciones.'),
(6, 5, 5, 'Matemática', 'I Trimestre 2026', 90, 'Muy buen razonamiento lógico.');
GO

INSERT INTO Asistencia (ConsecutivoEstudiante, ConsecutivoMateria, ConsecutivoDocente, Fecha, Estado) VALUES
(3, 1, 2, '2026-05-04', 'Presente'),
(3, 1, 2, '2026-05-05', 'Presente'),
(3, 1, 2, '2026-05-06', 'Tardía'),
(4, 1, 2, '2026-05-04', 'Ausente'),
(4, 1, 2, '2026-05-05', 'Presente');
GO

PRINT 'Base de datos Escuela_BD creada e inicializada correctamente (v3).';
