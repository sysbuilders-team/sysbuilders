using System;
using Core.Session;
using Managers;
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

#if UNITY_EDITOR
        [Header("Editor Quick Test (Offline)")]
        [Tooltip("Quando ativado, inicia uma partida local automaticamente ao dar Play na GameScene se nenhuma partida online foi iniciada.")]
        [SerializeField] private bool _autoStartInEditor = true;
        [SerializeField] private string _editorTestTheme = "Biblioteca";
        [SerializeField] private string _editorTestTrailId = "trail_biblioteca_01";

        private bool _isEditorOfflineTest;
#endif

        public MatchSession Session { get; private set; }

        public event Action OnLocalPlayerReady;
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

            if (_requirementManager == null)
            {
                _requirementManager = GetComponent<RequirementManager>() ?? FindFirstObjectByType<RequirementManager>();
            }

            var requirements = _requirementManager.GetTrailById(theme, trailId);

            Session = new MatchSession("Jogador 1", "Jogador 2", requirements, localSlot);

            InitializeUI(localSlot);
        }

        private void InitializeUI(PlayerSlot localSlot)
        {
            var turnManager = Session.TurnManager;

            if (_topMenu != null)
            {
                _topMenu.Initialize(
                    turnContext: turnManager, 
                    requirementContext: Session
                );
            }

            if (_rightBar != null)
            {
                _rightBar.Initialize(
                    scoreContext: Session, 
                    refactorContext: Session, 
                    turnPlayerActions: turnManager, 
                    localSlot: localSlot, 
                    turnContext: turnManager
                );
            }

            if (_toolBox != null)
            {
                _toolBox.Initialize(
                    requirementContext: Session, 
                    diagramActions: Session, 
                    turnContext: turnManager
                );
            }

            if (_diagram != null)
            {
                _diagram.Initialize(
                    diagram: Session.LocalPlayer.Diagram, 
                    diagramActions: Session
                );
            }

            if (_evaluationModal != null)
            {
                _evaluationModal.Initialize(
                    summaryContext: Session, 
                    turnPlayerActions: turnManager, 
                    turnContext: turnManager, 
                    localSlot: localSlot
                );
            }

            if (_endGameModal != null)
            {
                _endGameModal.Initialize(
                    endContext: Session, 
                    navigationActions: this, 
                    turnContext: turnManager, 
                    localSlot: localSlot
                );
            }
        }
        public void ReturnToMainMenu()
        {
            if (Application.CanStreamedLevelBeLoaded("Menu"))
            {
                SceneManager.LoadScene("Menu");
            }
            else
            {
                SceneManager.LoadScene("MenuScene");
            }
        }

        public void RestartMatch()
        {
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