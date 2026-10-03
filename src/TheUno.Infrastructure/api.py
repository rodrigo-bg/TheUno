from fastapi import FastAPI, HTTPException, status
from pydantic import BaseModel, Field
from typing import List, Optional
from datetime import datetime
import sqlite3
import uuid

app = FastAPI()

BD = "theuno.db"

# Clases
class MatchCreate(BaseModel):
    player_ids: List[int] = Field(..., min_items=3, max_items=3)

class MatchCreateResponse(BaseModel):
    match_id: str
    started_at: datetime
    
class LogEntryRequest(BaseModel):
    match_id: str
    turn_number: int
    player_id: str
    action: str
    card_details: Optional[str] = ""
    chosen_color: Optional[str] = None
    timestamp: datetime = Field(default_factory=datetime.utcnow)
    
class PlayerStatsResponse(BaseModel):
    player_id: str
    name: str
    matches_played: int
    matches_won: int
    matches_lost: int
    win_rate: float
    
# Funciones para la base de datos
def get_db_connection():
    conn = sqlite3.connect(BD)
    conn.row_factory = sqlite3.Row
    return conn

# ENDPOINTS REST

@app.post("/matches", response_model=MatchCreateResponse, status_code=status.HTTP_201_CREATED)
def create_match(payload: MatchCreateRequest):
    # Registra el incio de una nueva partida
    match_id = str(uuid.uuid4())
    started_at = datetime.utcnow().isoformat()
    
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute(
        'INSERT INTO "Matches" ("Id", "StartedAt", "TotalTurns", "Duration") VALUES (?, ?, ?, ?)',
        (match_id, started_at, 0 , "00:00:00")
    )
    conn.commit()
    conn.close()
    
    return MatchCreateResponse(match_id=match_id, started_at=started_at)

@app.post("/logs", status_code=status.HTTP_201_CREATED)
def log_action(payload: LogEntryRequest):
    conn = get_db_connection()
    cursor = conn.cursor()
    
    cursor.execute(
        '''
        INSERT INTO "GameLogs" ("MatchId", "TurnNumber", "PlayerId", "Action", "CardDetails", "ChosenColor", "Timestamp")
            VALUES (?, ? ,? ,? ,?, ?, ?)
        ''',
        (
            payload.match_id,
            payload.turn_number,
            payload.player_id,
            payload.action,
            payload.card_details,
            payload.chosen_color,
            payload.timestamp.isoformat()
        )
    )
    conn.commit()
    conn.close()
    
    return {"status": "success", "message": "Log registrado correctamente"}

@app.get("/players/{player_id}/stats", response_model=PlayerStatsResponse)
def get_player_stats(player_id: str):
    # Obtiene las estadistícas del jugador
    conn = get_db_connection()
    cursor = conn.cursor()
    
    # Verificar si existe
    player = cursor.execute('SELECT "Id", "Name" FROM "Players" WHERE "Id" = ?', (player_id,)).fetchone()
    if not player:
        conn.close()
        raise HTTPException(status_code=404, detail="Jugador no encontrado")

    # Contar total de partidas completadas
    total_matches = cursor.execute('SELECT COUNT(*) FROM "Matches" WHERE "EndedAt" IS NOT NULL').fetchone()[0]
    # Contar partidas ganadas por el jugador
    matches_won = cursor.execute(
        'SELECT COUNT(*) FROM "Matches" WHERE "WinnerId" = ? AND "EndedAt" IS NOT NULL"',
        (player_id,)
    ).fetchone()[0]
    
    conn.close()
    
    matches_lost = total_matches - matches_won
    win_rate = (matches_won / total_matches * 100.0) if matches_won > 0 else 0.0
    
    return PlayerStatsResponse(
        player_id=player["Id"],
        name=player["Name"],
        matches_played=total_matches,
        matches_won=matches_won,
        matches_lost=matches_lost,
        win_rate=round(win_rate, 2)
    )

    