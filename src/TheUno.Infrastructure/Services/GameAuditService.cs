/*using Microsoft.Extensions.Logging;
using TheUno.Core.Contracts;
using TheUno.Infrastructure.Engine;

namespace TheUno.Infrastructure.Services;

public class GameAuditService : IDisposable
{
    private readonly IGameLogRepository _logRepository;
    private readonly IGameEngine _gameEngine;
    private readonly Guid _currentMatchId;
    private int _currenTurnNumber;

    public GameAuditService(IGameLogRepository logRepository, IGameEngine gameEngine, Guid matchID)
    {
        _logRepository = logRepository;
        _gameEngine = gameEngine;
        _currentMatchId = matchID;
        _currenTurnNumber = 1;
        
        // Suscribirse a los eventos del motor de juego
        SubscribeToEngineEvents();
    }

    private void SubscribeToEngineEvents()
    {
        
    }
}*/