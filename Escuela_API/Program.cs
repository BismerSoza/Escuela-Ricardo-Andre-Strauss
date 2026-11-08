using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Escuela_API.Hubs;
using Escuela_API.Middleware;
using Escuela_API.Services;
using QuestPDF.Infrastructure;
using System.Text;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUtilesService, UtilesService>();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration["Cors:Origins"]!
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    options.AddPolicy("EscuelaPolicy", policy =>
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
            ClockSkew = TimeSpan.Zero
        };

        // SignalR no puede enviar el header Authorization en WebSockets,
        // por lo que el token se recibe como query string ("access_token").
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware centralizado de errores: cualquier excepción no controlada en
// cualquier controlador/servicio termina aquí y se convierte en un JSON
// amigable en lugar de tumbar la aplicación (ver Middleware/ManejadorErroresMiddleware.cs)
app.UseManejadorErrores();

// Sin redireccion a HTTPS en desarrollo: el WEB llama a la API por http://localhost:5100 y
// una redireccion hace que HttpClient pierda el encabezado Authorization (error 401).
// app.UseHttpsRedirection();

app.UseCors("EscuelaPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run();
