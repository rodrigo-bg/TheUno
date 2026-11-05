using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace TheUno.Tests;

/// <summary>
/// Prueba de punta a punta: 3 clientes SignalR reales juegan contra el servidor en memoria,
/// usando los mismos nombres de métodos y mensajes que la interfaz de Avalonia.
/// </summary>
public class GameHubIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameHubIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ThreeClients_PlayTogether_EachReceivesOnlyItsOwnHand()
    {
        string[] names = { "RODRIGO", "TONATIUH", "ALEJANDRO" };
        var states = names.ToDictionary(n => n, _ => new BlockingCollection<JsonElement>());
        var clients = new List<HubConnection>();

        foreach (string name in names)
        {
            HubConnection client = CreateClient();
            client.On<JsonElement>("StateChanged", s => states[name].Add(s));
            await client.StartAsync();
            clients.Add(client);
        }

        // Sala: el host crea la partida y cada quien elige su jugador
        await clients[0].InvokeAsync("CreateGame");
        for (int i = 0; i < names.Length; i++)
        {
            Assert.True(await clients[i].InvokeAsync<bool>("ChoosePlayer", names[i]));
        }
        Assert.False(await clients[1].InvokeAsync<bool>("ChoosePlayer", "RODRIGO"));

        await clients[0].InvokeAsync("StartGame");

        // Cada cliente recibe su propia mesa: su mano completa y de los rivales solo cuántas cartas tienen
        var first = names.ToDictionary(n => n, n => Take(states[n]));
        var handIds = new HashSet<string>();
        foreach (string name in names)
        {
            JsonElement state = first[name];
            Assert.Equal(name, state.GetProperty("myName").GetString());

            JsonElement me = state.GetProperty("players").EnumerateArray()
                .First(p => p.GetProperty("name").GetString() == name);
            Assert.Equal(me.GetProperty("cardCount").GetInt32(), state.GetProperty("myHand").GetArrayLength());

            foreach (JsonElement player in state.GetProperty("players").EnumerateArray())
            {
                Assert.False(player.TryGetProperty("hand", out _));
            }

            foreach (JsonElement card in state.GetProperty("myHand").EnumerateArray())
            {
                Assert.True(handIds.Add(card.GetProperty("id").GetString()!), "Una carta apareció en dos manos");
            }
        }

        // Una jugada del jugador en turno llega a los 3 clientes
        string current = first[names[0]].GetProperty("currentPlayerName").GetString()!;
        JsonElement currentState = first[current];
        HubConnection currentClient = clients[Array.IndexOf(names, current)];
        JsonElement[] playable = currentState.GetProperty("playableCardIds").EnumerateArray().ToArray();

        if (playable.Length > 0)
        {
            await currentClient.InvokeAsync("PlayCard", playable[0].GetGuid(), (int?)0);
        }
        else
        {
            await currentClient.InvokeAsync("DrawCard");
        }

        foreach (string name in names)
        {
            Take(states[name]);
        }

        foreach (HubConnection client in clients)
        {
            await client.DisposeAsync();
        }
    }

    private HubConnection CreateClient() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "gamehub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    private static JsonElement Take(BlockingCollection<JsonElement> queue)
    {
        Assert.True(queue.TryTake(out JsonElement state, TimeSpan.FromSeconds(5)), "No llegó el estado de la mesa");
        return state;
    }
}
