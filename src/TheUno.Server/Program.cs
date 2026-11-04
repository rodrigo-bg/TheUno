using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel para escuchar en todas las interfaces de red en el puerto 5000 (LAN)
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Soporte de CORS para conexiones entrantes
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

// Soporte de SignalR para comunicación en tiempo real
builder.Services.AddSignalR();

// Inyección de dependencias para persistencia en FastAPI
var persistenceApiUrl = builder.Configuration["PersistenceApiUrl"] ?? "http://127.0.0.1:8000/";
builder.Services.AddHttpClient<IMatchRepository, ApiMatchRepository>(client =>
{
    client.BaseAddress = new Uri(persistenceApiUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHttpClient<IGameLogRepository, ApiGameLogRepository>(client =>
{
    client.BaseAddress = new Uri(persistenceApiUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

app.UseCors();

// Endpoint de diagnóstico para comprobar el estado del servidor
app.MapGet("/", () => Results.Ok(new
{
    status = "online",
    service = "TheUno.Server",
    message = "El servidor central de juego de The Uno está activo.",
    timestamp = DateTime.UtcNow
}));

app.Run();
