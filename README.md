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
│   ├── CrearBaseDatos.sql           <- crea la base desde cero (v3) con datos de ejemplo
│   └── MigracionV3.sql              <- actualiza una base v2 existente SIN perder datos
├── Escuela_API/                     <- Web API (ASP.NET Core)
│   ├── Controllers/
│   │   ├── UsuarioController.cs         (login, registro, perfil, solicitud de nueva clave)
│   │   ├── AdminController.cs           (Director: usuarios, secciones, aulas, bitácora de errores)
│   │   ├── SeccionController.cs         (catálogo de secciones para listas desplegables)
│   │   ├── DashboardController.cs       (resumen/estadísticas por rol)
│   │   ├── MateriaController.cs         (asignaturas)
│   │   ├── HorarioController.cs         (calendario + detección de choques)
│   │   ├── AsistenciaController.cs
│   │   ├── AvisoController.cs           (con prioridad: Importante/Información/General)
│   │   ├── MensajeController.cs
│   │   ├── HistorialController.cs       (calificaciones + Exportar PDF + Importar CSV)
│   │   └── EstudianteController.cs
│   ├── Hubs/ChatHub.cs               <- SignalR (mensajería en tiempo real)
│   ├── Middleware/ManejadorErroresMiddleware.cs  <- captura TODO error no controlado
│   ├── Models/                        <- Request/Response models
│   ├── Services/                      <- JWT (UtilesService) + AccesoHelper (qué ve cada docente)
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
| Asignaturas | Ver las de su sección | Crear las propias, desactivar/reactivar | Ver y crear todas, asignar a cualquier docente |
| Calificaciones | Ver las propias + exportar PDF | Registrar, importar CSV, exportar PDF | Ver y exportar (no registra notas) |
| Asistencia | Ver la propia (% y detalle) | Marcar asistencia diaria | Solo consulta (de lectura) |
| Horario | Ver el de su sección | Ver el propio y consultar cualquier aula/sección; gestionar el de sus materias | Gestionar cualquiera |
| Avisos | Ver los de su sección + generales | Publicar/eliminar los propios, solo a secciones donde dicta clases | Publicar/eliminar cualquiera (incluye «toda la institución»), ve todos |
| Mensajería | Chat con docentes y Director | Chat con sus estudiantes y Director | Chat con cualquier docente/estudiante |
| Administración | — | — | Usuarios (crear, aprobar, activar/desactivar, cambiar rol, restablecer clave), Secciones, Aulas, Bitácora de errores |

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

### 1. Crear (o actualizar) la base de datos
- **Instalación nueva:** ejecute `BaseDeDatos/CrearBaseDatos.sql` completo en SQL Server
  Management Studio. Crea `Escuela_BD`, todas las tablas y los datos de ejemplo.
- **Ya tiene la base de la versión anterior:** NO ejecute `CrearBaseDatos.sql` (fallaría
  porque la base ya existe). Ejecute `BaseDeDatos/MigracionV3.sql`:
  agrega las tablas/columnas nuevas y convierte sus datos (secciones y aulas escritas
  a mano pasan a los catálogos). Se puede ejecutar más de una vez sin riesgo.

### 2. Configurar la cadena de conexión
En `Escuela_API/appsettings.json`, ajuste `ConnectionStrings:DefaultConnection`:
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=Escuela_BD;Integrated Security=True;TrustServerCertificate=True;"
```

### 3. Abrir la solución
Abra `Escuela_SM.sln` con **Visual Studio 2022** (17.8+). Al compilar,
restaura automáticamente todos los paquetes NuGet (Dapper, JWT, SignalR,
BCrypt, **QuestPDF**, **CsvHelper**, etc.).

### 4. Ejecutar ambos proyectos a la vez
1. Click derecho sobre la **solución** → *Configurar proyectos de inicio…*
2. Elija **"Varios proyectos de inicio"**, ponga **Iniciar** en `Escuela_API`
   y en `Escuela_WEB`.
3. Presione **F5**. Se abren dos pestañas:
   - `Escuela_API` → `https://localhost:7100/swagger`
   - `Escuela_WEB` → `https://localhost:7200/`

Si el puerto de la API cambia, actualice `Escuela_WEB/appsettings.Development.json` → `Api:BaseUrl`.

## Cuentas de prueba (incluidas en el script SQL)

| Rol        | Correo                    | Clave           |
|------------|----------------------------|------------------|
| Director   | director@escuela.cr       | director123      |
| Docente    | docente@escuela.cr        | docente123       |
| Estudiante | estudiante1@escuela.cr    | estudiante123    |
| Estudiante | estudiante2@escuela.cr    | estudiante123    |
| Docente    | docente2@escuela.cr       | docente123       |
| Estudiante | estudiante3@escuela.cr    | estudiante123    |

---

# Guía: cómo funciona cada cosa y de dónde sale cada dato

## Idea central: dos catálogos y una regla para el docente

| Dato | De dónde sale | Quién lo administra |
|---|---|---|
| **Secciones** (grados) | Tabla `Seccion` | Director → *Administración → Secciones* |
| **Aulas** | Tabla `Aula` | Director → *Administración → Aulas* |
| **Sección de un estudiante** | `Usuario.Seccion` (debe existir en `Seccion`) | El estudiante la elige al registrarse; luego solo el Director la cambia |
| **Secciones de un docente** | **No se guardan**: se deducen de sus asignaturas activas (`Materia.ConsecutivoDocente`) | Se actualizan solas cuando el Director/docente crea o desactiva asignaturas |

Por eso el docente **ya no escribe ninguna sección al registrarse**: si la dirección le asigna
«Matemática – Quinto Grado», el docente pasa a ver automáticamente los estudiantes, avisos y
asistencia de Quinto Grado. Esa lógica vive en `Escuela_API/Services/AccesoHelper.cs` y la usan
estudiantes, avisos, mensajes, historial y asistencia (un docente nunca ve datos de secciones
donde no dicta clases).

## Registro y acceso de cuentas

1. **Estudiante** (pantalla *Crear cuenta*): elige su grado de una lista que sale de la tabla
   `Seccion`. La cuenta queda activa de inmediato.
2. **Docente**: no elige sección. Su cuenta se crea **pendiente de aprobación**
   (`Usuario.Pendiente = 1`, `Activo = 0`). El Director la ve resaltada en *Usuarios* (y en un aviso
   del inicio) y pulsa **Aprobar**. Luego crea/asigna sus asignaturas en *Asignaturas*.
3. **Director**: solo lo crea otro Director desde *Usuarios → Nuevo usuario*.
4. **Activar / desactivar**: *Usuarios → Desactivar / Activar*. Un usuario desactivado pierde el
   acceso **de inmediato**: la API revisa en cada petición que la cuenta siga activa y que su rol
   y sección no hayan cambiado (`Program.cs`, evento `OnTokenValidated`); la web lo devuelve al login.
5. **Olvidé mi contraseña**: ya no muestra ni cambia nada por sí sola (antes cualquiera podía
   tomar una cuenta ajena con solo su correo). Registra la solicitud (`Usuario.ClaveSolicitada`); el
   Director ve la marca «Pidió nueva clave» y pulsa 🔑 **Restablecer**: se genera una contraseña
   temporal que se muestra **una sola vez** para entregársela al usuario, quien la cambia en
   *Mi perfil*. Cambiar la contraseña ahora exige la contraseña actual.

## Horario y calendario sin choques

- **Pantalla *Horario de clases***: calendario semanal (lunes a viernes) con bloques posicionados
  según su hora real. Siempre muestra **un solo contexto** para que nunca se encimen:
  - Estudiante: su sección.
  - Docente: *Mi horario*, o consultar cualquier *sección* o *aula* (para ver qué espacio está libre).
  - Director: por *sección*, por *aula* o por *docente*.
- **Agregar bloque**: elige asignatura (de ahí salen su sección y su docente), día, hora de inicio y fin, y aula.
  Mientras completa el formulario aparece en vivo si el espacio está libre o con quién choca.
- **Regla anti-choque** (`HorarioController.cs`): en el mismo día no pueden solaparse dos bloques que
  compartan **aula**, **docente** o **sección**. Dos clases seguidas (07:00–08:00 y 08:00–09:00) son válidas.
  La verificación y el guardado ocurren en una transacción, así que dos personas no pueden reservar el
  mismo espacio a la vez. Además: horario lectivo 06:00–18:00, mínimo 20 minutos por bloque.
- Las aulas disponibles salen de la tabla `Aula`; una aula desactivada deja de ofrecerse pero conserva sus bloques.

## De dónde sale cada pantalla (módulo → tabla)

| Pantalla | Datos que muestra | Tabla(s) |
|---|---|---|
| Inicio (3 versiones) | Resúmenes por rol | `Usuario`, `Materia`, `HistorialAcademico`, `Asistencia`, `Aviso` |
| Asignaturas | Asignaturas activas/inactivas con su docente y sección | `Materia`, `Usuario` |
| Historial académico | Notas por estudiante/asignatura/periodo | `HistorialAcademico` |
| Asistencia | Lista de la sección de la asignatura + estado por día | `Usuario`, `Asistencia` |
| Horario de clases | Bloques semanales | `Horario`, `Materia`, `Aula`, `Usuario` |
| Avisos | Comunicados (generales o por sección) | `Aviso` |
| Mensajería | Conversaciones (tiempo real con SignalR) | `Mensaje` |
| Usuarios / Secciones / Aulas | Administración (solo Director) | `Usuario` / `Seccion` / `Aula` |
| Errores del sistema | Bitácora técnica | `RegistroError` |

Quién puede escribirle a quién (Mensajería): el docente, a los estudiantes de las secciones donde dicta
clases y al Director; el estudiante, a los docentes de su sección y al Director; el Director, a cualquiera.

## Qué se corrigió en esta versión

1. **Login / registro**: rediseño completo (tarjeta centrada, sin la columna «mini campus» ni el espacio vacío).
2. **Registro**: el docente ya no pide sección; el estudiante elige de una lista real; el docente queda pendiente de aprobación.
3. **Reactivar usuarios**: *causa raíz*: el botón enviaba `value="@(!u.Activo)"`; Razor omite un atributo
   booleano `false` y escribe `value="value"` para `true`, por lo que la API siempre recibía «desactivar».
   Ahora hay acciones separadas *Activar* / *Desactivar* y el mismo cuidado se aplicó a Secciones, Aulas y Asignaturas
   (que tampoco tenían forma de reactivarse).
4. **Horario**: calendario real + catálogo de aulas + detección de choques (antes se podían guardar choques y la vista era una lista de tarjetas).
5. **Registrar notas (docente)**: el formulario no enlazaba sus campos (prefijo `NuevoRegistro.`), así que no guardaba nada. Ahora la
   nota se liga a una asignatura real del docente y a un estudiante de esa sección.
6. **Seguridad / datos**: un docente ya no puede ver historial, asistencia ni estudiantes de otras secciones; el chat ya no
   interpreta HTML de los mensajes (XSS); la recuperación de contraseña dejó de permitir tomar cuentas; el CORS ahora incluye
   el origen de la web (`localhost:7200/5200`); `UseExceptionHandler` apuntaba a una ruta inexistente.
7. **Importar CSV**: acepta `,` `;` o tabulador, valida encabezados, rango de notas y que la asignatura sea del docente en la sección del estudiante.

## Lista de comprobación recomendada (5 minutos)

1. `director@escuela.cr` → *Usuarios*: desactive a un estudiante y vuelva a activarlo. Pruebe iniciar sesión con él entre ambos pasos.
2. Cierre sesión → *Crear cuenta* como docente (sin sección) → inicie sesión: debe decir «pendiente». Como Director, **Aprobar** y vuelva a probar.
3. Como Director → *Asignaturas → Nueva* asignándole esa docente una asignatura; luego, como docente, compruebe que ve a esa sección.
4. *Horario de clases → Agregar bloque*: intente repetir aula/hora (debe avisar el choque) y luego una hora libre.
5. Como docente, *Historial académico → Agregar* una nota y compruebe que el estudiante la ve.

> **Transparencia de pruebas:** este paquete se compiló completo (API + WEB, incluidas todas las vistas Razor) y la WEB se
> probó en navegador contra una API simulada (activar/desactivar, calendario, verificación de choques, registro, protección por rol).
> Las consultas SQL y los scripts no se pudieron ejecutar contra un SQL Server real en mi entorno (los scripts se validaron
> sintácticamente); por eso conviene correr la lista de comprobación de arriba la primera vez.

---

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
