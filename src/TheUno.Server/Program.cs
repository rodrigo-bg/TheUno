using Microsoft.AspNetCore.SignalR;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;
using TheUno.Server.Contracts;
using TheUno.Server.Hubs;
using TheUno.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Kestrel escucha en todas las interfaces de red (LAN). Puerto 5050: en macOS el 5000 lo ocupa AirPlay.
builder.WebHost.UseUrls(builder.Configuration["ServerUrl"] ?? "http://0.0.0.0:5050");

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

// Registrar el gestor central de sesión como Singleton para mantener el GameEngine en memoria
builder.Services.AddSingleton<GameSessionManager>();

var app = builder.Build();

app.UseCors();

// Suscribir la expiración de la ventana de 2 segundos de UNO para difundir el nuevo turno a todos los clientes
var sessionManager = app.Services.GetRequiredService<GameSessionManager>();
var hubContext = app.Services.GetRequiredService<IHubContext<GameHub>>();
sessionManager.OnUnoWindowExpired += async () =>
{
    var states = sessionManager.CreatePersonalizedStates();
    for (int i = 0; i < states.Count; i++)
    {
        await hubContext.Clients.Client(states[i].ConnectionId).SendAsync(HubMessages.StateChanged, states[i].State);
    }
};

// Mapear el Hub de SignalR en la ruta esperada por los clientes de Avalonia
app.MapHub<GameHub>("/gamehub");

// Endpoint de diagnóstico para comprobar el estado del servidor
app.MapGet("/", () => Results.Ok(new
{
    status = "online",
    service = "TheUno.Server",
    hub = "/gamehub",
    message = "El servidor central de juego de The Uno está activo.",
    timestamp = DateTime.UtcNow
}));

app.Run();

// Expuesto para las pruebas de integración (WebApplicationFactory)
public partial class Program;
