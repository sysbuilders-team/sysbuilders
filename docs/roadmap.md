Core (C# puro, zero Unity/Mirror)
├─ Core.Diagram: ClassNode, MethodData, ClassDiagram, IReadOnlyClassDiagram
├─ Core.Evaluation: IEvaluationService, EvaluationResult, Requirement
│                    → depende só de Core.Diagram
└─ Core.Session: PlayerState, RefactorHandler/RefactorRules, TurnManager,
ITurnPlayerActions, MatchSession, (futuro) IRequirementProvider
→ depende de Core.Diagram e Core.Evaluation
→ MatchSession expõe evento OnEvaluationCompleted(resultOne, resultTwo)

Managers / UI (Unity-facing, conhece Core só via interface)
├─ GameManager               → dono de um MatchSession LOCAL completo (não sabe de rede)
│                               nasce na GameScene, distribui interfaces pros controllers
├─ TopMenuController         → ITurnPlayerActions (+ TurnManager.OnPhaseChange)
├─ RightBarController        → interface de EvaluationResult/refactor (não MatchSession cru)
├─ ToolboxController         → interface de criação de classe/relação
├─ DiagramController         → IReadOnlyClassDiagram (só o PRÓPRIO diagrama do jogador)
├─ EvaluationManager         → assina MatchSession.OnEvaluationCompleted
│                               [PENDENTE: hoje depende de MatchSession concreto,
│                               deveria depender de interface restrita]
└─ MenuSceneController       → não depende de Core; só PlayerPrefs/SceneManager

Network (Mirror — só troca dado primitivo/serializável, nunca tipo de Core)
├─ NetworkManager            → conexão, NetworkDiscovery (LAN), scene transition
├─ RoomManager (NetworkRoomManager) → coleta histórico de trilha (PlayerPrefs) via rede,
│                               decide trilha, entrega (tema, trilha) pro GameManager
│                               via Initialize(...)
└─ MatchSyncBridge (NetworkBehaviour, nome provisório)
→ ÚNICA peça que sincroniza entre os dois lados:
fase de turno (quando ambos prontos) + EvaluationResult
próprio → manda pro rival → injeta no PlayerState do
oponente via AddEvaluationResult; também repassa
nome do jogador (SyncVar, via RoomPlayer)


Controller / Componente UI	Interfaces Recebidas	O que faz na tela
TopMenuController	ITurnContext	Mostra número do turno atual (1 a 5) e a fase atual.
RightBarController	IEvaluationContext, ITurnContext	Mostra os pontos acumulados e contém o botão "Pronto".
ToolboxController	IRequirementContext, IPlayerSessionActions	Lista os cards de métodos do turno, botão de nova classe e refatoração.
DiagramController	IReadOnlyClassDiagram	Renderiza visualmente as caixas de classes e as linhas.
EndGameModalController	IEvaluationContext	Abre ao final do jogo e renderiza a lista completa de smells e o balanço final de pontos.
EvaluationEndTurnController	ITurnSummaryContext, ITurnContext	Modal/painel exibido ao fim do turno. Mostra o placar comparativo (Jogador vs Adversário): pontos ganhos no turno, penalidades por má qualidade (sem listar os nomes dos smells) e pontuação acumulada. Contém o botão de avançar para a próxima rodada.