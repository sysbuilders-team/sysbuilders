# SysBuilders

**SysBuilders** is a 1v1 educational software architecture game built with **Unity** and **C#**, designed to help Computer Science and Information Systems students practice **Object-Oriented Analysis and Design (OOAD)**, cohesion, coupling, and refactoring under evolving requirements.

Players take on the role of software architects competing across 5 turns to design the cleanest, most cohesive UML class diagram as new functional requirements arrive each round.

---

## Table of Contents

- [Features](#features)
- [Gameplay & Scoring](#gameplay--scoring)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Documentation](#documentation)
- [License](#license)

---

## Features

- **1v1 Turn-Based Architecture Duels**: Asynchronous simultaneous turns synchronized over LAN via Mirror (`Show -> Build -> Evaluate -> Completed`).
- **Interactive UML Class Modeling**: Create classes, allocate method cards, draw associations between classes, and refactor previously locked classes.
- **Automated Design Evaluation**: Evaluates fulfilled requirements, domain associations, and common object-oriented code smells in real time at the end of each turn.
- **Resource & Time Pressure**: Class-per-turn limits, match class caps, turn countdown timers, and limited Refactor Points force deliberate architectural trade-offs.
- **Zero-Config LAN Discovery & Security**: Custom pure-C# RFC 6762/6763 mDNS room discovery (`224.0.0.251:5353`) with direct IP fallback and TOFU (Trust On First Use) host public-key fingerprint pinning over Mirror's `EncryptionTransport`.
- **Offline Editor Testing**: Built-in `EditorOfflineBootstrap` allows solo playtesting directly inside `GameScene` in the Unity Editor.

---

## Gameplay & Scoring

Each match runs for **5 turns** along a themed requirement trail (loaded from `Assets/Resources/requirements.json`, e.g., `Biblioteca` / `trail_biblioteca_01`):

1. **Requirement Reveal (`Show` / `Build`)**: A new functional requirement and its method cards are revealed. Each method belongs to a hidden domain category (such as `Usuario`, `Acervo`, `Circulacao`, `Notificacao`, or `Financeiro`) that players infer from the method names and requirement description.
2. **Diagram Construction**:
   - Create up to **2 classes per turn** (up to **7 classes per match** by default).
   - Allocate method cards into classes and create **Association** links between classes.
   - Unlock and modify classes locked from earlier turns by spending **Refactor Points** (`DefaultRefactorPointPolicy`: `1` initial point, `+1` per turn from turn 2 onward).
   - Complete the turn before the countdown expires (`120s` base + `30s` per subsequent turn).
3. **Lock & Evaluate (`Evaluate`)**: Modified classes are locked and `EvaluationEngine` scores the diagram:
   - **+100 pts** per fulfilled requirement (all required methods present within connected components)
   - **+50 pts** Clean Architecture bonus (at least 1 fulfilled requirement and 0 detected smells)
   - **+25 pts** per satisfied domain association rule
   - **-25 pts** per unallocated required method
   - **Code Smell Penalties** (`basePenalty * severity`):
     - `LargeClass` (`25` × severity): class exceeds `5` methods
     - `LowCohesion` (`30` × severity): class mixes `2+` distinct method categories
     - `DuplicateCode` (`40` × severity): identical `(Name, Category)` method placed in multiple classes
     - `LowAffinityCoupling` (`20` × severity): association connecting categories without domain affinity
4. **Match Score**: Computed as the latest cumulative `RawScore` minus the sum of all per-turn penalties, floored at `0` (`ScoreRules.Score`). Detailed smell diagnostics are withheld until the final turn modal (`EndGameModalController`) so players must deduce issues from round summaries.

---

## Architecture

The codebase follows a **Ports & Adapters** design split into four Unity assemblies with strict dependency boundaries:

```text
Sys.UI ──┐
         ├──► Sys.Core (Pure C#, zero Mirror dependency)
Sys.Managers ◄── Sys.Network (Mirror + mDNS Discovery adapters)
```

| Assembly | Path | Responsibility |
| :--- | :--- | :--- |
| **`Sys.Core`** | `Assets/Scripts/Core/` | Pure C# domain models and rules: `Diagram` (`ClassDiagram`, `DiagramEditor`), `Requirements` (`RequirementLoader`, `RequirementCatalog`), `Evaluation` (`EvaluationEngine`, `CodeSmellDetector`, `ScoreRules`), `Session` (`MatchSession`, `TurnManager`, `MatchScoreTracker`, `RefactorHandler`), and `Discovery` (RFC 6762/6763 mDNS codec/responder/browser & `TofuPolicy`). |
| **`Sys.UI`** | `Assets/Scripts/UI/` | Unity UI controllers (`TopMenuController`, `RightBarController`, `ToolboxController`, `DiagramController`, `EvaluationEndTurnController`, `EndGameModalController`, lobby & menu controllers) consuming segregated Core interfaces. Zero dependency on Mirror or `Sys.Network`. |
| **`Sys.Managers`** | `Assets/Scripts/Managers/` | Scene composition root (`GameManager`), `RequirementManager`, `SceneNavigator`, `TurnTimerService`, and `EditorOfflineBootstrap`. Zero dependency on Mirror. |
| **`Sys.Network`** | `Assets/Scripts/Network/` | Mirror networking and discovery adapters: `GameRoomManager`, `GameRoomPlayer`, `MatchSyncBridge`, `MenuNetworkController`, `RoomDiscoveryService`, `RoomLobbyAdapter`, `ReadyQuorum`, `SmellWireCodec`, `EvaluationPayloadValidator`, and `KnownHostStore`. |

---

## Tech Stack

- **Engine**: Unity `6000.3.21f1`
- **Rendering & UI**: Universal Render Pipeline (`17.3.0`), Unity UI (`com.unity.ugui` `2.0.0`), TextMesh Pro, Input System (`1.20.0`)
- **Networking**: Mirror (`96.0.1`) + `EncryptionTransport` + custom mDNS LAN discovery
- **Data / Serialization**: `Newtonsoft.Json` (`com.unity.nuget.newtonsoft-json` `3.2.2`)
- **Testing**: Unity Test Framework (`1.6.0`)

---

## Project Structure

```text
sysbuilders/
├── Assets/
│   ├── Resources/
│   │   ├── Icons/
│   │   └── requirements.json          # Requirement trails, methods, and association rules
│   ├── Scenes/
│   │   ├── Menu.unity                 # Main menu & LAN room lobby
│   │   └── GameScene.unity            # Core gameplay scene
│   ├── Scripts/
│   │   ├── Core/                      # Sys.Core assembly (Diagram, Discovery, Evaluation, Requirements, Session)
│   │   ├── Managers/                  # Sys.Managers assembly (GameManager, RequirementManager, Timers)
│   │   ├── Network/                   # Sys.Network assembly (Mirror sync, lobby, mDNS service)
│   │   └── UI/                        # Sys.UI assembly (Canvas, Toolbox, Modals, Menu UI)
│   └── Tests/
│       └── EditMode/                  # 24 NUnit EditMode test suites (Tests1 assembly)
├── docs/                              # Game Design Document, architecture notes, and roadmap
├── Packages/                          # Unity Package Manager manifest & lockfile
├── ProjectSettings/                   # Unity project & editor version settings
├── LICENSE
└── README.md
```

---

## Getting Started

### Prerequisites

- [Unity Hub](https://unity.com/download)
- **Unity `6000.3.21f1`**

### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/sysbuilders-team/sysbuilders.git
   cd sysbuilders
   ```
2. Open the project folder in **Unity Hub** using Unity `6000.3.21f1`.

### Running the Game

- **Solo / Quick Test (Unity Editor)**:
  1. Open `Assets/Scenes/GameScene.unity`.
  2. Press **Play**. `EditorOfflineBootstrap` automatically starts a local match using the `Biblioteca` theme (`trail_biblioteca_01`).
- **1v1 LAN Multiplayer**:
  1. Open `Assets/Scenes/Menu.unity` (or launch two builds / Editor + Build on the same local network).
  2. **Host**: Create a room from the menu. The room is advertised on the LAN via mDNS.
  3. **Client**: Select the discovered room from the lobby list or connect directly by IP address, ready up, and start the match.

---

## Running Tests

The repository includes **24 EditMode test suites** covering diagram editing, class naming rules, requirement loading, code smell detection, scoring rules, turn management, room discovery, payload validation, and UI controllers.

1. In the Unity Editor, open **Window → General → Test Runner**.
2. Select the **EditMode** tab.
3. Click **Run All** to execute the `Tests1` assembly (`Assets/Tests/EditMode/`).

---

## Documentation

Additional design and technical documentation is available in [`docs/`](docs/):

- [`docs/GameDesignDocument.md`](docs/GameDesignDocument.md) — Game design one-pager, core loop, and educational goals (PT-BR)
- [`docs/architectural_analysis.md`](docs/architectural_analysis.md) — Layering, SOLID, and component interaction analysis
- [`docs/roadmap.md`](docs/roadmap.md) — Layer breakdown and Phase 1 / Phase 2 roadmap (PT-BR)
- [`docs/server-authoritative-scoring.md`](docs/server-authoritative-scoring.md) — Multiplayer scoring invariants and synchronization notes

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.
