# Stockfish 11 — C# Port

[English](#english) | [Español](#español)

## English

C# port of the Stockfish 11 chess engine, organized as an independent library and a console application that exposes the engine through the Universal Chess Interface (UCI) protocol.

This independent project aims to preserve the original engine’s behavior while prioritizing readability, portability, and integration into .NET applications.

### Repository contents

- **C# library** with board representation, move generation for standard chess and Chess960, classical evaluation, multithreaded search, transposition tables, time management, and Syzygy endgame probing.
- **Console application** that integrates the library with a UCI interface for terminal use and compatible chess GUIs.

The engine evaluates positions using material, piece-square tables, pawn structure, mobility, threats, king safety, and specialized endgame rules.

This repository contains the source code for both projects. Build the executable locally and obtain Syzygy tablebases separately if you want to use them.

### Requirements

- **Build tools.** .NET 10 SDK.
- **Engine library.** C# 8 and .NET Standard 2.1, with `unsafe` enabled for specific memory operations.
- **Console application.** .NET 10.

### Project structure

```text
Stockfish-sf_11_CSharp_Library/
└── Stockfish-sf_11_CSharp_Library/
    └── Engine library source code

Stockfish-sf_11_CSharp_App/
└── Console application and UCI interface
```

### Building

Build the console application from the repository root. The build also compiles the referenced engine library.

```bash
dotnet build Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release
```

To build the library by itself, use the following command.

```bash
dotnet build Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library.csproj -c Release
```

Use **Release** builds for normal engine operation and performance comparisons.

### Syzygy tablebases

Syzygy tablebases are optional. The engine supports `.rtbw` (WDL) and `.rtbz` (DTZ) files for positions with up to seven pieces. It can use them during search when the files cover the position.

Set `SyzygyPath` to the directory containing the files. The same option is available in chess GUIs that expose UCI engine settings.

```text
setoption name SyzygyPath value C:\Tablebases\Syzygy
isready
```

### Running from a terminal

```bash
dotnet run --project Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release
```

Once the engine starts, enter the following UCI commands. Wait for `readyok` after `isready` before starting the search.

```text
uci
setoption name Threads value 1
setoption name Hash value 32
setoption name Ponder value false
isready
position startpos
go depth 16
```

The engine reports search progress through `info` messages and returns its selected move through `bestmove`. Send `stop` to end a search early or `quit` to close the application.

For diagnostics, `eval` displays the classical evaluation terms, `go perft 4` counts legal move sequences to depth four, and `bench` runs the engine benchmark.

### Using a chess GUI

1. Build or publish the console application in Release mode.
2. Register its executable as a UCI engine in your chess GUI.
3. Configure `Threads`, `Hash`, `MultiPV`, and other options as needed. Set `SyzygyPath` if you use tablebases or enable `UCI_Chess960` for Chess960.

To publish a Windows x64 application with the .NET runtime included, use the following command.

```bash
dotnet publish Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

Select the executable in `publish/win-x64` and keep the published directory together.

### Integrating the library

Reference the library project from an application that supports .NET Standard 2.1. This example assumes the application project folder is next to `Stockfish-sf_11_CSharp_Library/`.

```xml
<ItemGroup>
  <ProjectReference Include="../Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library.csproj" />
</ItemGroup>
```

The console application shows how to configure `UciEngine`, send commands, receive results, and dispose of engine resources. Send commands from a single owning thread. In a graphical application, forward output callbacks to the UI thread before updating controls.

### Compatibility and Unity

The library targets C# 8 and .NET Standard 2.1 and can be referenced independently of the console application. Unity Mono and IL2CPP compatibility remains to be validated, including worker threads, tablebase file access, and resource cleanup.

### Performance and correspondence with C++

The port aims to reproduce Stockfish 11’s behavior. For a direct comparison with the original C++ engine, use Release builds and keep the following conditions consistent.

- Position, variant, and search limit, such as fixed depth.
- Number of threads and transposition table size.
- `MultiPV`, playing strength, and contempt settings.
- Pondering and Syzygy settings.
- Runtime environment and initial state of the engine’s tables.

Use one thread for reproducible comparisons. Compare total nodes, score, best move, and principal variation alongside nodes per second.

### Original project and attribution

Stockfish is a free chess engine developed by its many contributors. This port derives from the Stockfish 11 release and retains the original copyright notices and attribution.

- [Stockfish 11 source code (`sf_11`)](https://github.com/official-stockfish/Stockfish/tree/sf_11)
- [Official Stockfish website](https://stockfishchess.org/)

### License

The port is distributed under the **GNU General Public License, version 3 or later (GPL-3.0-or-later)**. See the full license text in [LICENSE](LICENSE).

Distribution of modified source code or binaries must comply with the GPL, including providing the corresponding source code where required. Syzygy tablebases are obtained separately and are subject to their own distribution terms.

---

## Español

Port del motor de ajedrez Stockfish 11 a C#, organizado como una biblioteca independiente y una aplicación de consola que expone el motor mediante el protocolo UCI (Universal Chess Interface).

Este proyecto independiente busca conservar el comportamiento del motor original, con prioridad en la legibilidad, la portabilidad y la integración en aplicaciones .NET.

### Contenido del repositorio

- **Biblioteca C#** con representación del tablero, generación de jugadas para ajedrez estándar y Chess960, evaluación clásica, búsqueda multihilo, tablas de transposición, administración del tiempo y consulta de finales Syzygy.
- **Aplicación de consola** que integra la biblioteca con una interfaz UCI para utilizar el motor desde una terminal o una interfaz gráfica de ajedrez compatible.

El motor evalúa las posiciones según el material, las tablas de pieza y casilla, la estructura de peones, la movilidad, las amenazas, la seguridad del rey y las reglas especializadas de finales.

Este repositorio contiene el código fuente de los dos proyectos. Compila el ejecutable localmente y obtén las tablas Syzygy por separado si deseas usarlas.

### Requisitos

- **Herramientas de compilación.** SDK de .NET 10.
- **Biblioteca del motor.** C# 8 y .NET Standard 2.1, con `unsafe` habilitado para determinadas operaciones de memoria.
- **Aplicación de consola.** .NET 10.

### Organización del proyecto

```text
Stockfish-sf_11_CSharp_Library/
└── Stockfish-sf_11_CSharp_Library/
    └── Código fuente de la biblioteca del motor

Stockfish-sf_11_CSharp_App/
└── Aplicación de consola e interfaz UCI
```

### Compilación

Compila la aplicación de consola desde la raíz del repositorio. La compilación también genera la biblioteca que la aplicación referencia.

```bash
dotnet build Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release
```

Para compilar únicamente la biblioteca, usa el siguiente comando.

```bash
dotnet build Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library.csproj -c Release
```

Utiliza compilaciones **Release** para ejecutar el motor y comparar su rendimiento.

### Tablas de finales Syzygy

Las tablas de finales Syzygy son opcionales. El motor admite archivos `.rtbw` (WDL) y `.rtbz` (DTZ) para posiciones de hasta siete piezas. Puede consultarlos durante la búsqueda cuando los archivos cubren la posición.

Configura `SyzygyPath` con la ruta de la carpeta que contiene los archivos. La misma opción está disponible en las interfaces gráficas que permiten editar los ajustes UCI del motor.

```text
setoption name SyzygyPath value C:\Tablas\Syzygy
isready
```

### Ejecución desde terminal

```bash
dotnet run --project Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release
```

Una vez iniciado el motor, introduce los siguientes comandos UCI. Después de `isready`, espera la respuesta `readyok` antes de iniciar la búsqueda.

```text
uci
setoption name Threads value 1
setoption name Hash value 32
setoption name Ponder value false
isready
position startpos
go depth 16
```

El motor comunica el progreso mediante mensajes `info` y devuelve la jugada elegida con `bestmove`. Envía `stop` para detener una búsqueda antes de que termine o `quit` para cerrar la aplicación.

Para diagnóstico, `eval` muestra los términos de la evaluación clásica, `go perft 4` cuenta las secuencias de jugadas legales hasta profundidad cuatro y `bench` ejecuta la prueba de rendimiento del motor.

### Uso desde una interfaz gráfica de ajedrez

1. Compila o publica la aplicación de consola en Release.
2. Añade el ejecutable como motor UCI en tu interfaz de ajedrez.
3. Ajusta `Threads`, `Hash`, `MultiPV` y las demás opciones según tus necesidades. Configura `SyzygyPath` si usas tablas de finales o habilita `UCI_Chess960` para jugar Chess960.

Para publicar una aplicación Windows x64 que incluya el entorno de ejecución de .NET, usa el siguiente comando.

```bash
dotnet publish Stockfish-sf_11_CSharp_App/Stockfish-sf_11_CSharp_App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

Selecciona el ejecutable en `publish/win-x64` y conserva juntos los archivos de la carpeta publicada.

### Integración de la biblioteca

Referencia el proyecto de biblioteca desde una aplicación compatible con .NET Standard 2.1. Este ejemplo supone que la carpeta de tu proyecto está al mismo nivel que `Stockfish-sf_11_CSharp_Library/`.

```xml
<ItemGroup>
  <ProjectReference Include="../Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library/Stockfish-sf_11_CSharp_Library.csproj" />
</ItemGroup>
```

La aplicación de consola muestra cómo configurar `UciEngine`, enviar comandos, recibir resultados y liberar los recursos del motor. Envía los comandos desde un único hilo. En una aplicación gráfica, entrega los mensajes recibidos al hilo de la interfaz antes de actualizar los controles.

### Compatibilidad y Unity

La biblioteca utiliza C# 8 y .NET Standard 2.1 y se puede referenciar de forma independiente de la aplicación de consola. La compatibilidad con Unity Mono e IL2CPP queda pendiente de validación, incluidos los hilos de búsqueda, el acceso a archivos Syzygy y la liberación de recursos.

### Rendimiento y correspondencia con C++

El port busca reproducir el comportamiento de Stockfish 11. Para compararlo directamente con el motor original en C++, utiliza compilaciones Release y mantén constantes las siguientes condiciones.

- Posición, variante y límite de búsqueda, por ejemplo, una profundidad fija.
- Número de hilos y tamaño de la tabla de transposiciones.
- Opciones de `MultiPV`, nivel de juego y `Contempt`.
- Opciones de ponderación y Syzygy.
- Entorno de ejecución y estado inicial de las tablas del motor.

Usa un solo hilo para obtener comparaciones reproducibles. Compara los nodos totales, la puntuación, la mejor jugada y la variante principal junto con los nodos por segundo.

### Proyecto original y atribución

Stockfish es un motor de ajedrez libre desarrollado por sus numerosos colaboradores. Este port deriva de la versión Stockfish 11 y conserva los avisos de copyright y las atribuciones correspondientes.

- [Código fuente original de Stockfish 11 (`sf_11`)](https://github.com/official-stockfish/Stockfish/tree/sf_11)
- [Sitio oficial de Stockfish](https://stockfishchess.org/)

### Licencia

El port se distribuye bajo la **Licencia Pública General de GNU, versión 3 o posterior (GPL-3.0-or-later)**. Consulta el texto completo en [LICENSE](LICENSE).

La distribución de código modificado o de binarios debe cumplir la GPL, incluida la entrega del código fuente correspondiente cuando proceda. Las tablas Syzygy se obtienen por separado y están sujetas a sus propias condiciones de distribución.
