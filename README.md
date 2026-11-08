# The Uno

Juego de UNO para 3 jugadores hecho en C# con Avalonia (patrón MVVM). Cada jugador juega desde su propia computadora en la misma red local (LAN): una de ellas es el **servidor**, que lleva la partida, y las 3 se conectan como clientes. Cada quien solo ve sus propias cartas; las de los rivales se ven boca abajo. Las partidas, el historial y todos los movimientos se guardan en una base de datos SQLite a través de una API REST en Python (FastAPI) que corre en la compu servidor.

## Requisitos

- Windows 10/11 o macOS, en **todas** las compus
- [.NET 10 SDK](https://dotnet.microsoft.com/download), en **todas** las compus
- [Python 3.10 o superior](https://www.python.org/downloads/), **solo en la compu servidor** (en Windows, al instalarlo marcar **"Add Python to PATH"**)
- Las 3 compus conectadas a la **misma red** (el mismo Wi-Fi o el mismo router)
- Conexión a internet la primera vez, para descargar las bibliotecas de Python y de .NET

## Cómo jugar en distintas computadoras

Todos los comandos se escriben desde la carpeta raíz del repositorio. En macOS, donde dice `python` se escribe `python3`.

### 1. En la compu servidor

Se necesitan **dos terminales** abiertas mientras dure el juego.

**Primera terminal: la API (base de datos)**
```bash
cd src/TheUno.Infrastructure/Api
python -m pip install -r requirements.txt
python -m uvicorn api:app --host 0.0.0.0 --port 8000
```
- El `pip install` descarga las bibliotecas de Python (`fastapi`, `uvicorn`, `pydantic`). Solo hace falta la primera vez.
- Cuando aparezca `Uvicorn running on http://0.0.0.0:8000`, la API ya está funcionando.
- La base de datos `theuno.db` se crea sola en `src/TheUno.Infrastructure/Api` la primera vez, con los 3 jugadores del equipo ya registrados.

**Segunda terminal: el servidor del juego**
```bash
dotnet run --project src/TheUno.Server
```
- Escucha en el puerto **5000**. Ahí vive el motor de juego: valida las reglas, reparte las cartas y guarda cada partida en la API.

**Permitir las conexiones:** la primera vez, Windows (o macOS) pregunta si la API y el servidor pueden usar la red. Hay que **permitirlo** en redes privadas; si no, las otras compus no se pueden conectar. Se usan los puertos **5000** (juego) y **8000** (historial).

### 2. En cada compu (también en la del servidor)

```bash
dotnet run --project src/TheUno.UI/TheUno.UI.csproj
```

También se puede abrir `The Uno.sln` en JetBrains Rider o Visual Studio y ejecutar el proyecto `TheUno.UI`.

### 3. Entrar a la partida

1. **En la compu servidor:** **INICIAR JUEGO** → **CREAR PARTIDA**. La pantalla muestra **TU IP** (por ejemplo, `192.168.1.50`): es la que escriben los demás. Elige con qué jugador quieres jugar.
2. **En las otras dos compus:** **INICIAR JUEGO** → **UNIRSE A PARTIDA**. Escribe la IP del servidor, pica **CONECTAR** (o Enter) y elige un jugador. Los jugadores que ya eligió alguien aparecen como **OCUPADO**.
3. Todos esperan en la **Sala de conexión**. Cuando aparece **JUGADORES 3/3**, quien creó la partida pica **INICIAR JUEGO**.

### Probar en una sola computadora

Para probar sin otras compus, se abren 3 veces el juego en la misma compu (además de la API y el servidor). Primero se compila una vez y luego se abre cada ventana en su propia terminal:
```bash
dotnet build src/TheUno.UI/TheUno.UI.csproj
dotnet src/TheUno.UI/bin/Debug/net10.0/TheUno.UI.dll
```
Una ventana crea la partida y las otras dos se unen con la IP `127.0.0.1`.

## Cómo se juega

- **Tu mano** siempre está abajo; los rivales están a los lados con sus cartas boca abajo. Solo puedes jugar en tu turno.
- **Tirar carta:** clic en una carta. Las que no se pueden tirar se ven más oscuras.
- **Comodín y +4:** al tirarlos se abre un selector para elegir el color. Según las reglas oficiales, el +4 solo se puede tirar si no tienes ninguna carta del color activo.
- **Robar:** clic en el mazo. Si la carta robada se puede tirar, aparece el botón **PASAR** para quedarse con ella.
- **Cantar UNO:** al tirar la penúltima carta aparece el botón **¡UNO!** con una barra de 2 segundos. Si no se pica a tiempo, el jugador recibe 2 cartas de penalización.
- **Fin de partida:** aparece **¡¡GANASTE!!** o **¡¡PERDISTE!!**. **Volver a jugar** regresa a la sala de conexión para elegir jugador otra vez.
- **Historial:** desde el menú, **VER HISTORIAL** muestra la tabla de clasificación, las partidas terminadas y la bitácora de movimientos. Lee la API de la compu servidor: toma sola la IP de la última partida, y en **SERVIDOR** se puede escribir otra y picar **RECARGAR**.
- **Sonido:** el botón de la bocina, arriba a la izquierda del menú, quita o regresa la música y los efectos.
- **Pantalla completa:** **F11** (en algunas laptops, **Fn + F11**).

## Si algo no funciona

| Problema | Qué revisar |
|---|---|
| "No se pudo conectar a esa IP" | Que la IP sea la que muestra **CREAR PARTIDA** en la compu servidor, que el servidor del juego esté abierto, que las compus estén en la misma red y que el firewall permita el puerto 5000. |
| "No se pudo conectar con el servidor" al crear la partida | El servidor del juego no está abierto en esa compu. |
| "ERROR: Partida existente ya creada con la misma red IP" | Alguien ya creó la partida en ese servidor: hay que entrar con **UNIRSE A PARTIDA**. |
| El historial dice "No se pudo conectar con la API" | Que la API esté abierta en la compu servidor con `--host 0.0.0.0`, que el firewall permita el puerto 8000 y que la IP de **SERVIDOR** sea la correcta. |
| Una partida no aparece en el historial | Solo se guardan las partidas que terminan con un ganador. |
| "Se perdió la conexión con el servidor" | Se cerró el servidor del juego o se cayó la red. Hay que regresar al menú y volver a entrar. |

## Estructura

```
src/
  TheUno.Core/             Motor de juego: reglas, modelos, eventos y contratos
  TheUno.Infrastructure/   Persistencia: repositorios de la API, auditoría, Entity Framework y migraciones
    Api/                   API REST en Python (FastAPI + SQLite)
  TheUno.Server/           Servidor del juego (ASP.NET Core + SignalR): lleva la partida
  TheUno.UI/               Cliente en Avalonia: vistas, ViewModels, cartas, fondos, fuente, música y conexión al servidor
```

## Equipo

| Integrante | Rol | Proyecto |
|---|---|---|
| Rodrigo | Motor de juego y servidor | `TheUno.Core`, `TheUno.Server` |
| Tonatiuh | Interfaz y cliente de red | `TheUno.UI` |
| Alejandro | Base de datos, persistencia y auditoría | `TheUno.Infrastructure` |
