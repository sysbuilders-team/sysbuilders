PROJECT
SysBuilders is a 1v1 educational Unity game teaching Object-Oriented Analysis
and Design (Análise e Projeto de Sistemas) to Computer Science students at
UFSM. Players act as software architects: each turn introduces new
requirements and available methods, and players build/evolve a class diagram
(create classes, add methods, create relations, refactor). At turn end,
diagrams are evaluated for requirement fulfillment, class cohesion,
coupling, and code smells (Large Class, Duplicate Code, Baixa Coesão).
There is no single correct architecture — the game rewards structures that
adapt well to future, not-yet-seen requirements.

Methods carry an invisible "Category" (e.g. Usuario, Emprestimo,
Notificacao) used only by the evaluator to judge cohesion — players never
see categories, only method names. Class names are freely chosen by the
player and are NOT used in scoring.

ARCHITECTURE RULE (non-negotiable)
The codebase is split into two strict layers:
1. Assets/Scripts/Core — pure C#. NO `using UnityEngine`, NO MonoBehaviour,
   NO Unity API calls of any kind. Fully unit-testable outside Play Mode.
   This is the "game engine": turns, requirements, diagram, evaluation,
   refactor/lock logic all live here.
2. Assets/Scripts/Managers, UI, Multiplayer — thin MonoBehaviours. Their
   only job is to call into Core, receive Unity input/events, and update
   visuals. They must not contain game logic or scoring rules themselves.

If you (the AI) are asked to write something in Core, refuse to add any
Unity dependency, even a convenient one, and say so if the request would
require one.

CODE CONVENTIONS ESTABLISHED SO FAR
- Namespace root for Core classes: `Core.Diagram` (not `SysBuilders.Core.*`
  — this was tried and reverted; stay consistent with what's committed).
- Class naming avoids the `Game` prefix (e.g. GameClass, GameMethod) to
  prevent confusion with actual gameplay/manager classes. Current names:
  `ClassNode`, `MethodData`, `Relation`, `ClassDiagram`.
- Prefer auto-properties over manual getter/setter methods (C# idiom, not
  Java-style). Use `{ get; }` for immutable fields set only in the
  constructor, `{ get; private set; }` for fields that change only via an
  explicit method on the same class (e.g. IsLocked via Lock()).
- Expose internal lists as `IReadOnlyList<T>` backed by a private
  `List<T>` field, with explicit Add/Find methods — never expose a raw
  settable `List<T>` property.
- Tests live in Assets/Tests/EditMode, use Unity's built-in Test Framework
  (NUnit), follow Arrange/Act/Assert with descriptive Assert messages.
  Every new Core class should ship with at least one test covering a
  found/success case and, where relevant, a not-found/failure case.

DOMAIN MODEL BUILT SO FAR (Assets/Scripts/Core/Diagram)
- MethodData: Name, Category (both immutable).
- ClassNode: Name (immutable), Methods (IReadOnlyList<MethodData>),
  IsLocked (private set, flips via Lock()). AddMethod() is a no-op if
  locked — locking is enforced inside the class, not by callers
  remembering to check.
- Relation: Origin (ClassNode), Destination (ClassNode), Type
  (RelationType enum — currently only Association; Inheritance/
  Composition/Aggregation are planned Fase 2 additions, don't build
  them yet, but don't design against ever adding them either).
- ClassDiagram: owns the master lists of ClassNode and Relation.
  ClassNode does NOT hold its own relations — ClassDiagram is the single
  source of truth for relations, to avoid two places that can go out
  of sync.

NOT BUILT YET (do not assume these exist)
- Requirement (data: description, list of required MethodData names)
- TurnManager / Turn state (turn number, requirements, available methods,
  current player, turn end -> lock diagram)
- EvaluationManager (pure Core module: takes ClassDiagram + List<Requirement>,
  returns score + detected code smells + feedback text; never mutates
  the diagram itself, read-only analysis only)
- RefactorManager / refactor point cost logic (modifying a locked class
  costs a refactor point)
- MonoBehaviour layer: GameManager, TurnManager (Unity-facing wrapper),
  RequirementManager, DiagramManager, EvaluationManager (Unity-facing
  wrapper), MultiplayerManager, UIManager — per the planned architecture:
  GameManager only coordinates, it does not itself compute scores or
  create classes.
- Multiplayer: intentionally "dumb" — both players play independent
  solo turns, server only syncs at turn-end ("Jogador A finalizou ->
  Jogador B finalizou -> avança turno"). No real-time interaction.

FASE 1 (CORE, current focus) vs FASE 2 (LATER, do not build yet)
Fase 1: turns, requirements, classes, methods, relations, evaluation,
refactor/lock, basic multiplayer sync, core screens (menu, turn, results).
Fase 2 (explicitly deferred): design patterns (Observer, Strategy), special
abilities (Espionar/spy on opponent, Adicionar Smell/sabotage), animations,
sound, more themes, more relation types, more code smells, balancing.

WHEN HELPING WITH THIS PROJECT
- Ask which layer (Core vs Unity-facing) a task belongs to before writing
  code, if it's ambiguous.
- Favor small, single-responsibility classes over large ones — the whole
  point of the project is teaching this discipline, so the codebase
  should model it.
- If a request would blur Core/Unity separation (e.g. "just make the
  MonoBehaviour also calculate the score"), push back and suggest the
  Core-first alternative instead of complying silently.
- Prefer writing or updating a test alongside any new Core logic.