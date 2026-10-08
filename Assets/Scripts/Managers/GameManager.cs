using System;
using System.Collections;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Session;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    public class GameManager : MonoBehaviour, ISceneNavigationActions
    {
        [Header("Manager da Scene")]
        [SerializeField] private RequirementManager _requirementManager;

        [Header("UI Controllers")]
        [SerializeField] private TopMenuController _topMenu;
        [SerializeField] private RightBarController _rightBar;
        [SerializeField] private ToolboxController _toolBox;
        [SerializeField] private DiagramController _diagram;
        [SerializeField] private EvaluationEndTurnController _evaluationModal;
        [SerializeField] private EndGameModalController _endGameModal;

        [Header("Navegação")]
        [SerializeField] private string _menuSceneName = "Menu";

#if UNITY_EDITOR
        [Header("Editor Quick Test (Offline)")]
        [Tooltip("Quando ativado, inicia uma partida local automaticamente ao dar Play na GameScene se nenhuma partida online foi iniciada.")]
        [SerializeField] private bool _autoStartInEditor = true;
        [SerializeField] private string _editorTestTheme = "Biblioteca";
        [SerializeField] private string _editorTestTrailId = "trail_biblioteca_01";

        private bool _isEditorOfflineTest;
#endif

        public MatchSession Session { get; private set; }
        public bool RequireOpponentResult { get; set; }

        public event Action OnLocalPlayerReady;
        public event Action OnReturnToMenuRequested;

        private bool _localEvaluated;
        private bool _opponentEvaluated;
        private bool _advanceScheduled;

        //TODO: passar nome dos players por parametro
        public void Initialize(string theme, string trailId, PlayerSlot localSlot = PlayerSlot.PlayerOne)
        {
            StartMatch(theme, trailId, localSlot);
        }

        public void StartMatch(string theme, string trailId, PlayerSlot localSlot)
        {
            if (Session != null)
            {
                Debug.LogWarning("[GameManager] StartMatch ignorado: a partida já foi iniciada.");
                return;
            }

            var requirements = _requirementManager.GetTrailById(theme, trailId);
            Session = new MatchSession("Jogador 1", "Jogador 2", requirements, localSlot, isNetworkMatch: RequireOpponentResult);
            
            Session.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
            Session.TurnManager.OnPhaseChange += HandlePhaseChange;

            InitializeUI(localSlot);
            WireUI();
            
            Session.TurnManager.ForceAdvancePhase();
        }

        private void InitializeUI(PlayerSlot localSlot)
        {
            var turnManager = Session.TurnManager;

            // TODO: cronômetro -> passar um ITurnTimerContext ao TopMenuController quando existir.
            _topMenu.Initialize(
                turnContext: turnManager,
                requirementContext: Session
            );

            _rightBar.Initialize(
                scoreContext: Session,
                refactorContext: Session,
                turnPlayerActions: turnManager,
                localSlot: localSlot,
                turnContext: turnManager
            );

            _toolBox.Initialize(
                requirementContext: Session,
                diagramActions: Session,
                turnContext: turnManager
            );

            _diagram.Initialize(
                diagram: Session.LocalPlayer.Diagram,
                diagramActions: Session
            );

            _evaluationModal.Initialize(
                summaryContext: Session,
                turnPlayerActions: turnManager,
                turnContext: turnManager,
                localSlot: localSlot
            );

            _endGameModal.Initialize(
                endContext: Session,
                navigationActions: this,
                turnContext: turnManager,
                localSlot: localSlot
            );
        }

        private void WireUI()
        {
            _rightBar.OnEndTurnTriggered += HandleLocalReady;
            _evaluationModal.OnAdvanceClicked += HandleLocalReady;
            _rightBar.OnRefactorButtonClicked += HandleRefactorRequested;

            _toolBox.OnCreateClassClicked += HandleCreateClassRequested;
            _toolBox.OnAssociationModeToggled += _diagram.SetAssociationMode;
            _toolBox.OnMethodCardSelected += HandleMethodCardSelected;

            _diagram.OnAssociationCreated += HandleAssociationCreated;
        }

        private void UnwireUI()
        {
            if (_rightBar != null)
            {
                _rightBar.OnEndTurnTriggered -= HandleLocalReady;
                _rightBar.OnRefactorButtonClicked -= HandleRefactorRequested;
            }

            if (_evaluationModal != null)
            {
                _evaluationModal.OnAdvanceClicked -= HandleLocalReady;
            }

            if (_toolBox != null)
            {
                _toolBox.OnCreateClassClicked -= HandleCreateClassRequested;
                _toolBox.OnMethodCardSelected -= HandleMethodCardSelected;
                if (_diagram != null)
                {
                    _toolBox.OnAssociationModeToggled -= _diagram.SetAssociationMode;
                }
            }

            if (_diagram != null)
            {
                _diagram.OnAssociationCreated -= HandleAssociationCreated;
            }
        }

        private void HandleLocalReady()
        {
            OnLocalPlayerReady?.Invoke();
        }

        private void HandleCreateClassRequested()
        {
            if (!_toolBox.CanCreateClass()) return;

            if (_diagram.TryCreateClass(NextClassName()))
            {
                _toolBox.NotifyClassCreated();
            }
        }

        // TODO: o GDD prevê nome livre. ClassNode.Name é imutável e não há campo de nome
        // na criação, então por enquanto os nomes são gerados (não afetam a pontuação).
        private string NextClassName()
        {
            var existing = Session.LocalPlayer.Diagram.Classes;
            int n = existing.Count + 1;
            string name;
            do
            {
                name = $"Classe{n++}";
            } while (existing.Any(c => c.Name == name));

            return name;
        }

        private void HandleMethodCardSelected(MethodData method)
        {
            if (method == null || _diagram.IsAssociationMode) return;

            var selected = _diagram.SelectedNode;
            if (selected == null || selected.Node == null) return;
            if (selected.Node.Methods.Contains(method)) return;

            _diagram.TryAddMethodToClass(selected.Node, method);
        }

        private void HandleRefactorRequested()
        {
            var selected = _diagram.SelectedNode;
            if (selected == null || selected.Node == null) return;
            if (!Session.CanRefactor(selected.Node)) return;

            if (Session.TryRefactorClass(selected.Node))
            {
                _diagram.RenderDiagram(); 
            }
        }

        private void HandleAssociationCreated(Relation relation)
        {
            _toolBox.SetAssociationMode(false);
        }
        

        private void HandlePhaseChange(TurnPhase phase)
        {
            if (phase == TurnPhase.Evaluate)
            {
                _diagram.RenderDiagram(); 
            }
        }

        private void HandleLocalEvaluationCompleted(EvaluationResult result)
        {
            _localEvaluated = true;
            TryScheduleAdvance();
        }
        
        public void ReceiveOpponentEvaluation(int turnNumber, EvaluationResult result)
        {
            if (Session == null) return;

            if (!Session.ReceiveOpponentResult(turnNumber, result))
            {
                Debug.LogWarning($"[GameManager] Resultado do oponente ignorado (turno {turnNumber} fora de ordem ou duplicado).");
                return;
            }

            _opponentEvaluated = true;
        }

        private void TryScheduleAdvance()
        {
            if (_advanceScheduled || !_localEvaluated) return;
            if (RequireOpponentResult) return; // Em partidas em rede, a autoridade de avanço de fase é do servidor via MatchSyncBridge

            _advanceScheduled = true;
            StartCoroutine(AdvanceAfterEvaluation());
        }

        private IEnumerator AdvanceAfterEvaluation()
        {
            yield return null;

            _localEvaluated = false;
            _opponentEvaluated = false;
            _advanceScheduled = false;

            if (Session != null && Session.TurnManager.CurrentPhase == TurnPhase.Evaluate)
            {
                Session.TurnManager.ForceAdvancePhase();
            }
        }

        public void ReturnToMainMenu()
        {
            if (OnReturnToMenuRequested != null)
            {
                OnReturnToMenuRequested.Invoke(); 
            }
            else
            {
                SceneManager.LoadScene(_menuSceneName);
            }
        }

        public void RestartMatch()
        {
            if (RequireOpponentResult)
            {
                ReturnToMainMenu();
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

#if UNITY_EDITOR
        private void Start()
        {
            // Se já foi iniciado (ex: MatchSyncBridge chamou StartMatch no Awake/OnStartServer), não faz nada.
            if (Session != null) return;

            if (_autoStartInEditor)
            {
                Debug.Log("[GameManager] Modo de Teste Offline no Editor iniciado.");
                _isEditorOfflineTest = true;
                StartMatch(_editorTestTheme, _editorTestTrailId, PlayerSlot.PlayerOne);

                if (Session != null)
                {
                    Session.OnEvaluationCompleted += HandleEditorOfflineEvaluationCompleted;

                    // Avança da fase Show para Build para permitir teste imediato da diagramação no Turno 1
                    if (Session.TurnManager.CurrentPhase == TurnPhase.Show)
                    {
                        Session.TurnManager.ForceAdvancePhase();
                    }
                }

                // Em modo offline de teste no editor, simula prontidão do Player 2 para permitir fluxo de turnos
                if (_rightBar != null)
                {
                    _rightBar.OnEndTurnTriggered += HandleEditorOfflinePlayerReady;
                }
                if (_evaluationModal != null)
                {
                    _evaluationModal.OnAdvanceClicked += HandleEditorOfflinePlayerReady;
                }
            }
        }

        private void HandleEditorOfflinePlayerReady()
        {
            if (!_isEditorOfflineTest || Session == null) return;
            // No modo teste local, marca o Player 2 pronto para que o TurnManager avance de fase sem rede
            Session.SetPlayerReady(PlayerSlot.PlayerTwo);
        }

        private void HandleEditorOfflineEvaluationCompleted(Core.Evaluation.EvaluationResult result)
        {
            if (!_isEditorOfflineTest || Session == null) return;
            // Após a avaliação do jogador local, avança automaticamente de Evaluate para Show para abrir a modal avaliativa
            if (Session.TurnManager.CurrentPhase == TurnPhase.Evaluate)
            {
                Session.TurnManager.ForceAdvancePhase();
            }
        }

        private void OnDestroy()
        {
            if (_isEditorOfflineTest)
            {
                if (_rightBar != null)
                {
                    _rightBar.OnEndTurnTriggered -= HandleEditorOfflinePlayerReady;
                }
                if (_evaluationModal != null)
                {
                    _evaluationModal.OnAdvanceClicked -= HandleEditorOfflinePlayerReady;
                }
                if (Session != null)
                {
                    Session.OnEvaluationCompleted -= HandleEditorOfflineEvaluationCompleted;
                }
            }
        }
#endif
    }
}