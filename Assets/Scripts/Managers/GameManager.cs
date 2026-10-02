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

        public MatchSession Session { get; private set; }

        public event Action OnLocalPlayerReady;
        //TODO: passar nome dos players por parametro
        public void Initialize(string theme, string trailId, PlayerSlot localSlot = PlayerSlot.PlayerOne)
        {
            StartMatch(theme, trailId, localSlot);
        }

        public void StartMatch(string theme, string trailId, PlayerSlot localSlot)
        {
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
    }
}