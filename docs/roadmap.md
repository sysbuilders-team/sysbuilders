# SysBuilders — Arquitetura e Roadmap

## 1. Visão Geral das Camadas

```
┌────────────────────────────────────────────────────────────────────────┐
│                        UI / Controllers                                │
│ (TopMenu, RightBar, Toolbox, Diagram, EvaluationEndTurn, EndGameModal) │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (Consome apenas Interfaces restritas)
┌───────────────────────────────────▼────────────────────────────────────┐
│                    Managers (Unity MonoBehaviour)                      │
│   • GameManager (Orquestrador local da GameScene, injeta interfaces)   │
│   • RequirementManager (TextAsset JSON -> Loader -> Catalog)           │
│   • MenuManager (Lobby LAN simples, sem conhecimento de tema/regras)   │
└───────────────────┬────────────────────────────────┬───────────────────┘
                    │                                │
┌───────────────────▼────────────────────────┐ ┌─────▼───────────────────┐
│        Core (C# Puro, Zero Unity)          │ │  Network (Mirror LAN)   │
│ • Core.Diagram (Classes, nós, relações)    │ │ • NetworkDiscovery      │
│ • Core.Requirements (Loader, Catalog)      │ │ • NetworkRoomManager    │
│ • Core.Evaluation (Engine, Smells, Regras) │ │ • MatchSyncBridge       │
│ • Core.Session (MatchSession, TurnManager) │ │   (Troca só primitivos) │
└────────────────────────────────────────────┘ └─────────────────────────┘
```

---

## 2. Detalhamento por Camada

### Core (C# Puro — Zero dependência de Unity ou Mirror)
* **Core.Diagram**: `ClassNode`, `MethodData`, `Relation`, `ClassDiagram`, `IReadOnlyClassDiagram`.
* **Core.Requirements**:
  * `Requirement`: Representa um requisito funcional com ID, descrição e lista de `MethodData`.
  * `Trail`: Sequência de 5 requisitos de um tema.
  * `RequirementLoader`: Utilitário estático para parsing e validação de JSON (`LoadFromJson`).
  * `RequirementCatalog`: Repositório em memória com todas as trilhas agrupadas por tema. Responsável por sortear ou buscar trilhas (`PickRandomTrail`, `GetTrailById`) sem persistência de histórico na Fase 1.
* **Core.Evaluation**: `IEvaluationService`, `EvaluationEngine`, `EvaluationResult`, `CodeSmellDetector`, `SmellPenaltyCalculator`, `RequirementChecker`. Depende apenas de `Core.Diagram` e `Core.Requirements`.
* **Core.Session**:
  * `PlayerState`: Diagrama e histórico de pontuação/refatoração do jogador.
  * `RefactorHandler`: Regras e deduções de pontos ao destravar classes bloqueadas.
  * `TurnManager`: Ciclo de 5 turnos e máquina de estados de fases (`Show` -> `Build` -> `Evaluate` -> `Completed`), rastreando prontidão de ambos os jogadores (`PlayerReadinessTracker`).
  * `MatchSession`: Dono da partida local. Orquestra jogadores, turnos, avaliação automática na transição de fase e expõe interfaces de contexto para a UI (`IRequirementContext`, `ITurnContext`, `IScoreContext`, etc.).

---

### Managers (Unity-facing — Orquestração Local na GameScene)
* **`RequirementManager`**:
  * MonoBehaviour que referencia o arquivo JSON (`[SerializeField] TextAsset _requirementsJson`).
  * No `Awake()`, usa `RequirementLoader.LoadFromJson()` e instancia o `RequirementCatalog`.
  * Expõe métodos de consulta e sorteio para o `GameManager`.
* **`GameManager`**:
  * MonoBehaviour local existente na hierarquia da `GameScene.unity`. Não sabe de rede.
  * Cria e possui a instância da `MatchSession` local.
  * Distribui interfaces segregadas (ISP) para cada um dos controladores de UI da cena.
* **`MenuManager` (Lobby / MenuScene)**:
  * Gerencia a navegação inicial, escolha de nomes e descoberta/criação de salas LAN.
  * **Não possui `GameManager` nem `MatchSession`**. Não carrega temas nem requisitos. Mantém a `MenuScene` leve e focada apenas na conexão dos dois jogadores.

---

### Network (Mirror — Sincronização LAN estilo Minecraft)
* **Regra de Ouro**: O Mirror transita **estritamente dados primitivos ou serializáveis básicos** (`int`, `string`, `bool`), **nunca** instâncias de tipos do Core.
* **`NetworkDiscovery` (LAN estilo Minecraft)**:
  * O Host clica em "Criar Sala" (`StartHost()` + `AdvertiseServer()`).
  * O Cliente clica em "Buscar Partidas" (`StartDiscovery()`), escuta pacotes broadcast na rede local e conecta diretamente via IP local.
* **`NetworkRoomManager`**: Gerencia a sala inicial e executa `ServerChangeScene("GameScene")` quando ambos os jogadores confirmam prontidão no lobby.
* **`MatchSyncBridge` (`NetworkBehaviour`)**:
  * Componente de rede presente na `GameScene`.
  * Faz a ponte entre a rede e a `MatchSession` local:
    1. **Sorteio da Trilha**: O Host sorteia tema e `trailId` via `RequirementCatalog` e sincroniza essa informação com o cliente na largada da partida.
    2. **Prontidão de Fases**: Quando um jogador finaliza o turno (ou clica para avançar na modal), envia `[Command] CmdSetReady(slot)`. Quando ambos estiverem prontos no servidor, este dispara `[ClientRpc] RpcAdvancePhase()`.
    3. **Sincronização de Placar**: Ao avaliar o turno, envia os valores numéricos (`pointsEarned`, `penaltyPoints`) para serem injetados no estado do oponente na máquina remota.

---

## 3. Matriz de Controllers da UI e Interfaces

| Controller / Componente UI | O que faz na tela | Interfaces Recebidas |
| :--- | :--- | :--- |
| **`TopMenuController`** | Exibe o turno atual e total (ex.: Turno 1/5), a fase ativa (Fase: Construção), a descrição completa do requisito funcional do turno e o cronômetro regressivo da rodada. | `ITurnContext`<br>`IRequirementContext` |
| **`RightBarController`** | Exibe o placar comparativo dos dois jogadores com seus nomes e pontuações atuais (Jogador vs Oponente); mostra o contador de Pontos de Refatoração disponíveis com o botão "Refatorar Classe"; e contém o botão "Finalizar Turno" na parte inferior. | `IScoreContext`<br>`IRefactorContext`<br>`ITurnPlayerActions` |
| **`ToolboxController`** | Painel lateral esquerdo de ações e elementos: inclui o botão e limite de "Criar Classe (X/X)" com sua lista de cards de métodos do turno, além da seção inferior com lista de conexões e o botão "Criar Associação". | `IRequirementContext`<br>`IDiagramActions`<br>`ITurnContext` |
| **`DiagramController`** | Canvas central com grid interativo onde são renderizadas e manipuladas as caixas de classes UML (cabeçalho e métodos) e as linhas de associação entre elas. | `IReadOnlyClassDiagram`<br>`IDiagramActions` |
| **`EvaluationEndTurnController`** | Modal exibido ao fim do turno (ou esgotamento do tempo). Apresenta o resumo avaliativo da rodada com os pontos ganhos, apenas quanto perdeu sem detalhes (sem citar os nomes dos smells para não entregar a resposta, apenas pontuação e penalidade total) e o botão para avançar ao próximo turno. | `ITurnSummaryContext`<br>`ITurnPlayerActions`<br>`ITurnContext` |
| **`EndGameModalController`** | Modal final exibido ao término do 5º turno. Mostra o balanço final da partida, o placar consolidado, a lista detalhada de code/design smells acumulados de ambos os lados e o resultado final (vitória/derrota). | `IMatchEndContext`<br>`ISceneNavigationActions` |

---

## 4. Contratos das Interfaces de UI (Segregação de Interfaces)

```csharp
// Contexto do Requisito Ativo (Fornecido pela MatchSession)
public interface IRequirementContext
{
    Requirement CurrentRequirement { get; }
    IReadOnlyList<Requirement> RequirementsHistory { get; }
    event Action<Requirement> OnRequirementChanged;
}

// Contexto do Turno e Tempo
public interface ITurnContext
{
    int CurrentTurn { get; }
    int MaxTurns { get; }
    TurnPhase CurrentPhase { get; }
    float RemainingTurnTime { get; }
    event Action<TurnPhase> OnPhaseChanged;
    event Action<float> OnTimerTick;
}

// Ações de Jogador no Turno (Prontidão)
public interface ITurnPlayerActions
{
    SetPlayerReadyResult SetPlayerReady(PlayerSlot player);
}

// Placar e Estado dos Jogadores
public interface IScoreContext
{
    string PlayerOneName { get; }
    string PlayerTwoName { get; }
    int PlayerOneScore { get; }
    int PlayerTwoScore { get; }
    event Action OnScoreUpdated;
}

// Pontos e Ações de Refatoração
public interface IRefactorContext
{
    int AvailableRefactorPoints { get; }
    int RefactorCost { get; }
    bool CanRefactor(ClassNode node);
    bool TryRefactorClass(ClassNode node);
    event Action<int> OnRefactorPointsChanged;
}

// Resumo Numérico da Rodada (Modal Fim de Turno)
public interface ITurnSummaryContext
{
    int RoundPointsEarned { get; }
    int RoundPenaltyPoints { get; }
    int RoundOpponentPointsEarned { get; }
    int RoundOpponentPenaltyPoints { get; }
}

// Relatório Consolidado (Modal Fim de Jogo)
public interface IMatchEndContext
{
    IReadOnlyList<EvaluationResult> PlayerOneHistory { get; }
    IReadOnlyList<EvaluationResult> PlayerTwoHistory { get; }
    string WinnerName { get; }
    bool IsDraw { get; }
}
```

---

## 5. Sincronização de Modais e Fluxo entre Turnos

Para garantir que um jogador não avance para o turno seguinte antes do oponente terminar de ler seu feedback avaliativo:

1. **Entrada na Modal**: Ao concluir a fase `TurnPhase.Evaluate`, o `TurnManager` transiciona para `TurnPhase.Show`. A modal `EvaluationEndTurnController` abre para ambos os jogadores.
2. **Clique do Jogador**: O jogador clica no botão *"Avançar para o Próximo Turno"*.
   - A modal chama `ITurnPlayerActions.SetPlayerReady(meuSlot)`.
   - O botão fica desabilitado e exibe o estado: *"Aguardando adversário..."*.
   - O `MatchSyncBridge` transmite via rede o `CmdSetReady(meuSlot)`.
3. **Sincronismo via Mirror**: Quando o segundo jogador também clica em avançar, o servidor detecta prontidão dupla e dispara `RpcAdvancePhase()`.
4. **Fechamento e Novo Turno**: O `TurnManager` em ambas as máquinas avança para o próximo turno na fase `TurnPhase.Build`. O evento `OnPhaseChanged` fecha automaticamente a modal e destrava o painel de construção (`ToolboxController`).