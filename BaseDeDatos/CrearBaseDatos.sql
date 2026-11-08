/* =====================================================================
   MiniCampus — Escuela Ricardo André Strauss
   Plataforma de Gestión Académica Integral — Proyecto TCU
   Script de creación de base de datos para SQL Server (v2)
   ===================================================================== */

CREATE DATABASE Escuela_BD;
GO

USE Escuela_BD;
GO

-- =========================================================
-- TABLA: Usuario (Director, Docente, Estudiante)
-- =========================================================
CREATE TABLE Usuario (
    ConsecutivoUsuario   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(150)   NOT NULL,
    CorreoElectronico    NVARCHAR(150)   NOT NULL UNIQUE,
    Contrasenna          NVARCHAR(256)   NOT NULL,       -- hash BCrypt
    Rol                  NVARCHAR(20)    NOT NULL
        CONSTRAINT CK_Usuario_Rol CHECK (Rol IN ('Director','Docente','Estudiante')),
    Seccion              NVARCHAR(80)    NULL,            -- grupo / grado (docente a cargo o estudiante matriculado)
    Activo               BIT             NOT NULL DEFAULT 1,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Materia (asignaturas / cursos que imparte cada docente)
-- =========================================================
CREATE TABLE Materia (
    ConsecutivoMateria   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre               NVARCHAR(100)   NOT NULL,
    Seccion              NVARCHAR(80)    NOT NULL,
    ConsecutivoDocente   INT             NOT NULL REFERENCES Usuario(ConsecutivoUsuario),
    Activa               BIT             NOT NULL DEFAULT 1,
    FechaCreacion        DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- TABLA: Horario (bloques de clase de cada materia)
-- =========================================================
CREATE TABLE Horario (
    ConsecutivoHorario   INT IDENTITY(1,1) PRIMARY KEY,
    ConsecutivoMateria   INT             NOT NULL REFERENCES Materia(ConsecutivoMateria),
    DiaSemana            NVARCHAR(15)    NOT NULL
        CONSTRAINT CK_Horario_Dia CHECK (DiaSemana IN ('Lunes','Martes','Miércoles','Jueves','Viernes')),
    HoraInicio           VARCHAR(5)      NOT NULL,        -- formato "HH:mm"
    HoraFin              VARCHAR(5)      NOT NULL,
    Aula                 NVARCHAR(50)    NULL
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
    Seccion              NVARCHAR(80)    NULL,            -- NULL = para toda la institución
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
    Periodo                NVARCHAR(50)  NOT NULL,
    Nota                    DECIMAL(5,2) NULL
        CONSTRAINT CK_Historial_Nota CHECK (Nota IS NULL OR (Nota >= 0 AND Nota <= 100)),
    Observacion              NVARCHAR(400) NULL,
    FechaCreacion             DATETIME    NOT NULL DEFAULT GETDATE()
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
    Observacion            NVARCHAR(300) NULL,
    FechaCreacion          DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Asistencia UNIQUE (ConsecutivoEstudiante, ConsecutivoMateria, Fecha)
);
GO

-- =========================================================
-- TABLA: RegistroError (bitácora de errores no controlados de la API)
-- =========================================================
CREATE TABLE RegistroError (
    ConsecutivoError     INT IDENTITY(1,1) PRIMARY KEY,
    Ruta                 NVARCHAR(300)   NULL,
    Mensaje               NVARCHAR(MAX)  NULL,
    Detalle                NVARCHAR(MAX) NULL,
    FechaCreacion           DATETIME     NOT NULL DEFAULT GETDATE()
);
GO

-- =========================================================
-- DATOS DE EJEMPLO
-- Contraseñas de referencia (texto plano antes del hash):
--   director123     -> Director
--   docente123      -> Docente
--   estudiante123   -> Estudiante 1 y 2
-- Los hashes fueron generados con BCrypt (compatibles con BCrypt.Net-Next).
-- =========================================================
INSERT INTO Usuario (Nombre, CorreoElectronico, Contrasenna, Rol, Seccion) VALUES
('María Elena Vargas Solano', 'director@escuela.cr',
 '$2b$10$RyDwXmfc0fxJTtsvJJ5ZCOwLF7zWHRI7o.PUpIvtSgM.Rl0doZMhC', 'Director', NULL),
('Christopher Cáseres Madrigal', 'docente@escuela.cr',
 '$2b$10$u0pzKia9yUU6/7PxzK6C2OZ6RGuWnOck0sbGH3xzys/sPPgtHueUO', 'Docente', 'Sexto Grado'),
('Ana Rodríguez Solís', 'estudiante1@escuela.cr',
 '$2b$10$smxTOb8MsNG3H.gQlotsE.vZpQXRnQUSKYHZ2yhqyQP5lt0vECoMq', 'Estudiante', 'Sexto Grado'),
('Luis Fernández Mora', 'estudiante2@escuela.cr',
 '$2b$10$smxTOb8MsNG3H.gQlotsE.vZpQXRnQUSKYHZ2yhqyQP5lt0vECoMq', 'Estudiante', 'Sexto Grado');
GO

INSERT INTO Materia (Nombre, Seccion, ConsecutivoDocente) VALUES
('Matemática', 'Sexto Grado', 2),
('Español', 'Sexto Grado', 2),
('Ciencias', 'Sexto Grado', 2),
('Estudios Sociales', 'Sexto Grado', 2);
GO

INSERT INTO Horario (ConsecutivoMateria, DiaSemana, HoraInicio, HoraFin, Aula) VALUES
(1, 'Lunes', '07:00', '08:00', 'Aula 3'),
(2, 'Lunes', '08:00', '09:00', 'Aula 2'),
(3, 'Lunes', '09:20', '10:20', 'Laboratorio'),
(4, 'Lunes', '10:40', '11:40', 'Aula 1'),
(1, 'Martes', '07:00', '08:00', 'Aula 3'),
(2, 'Miércoles', '08:00', '09:00', 'Aula 2');
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
(4, 2, 1, 'Matemática', 'I Trimestre 2026', 78, 'Necesita repaso de fracciones.');
GO

INSERT INTO Asistencia (ConsecutivoEstudiante, ConsecutivoMateria, ConsecutivoDocente, Fecha, Estado) VALUES
(3, 1, 2, '2026-05-04', 'Presente'),
(3, 1, 2, '2026-05-05', 'Presente'),
(3, 1, 2, '2026-05-06', 'Tardía'),
(4, 1, 2, '2026-05-04', 'Ausente'),
(4, 1, 2, '2026-05-05', 'Presente');
GO

PRINT 'Base de datos Escuela_BD creada e inicializada correctamente (v2 - 3 roles).';
