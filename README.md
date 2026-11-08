# MiniCampus — Escuela Ricardo André Strauss
### Plataforma de Gestión Académica Integral — Proyecto TCU
### Arquitectura: ASP.NET Core (API + MVC) + SQL Server — Visual Studio

Sistema completo de 3 roles (**Director, Docente, Estudiante**) con permisos
diferenciados, pensado para un entorno real de TCU: validaciones, manejo
global de errores (sin pantallas rotas), exportación de reportes en PDF e
importación masiva de calificaciones por CSV.

## Estructura de la solución

```
Escuela_SM/
├── Escuela_SM.sln                  <- abrir este archivo en Visual Studio
├── BaseDeDatos/
│   └── CrearBaseDatos.sql           <- script completo para SQL Server
├── Escuela_API/                     <- Web API (ASP.NET Core)
│   ├── Controllers/
│   │   ├── UsuarioController.cs         (login, registro, perfil, recuperar clave)
│   │   ├── AdminController.cs           (Director: usuarios, secciones, bitácora de errores)
│   │   ├── DashboardController.cs       (resumen/estadísticas por rol)
│   │   ├── MateriaController.cs         (asignaturas)
│   │   ├── HorarioController.cs
│   │   ├── AsistenciaController.cs
│   │   ├── AvisoController.cs           (con prioridad: Importante/Información/General)
│   │   ├── MensajeController.cs
│   │   ├── HistorialController.cs       (calificaciones + Exportar PDF + Importar CSV)
│   │   └── EstudianteController.cs
│   ├── Hubs/ChatHub.cs               <- SignalR (mensajería en tiempo real)
│   ├── Middleware/ManejadorErroresMiddleware.cs  <- captura TODO error no controlado
│   ├── Models/                        <- Request/Response models
│   ├── Services/                      <- JWT (UtilesService)
│   ├── Program.cs
│   └── appsettings.json               <- cadena de conexión SQL Server
└── Escuela_WEB/                      <- Sitio web MVC (Razor) con sidebar
    ├── Controllers/                   (uno por módulo, reflejan los de la API)
    ├── Views/
    │   ├── Home/                       (3 dashboards: Estudiante/Docente/Director)
    │   ├── Admin/                      (solo Director)
    │   ├── Materia/ Horario/ Asistencia/ Historial/ Aviso/ Mensaje/ Usuario/
    │   └── Shared/_Layout.cshtml       (sidebar + topbar estilo "MiniCampus")
    ├── Filters/
    │   ├── SessionAuthorizeAttribute.cs   (exige sesión iniciada)
    │   └── ManejadorExcepcionesFilter.cs  (captura errores → vista amigable)
    ├── Services/ApiClienteService.cs   (HttpClient hacia la API + descarga PDF + subida CSV)
    ├── Program.cs
    └── appsettings.json                (URL base de la API)
```

## Los 3 roles y sus permisos

| Módulo | Estudiante | Docente | Director |
|---|---|---|---|
| Dashboard | Su propio resumen académico | Resumen de sus grupos | Resumen institucional completo |
| Asignaturas | Ver las de su sección | Crear/editar las propias | Ver y crear todas, asignar a cualquier docente |
| Calificaciones | Ver las propias + exportar PDF | Registrar, importar CSV, exportar PDF | Ver y exportar (no registra notas) |
| Asistencia | Ver la propia (% y detalle) | Marcar asistencia diaria | Solo consulta (de lectura) |
| Horario | Ver el de su sección | Gestionar el de sus materias | Gestionar cualquiera |
| Avisos | Ver los de su sección + generales | Publicar/eliminar los propios | Publicar/eliminar cualquiera, ve todos |
| Mensajería | Chat con docentes y Director | Chat con sus estudiantes y Director | Chat con cualquier docente/estudiante |
| Administración | — | — | Usuarios (crear/activar/desactivar/cambiar rol), Secciones, Bitácora de errores |

> **Nota de diseño:** el Director tiene visibilidad total de la institución
> (usuarios, calificaciones, asistencia, avisos) pero **no registra notas ni
> marca asistencia directamente** — esa es responsabilidad del Docente de cada
> materia. El Director sí puede publicar avisos institucionales y gestionar
> cuentas de usuario. Esta separación se explica en el propio código
> (`AsistenciaController`, `HistorialController`) por si tu profesor pregunta
> el porqué de la decisión.

## Manejo de errores (para que nunca "saque" al usuario)

- **API**: `Middleware/ManejadorErroresMiddleware.cs` envuelve *toda* la
  aplicación. Cualquier excepción no capturada (falla de SQL, referencia
  nula, timeout, etc.) se registra en la tabla `RegistroError` y responde con
  un JSON `{ exito: false, mensaje: "..." }` con el código HTTP adecuado, en
  vez de mostrar el stack trace o tumbar el servidor.
- **WEB**: `Filters/ManejadorExcepcionesFilter.cs` captura cualquier excepción
  dentro de un controlador y redirige a una vista de error amigable
  (`Views/Shared/Error.cshtml`) en vez de la pantalla amarilla de ASP.NET.
- **Frontend (JS)**: `wwwroot/js/site.js` incluye `fetchSeguro()`, un
  envoltorio de `fetch` que muestra una alerta Bootstrap flotante cuando una
  llamada AJAX (como el chat) falla, en vez de dejar la pantalla "muerta".
- El Director puede revisar la bitácora completa de errores en
  **Administración → Errores del sistema**.

## Exportar PDF / Importar CSV

- **Exportar PDF**: botón "Exportar PDF" en Calificaciones (estudiante,
  docente o director). Se genera con **QuestPDF** en la API
  (`HistorialController.ExportarPdfAPI`) con el logo institucional, tabla de
  notas y promedio general.
- **Importar CSV**: el Docente puede importar calificaciones masivas desde
  **Historial académico → Importar CSV**. El archivo debe tener las columnas
  `CorreoEstudiante,Materia,Periodo,Nota,Observacion`. Se procesa con
  **CsvHelper** y reporta cuántas filas se insertaron y cuáles fallaron (y
  por qué), sin detener el proceso completo por un error puntual.

## Cómo ponerlo a andar en Visual Studio

### 1. Crear la base de datos
Ejecutá el script `BaseDeDatos/CrearBaseDatos.sql` completo en SQL Server
Management Studio (o Azure Data Studio). Esto crea `Escuela_BD`, todas las
tablas y los usuarios de prueba.

### 2. Configurar la cadena de conexión
En `Escuela_API/appsettings.json`, ajustá `ConnectionStrings:DefaultConnection`:
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=Escuela_BD;Integrated Security=True;TrustServerCertificate=True;"
```

### 3. Abrir la solución
Abrí `Escuela_SM.sln` con **Visual Studio 2022** (17.8+). Al compilar,
restaura automáticamente todos los paquetes NuGet (Dapper, JWT, SignalR,
BCrypt, **QuestPDF**, **CsvHelper**, etc.).

### 4. Ejecutar ambos proyectos a la vez
1. Click derecho sobre la **solución** → *Configurar proyectos de inicio…*
2. Elegí **"Varios proyectos de inicio"**, poné **Iniciar** en `Escuela_API`
   y en `Escuela_WEB`.
3. Presioná **F5**. Se abren dos pestañas:
   - `Escuela_API` → `https://localhost:7100/swagger`
   - `Escuela_WEB` → `https://localhost:7200/`

Si el puerto de la API cambia, actualizá `Escuela_WEB/appsettings.Development.json` → `Api:BaseUrl`.

## Cuentas de prueba (incluidas en el script SQL)

| Rol        | Correo                    | Clave           |
|------------|----------------------------|------------------|
| Director   | director@escuela.cr       | director123      |
| Docente    | docente@escuela.cr        | docente123       |
| Estudiante | estudiante1@escuela.cr    | estudiante123    |
| Estudiante | estudiante2@escuela.cr    | estudiante123    |

## Notas técnicas para el informe del TCU

- Seguridad: JWT entre WEB y API, contraseñas con **BCrypt** (nunca en texto
  plano), rutas de API protegidas con `[Authorize(Roles = "...")]` según cada
  caso de uso.
- El chat usa **SignalR** (WebSockets) para mensajería en tiempo real.
- La capa **WEB nunca habla directamente con la base de datos**: siempre pasa
  por la **API**, y la API nunca sirve HTML: solo JSON/PDF.
- El middleware de errores también sirve como bitácora de auditoría técnica
  (tabla `RegistroError`), útil para justificar mantenibilidad en la defensa
  del TCU.
