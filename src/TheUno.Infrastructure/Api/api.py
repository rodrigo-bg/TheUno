import sqlite3
import uuid
from datetime import datetime
from typing import List, Optional
from fastapi import FastAPI, HTTPException, status
from pydantic import BaseModel, Field

app = FastAPI(
    title="TheUno Persistence API",
    description="API REST en Python para la gestión de partidas, auditoría de eventos y estadísticas de UNO",
    version="1.0.0"
)

DB_NAME = "theuno.db"


# ==========================================
# FUNCIONES AUXILIARES DE BASE DE DATOS
# ==========================================

def get_db_connection():
    """
    Establece la conexión con la base de datos SQLite local.
    Configura row_factory para obtener resultados mapeados como diccionarios.
    """
    conn = sqlite3.connect(DB_NAME)
    conn.row_factory = sqlite3.Row
    return conn


def init_db():
    """
    Asegura que la estructura de las tablas exista al arrancar el servidor.
    """
    conn = get_db_connection()
    cursor = conn.cursor()

    # Tabla Players
    cursor.execute('''
                   CREATE TABLE IF NOT EXISTS "Players" (
                        "Id" TEXT NOT NULL PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL
                   );
                   ''')

    # Tabla Matches
    cursor.execute('''
                   CREATE TABLE IF NOT EXISTS "Matches" (
                        "Id" TEXT NOT NULL PRIMARY KEY,
                        "StartedAt" TEXT NOT NULL,
                        "EndedAt" TEXT NULL,
                        "TotalTurns" INTEGER NOT NULL DEFAULT 0,
                        "Duration" TEXT NOT NULL DEFAULT '00:00:00',
                        "WinnerId" TEXT NULL,
                        FOREIGN KEY ("WinnerId") REFERENCES "Players" ("Id") ON DELETE SET NULL
                       );
                   ''')

    # Tabla MatchPlayer (Relación N:M entre Partidas y Jugadores)
    cursor.execute('''
                   CREATE TABLE IF NOT EXISTS "MatchPlayers" (
                        "MatchId" TEXT NOT NULL,
                        "PlayerId" TEXT NOT NULL,
                        PRIMARY KEY ("MatchId", "PlayerId"),
                        FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE CASCADE,
                        FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE CASCADE
                       );
                   ''')

    # Tabla GameLogs (Auditoría de eventos)
    cursor.execute('''
                   CREATE TABLE IF NOT EXISTS "GameLogs" (
                        "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                        "MatchId" TEXT NOT NULL,
                        "TurnNumber" INTEGER NOT NULL,
                        "PlayerId" TEXT NOT NULL,
                        "Action" TEXT NOT NULL,
                        "CardDetails" TEXT NULL,
                        "ChosenColor" TEXT NULL,
                        "Timestamp" TEXT NOT NULL,
                        FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE CASCADE,
                        FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE RESTRICT
                       );
                   ''')

    conn.commit()
    conn.close()


# Inicializar tablas al cargar el módulo
init_db()


# ==========================================
# ESQUEMAS / MODELOS PYDANTIC (JSON Contracts)
# ==========================================

class MatchCreateRequest(BaseModel):
    player_ids: List[str] = Field(..., description="Lista de Guids de los jugadores de la partida", min_items=1)

class MatchCreateResponse(BaseModel):
    match_id: str
    started_at: str

class MatchCompleteRequest(BaseModel):
    winner_id: str
    total_turns: int
    duration: str

class LogEntryRequest(BaseModel):
    match_id: str
    turn_number: int
    player_id: str
    action: str
    card_details: Optional[str] = ""
    chosen_color: Optional[str] = None
    timestamp: Optional[str] = None

class PlayerStatsResponse(BaseModel):
    player_id: str
    name: str
    matches_played: int
    matches_won: int
    matches_lost: int
    win_rate: float
    average_duration_minutes: float
    total_score: int


# ==========================================
# ENDPOINTS REST
# ==========================================

@app.get("/")
def read_root():
    return {"status": "online", "message": "TheUno Persistence API is running"}


@app.post("/matches", response_model=MatchCreateResponse, status_code=status.HTTP_201_CREATED)
def create_match(payload: MatchCreateRequest):
    """
    Registra el inicio de una nueva partida y vincula a los jugadores participantes.
    """
    match_id = str(uuid.uuid4())
    started_at = datetime.utcnow().isoformat()

    conn = get_db_connection()
    cursor = conn.cursor()

    try:
        # Registrar partida
        cursor.execute(
            'INSERT INTO "Matches" ("Id", "StartedAt", "TotalTurns", "Duration") VALUES (?, ?, ?, ?)',
            (match_id, started_at, 0, "00:00:00")
        )

        # Vincular jugadores en MatchPlayers
        for player_id in payload.player_ids:
            # Asegurar existencia del jugador básico si no existe previamente
            cursor.execute(
                'INSERT OR IGNORE INTO "Players" ("Id", "Name", "CreatedAt") VALUES (?, ?, ?)',
                (player_id, f"Player-{player_id[:4]}", datetime.utcnow().isoformat())
            )
            cursor.execute(
                'INSERT INTO "MatchPlayers" ("MatchId", "PlayerId") VALUES (?, ?)',
                (match_id, player_id)
            )

        conn.commit()
    except Exception as e:
        conn.rollback()
        conn.close()
        raise HTTPException(status_code=500, detail=f"Error creando la partida: {str(e)}")

    conn.close()
    return MatchCreateResponse(match_id=match_id, started_at=started_at)


@app.post("/matches/{match_id}/complete", status_code=status.HTTP_200_OK)
def complete_match(match_id: str, payload: MatchCompleteRequest):
    """
    Marca una partida como completada registrando al ganador, duración y turnos totales.
    """
    ended_at = datetime.utcnow().isoformat()
    conn = get_db_connection()
    cursor = conn.cursor()

    cursor.execute('SELECT "Id" FROM "Matches" WHERE "Id" = ?', (match_id,))
    if not cursor.fetchone():
        conn.close()
        raise HTTPException(status_code=404, detail="Partida no encontrada")

    cursor.execute(
        '''
        UPDATE "Matches"
        SET "EndedAt" = ?, "WinnerId" = ?, "TotalTurns" = ?, "Duration" = ?
        WHERE "Id" = ?
        ''',
        (ended_at, payload.winner_id, payload.total_turns, payload.duration, match_id)
    )

    conn.commit()
    conn.close()
    return {"status": "success", "message": "Partida finalizada correctamente"}


@app.post("/logs", status_code=status.HTTP_201_CREATED)
def log_action(payload: LogEntryRequest):
    """
    Registra una entrada de auditoría en la partida (jugar carta, robar, penalización).
    """
    conn = get_db_connection()
    cursor = conn.cursor()

    log_timestamp = payload.timestamp or datetime.utcnow().isoformat()

    cursor.execute(
        '''
        INSERT INTO "GameLogs" ("MatchId", "TurnNumber", "PlayerId", "Action", "CardDetails", "ChosenColor", "Timestamp")
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ''',
        (
            payload.match_id,
            payload.turn_number,
            payload.player_id,
            payload.action,
            payload.card_details,
            payload.chosen_color,
            log_timestamp
        )
    )

    conn.commit()
    conn.close()
    return {"status": "success", "message": "Log registrado correctamente"}


@app.get("/players/stats", response_model=List[PlayerStatsResponse])
def get_leaderboard_stats():
    """
    Obtiene la tabla de clasificación (Leaderboard) con estadísticas acumuladas de todos los jugadores.
    """
    conn = get_db_connection()
    cursor = conn.cursor()

    # Calcular duración promedio global de partidas finalizadas
    cursor.execute('SELECT AVG(JULIANDAY("EndedAt") - JULIANDAY("StartedAt")) * 1440 FROM "Matches" WHERE "EndedAt" IS NOT NULL')
    avg_duration_res = cursor.fetchone()[0]
    avg_duration = round(avg_duration_res, 2) if avg_duration_res else 0.0

    query = '''
            SELECT
                p."Id" AS player_id,
                p."Name" AS name,
                COUNT(mp."MatchId") AS matches_played,
                SUM(CASE WHEN m."WinnerId" = p."Id" THEN 1 ELSE 0 END) AS matches_won
            FROM "Players" p
                     LEFT JOIN "MatchPlayers" mp ON p."Id" = mp."PlayerId"
                     LEFT JOIN "Matches" m ON mp."MatchId" = m."Id" AND m."EndedAt" IS NOT NULL
            GROUP BY p."Id", p."Name"; \
            '''

    rows = cursor.execute(query).fetchall()
    conn.close()

    results = []
    for row in rows:
        played = row["matches_played"] or 0
        won = row["matches_won"] or 0
        lost = played - won
        rate = (won / played * 100.0) if played > 0 else 0.0

        results.append(PlayerStatsResponse(
            player_id=row["player_id"],
            name=row["name"],
            matches_played=played,
            matches_won=won,
            matches_lost=lost,
            win_rate=round(rate, 2),
            average_duration_minutes=avg_duration,
            total_score=won * 100 # Puntuación estimada o acumulada
        ))

    return results