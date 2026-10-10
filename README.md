# The Uno

Juego de UNO para 3 jugadores hecho en C# con Avalonia (patrón MVVM). Una sola persona controla a los 3 jugadores desde la misma pantalla. Las partidas, el historial y todos los movimientos se guardan en una base de datos SQLite a través de una API REST en Python (FastAPI).

## Requisitos

- Windows 10/11 o macOS
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Python 3.10 o superior](https://www.python.org/downloads/) (en Windows, al instalarlo marcar **"Add Python to PATH"**)
- Conexión a internet la primera vez, para descargar las bibliotecas de Python

## Cómo ejecutarlo

Se necesitan **dos terminales**: una para la API (base de datos) y otra para el juego. Todos los comandos se escriben desde la carpeta raíz del repositorio.

### 1. Abrir la API (primera terminal)

**Windows:**
```bash
cd src/TheUno.Infrastructure/Api
python -m pip install -r requirements.txt
python -m uvicorn api:app --port 8000
```

**macOS:**
```bash
cd src/TheUno.Infrastructure/Api
python3 -m pip install -r requirements.txt
python3 -m uvicorn api:app --port 8000
```

- El `pip install` descarga las bibliotecas de Python (`fastapi`, `uvicorn`, `pydantic`). Solo hace falta la primera vez.
- Cuando aparezca `Uvicorn running on http://127.0.0.1:8000`, la API ya está funcionando.
- **Dejar esa terminal abierta** mientras se juega. Para apagar la API: `Ctrl+C`.

### 2. Abrir el juego (segunda terminal)

```bash
dotnet run --project src/TheUno.UI/TheUno.UI.csproj
```

También se puede abrir `The Uno.sln` en JetBrains Rider o Visual Studio y ejecutar el proyecto `TheUno.UI`.

Si la API no está abierta, el juego funciona igual pero **no guarda** la partida, y el historial muestra "No se pudo conectar con la API".

La base de datos `theuno.db` se crea sola en `src/TheUno.Infrastructure/Api` la primera vez que se abre la API, con los 3 jugadores del equipo ya registrados. La documentación de los endpoints se puede ver en `http://127.0.0.1:8000/docs` mientras la API está abierta.

## Cómo se juega

- **Tirar carta:** clic en una carta de la mano del jugador en turno. Las cartas que no se pueden tirar se ven más oscuras.
- **Comodín y +4:** al tirarlos se abre un selector para elegir el color. Según las reglas oficiales, el +4 solo se puede tirar si el jugador no tiene ninguna carta del color activo.
- **Robar:** clic en el mazo. Si la carta robada se puede tirar, aparece el botón **PASAR** para quedarse con ella.
- **Cantar UNO:** al tirar la penúltima carta aparece el botón **¡UNO!** con una barra de 2 segundos. Si no se pica a tiempo, el jugador recibe 2 cartas de penalización.
- **Historial:** desde el menú, **VER HISTORIAL** muestra la tabla de clasificación, la lista de partidas terminadas y la bitácora con todos los movimientos de la partida elegida.

## Estructura

```
src/
  TheUno.Core/             Motor de juego: reglas, modelos, eventos y contratos
  TheUno.Infrastructure/   Persistencia: repositorios de la API, Entity Framework y migraciones
    Api/                   API REST en Python (FastAPI + SQLite)
  TheUno.UI/               Interfaz en Avalonia: vistas, ViewModels, cartas, fondos, fuente y música
```

## Equipo

| Integrante | Rol | Proyecto |
|---|---|---|
| Rodrigo | Motor de juego | `TheUno.Core` |
| Tonatiuh | Interfaz y experiencia de usuario | `TheUno.UI` |
| Alejandro | Base de datos, persistencia y auditoría | `TheUno.Infrastructure` |
