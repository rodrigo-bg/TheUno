using System;
using Microsoft.Extensions.DependencyInjection;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Repositories;

namespace TheUno.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configura e inyecta los clientes HTTP para IMatchRepository y IGameLogRepository
    /// apuntando al servidor FastAPI, facilitando su reutilización en Program.cs (servidor o cliente).
    /// </summary>
    public static IServiceCollection AddTheUnoInfrastructureHttpClients(this IServiceCollection services, string baseAddress = "http://127.0.0.1:8000/")
    {
        services.AddHttpClient<IMatchRepository, ApiMatchRepository>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHttpClient<IGameLogRepository, ApiGameLogRepository>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        return services;
    }
}
