using System.Collections.Generic;
using System.Reflection;
using Core.Evaluation;
using Core.Session;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    public class EndGameModalControllerTests
    {
        private class FakeMatchEndContext : IMatchEndContext
        {
            public List<EvaluationResult> P1History { get; } = new List<EvaluationResult>();
            public List<EvaluationResult> P2History { get; } = new List<EvaluationResult>();

            public IReadOnlyList<EvaluationResult> PlayerOneHistory => P1History;
            public IReadOnlyList<EvaluationResult> PlayerTwoHistory => P2History;

            public string WinnerName { get; set; } = "Alice";
            public bool IsDraw { get; set; } = false;
        }

        private class FakeNavigationActions : ISceneNavigationActions
        {
            public bool ReturnToMainMenuCalled { get; private set; }
            public bool RestartMatchCalled { get; private set; }

            public void ReturnToMainMenu() => ReturnToMainMenuCalled = true;
            public void RestartMatch() => RestartMatchCalled = true;
        }

        private GameObject _go;
        private GameObject _modalRoot;
        private EndGameModalController _controller;
        private TMP_Text _matchResultText;
        private TMP_Text _finalScoreboardText;
        private TMP_Text _playerSmellsText;
        private TMP_Text _opponentSmellsText;
        private Button _mainMenuBtn;
        private Button _restartBtn;

        private FakeMatchEndContext _endContext;
        private FakeNavigationActions _navActions;
        private TurnManager _turnManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EndGameModalTest");
            _modalRoot = new GameObject("ModalRoot");
            _modalRoot.transform.SetParent(_go.transform);

            _controller = _go.AddComponent<EndGameModalController>();

            var resultGo = new GameObject("ResultText");
            resultGo.transform.SetParent(_modalRoot.transform);
            _matchResultText = resultGo.AddComponent<TextMeshProUGUI>();

            var scoreGo = new GameObject("ScoreboardText");
            scoreGo.transform.SetParent(_modalRoot.transform);
            _finalScoreboardText = scoreGo.AddComponent<TextMeshProUGUI>();

            var pSmellsGo = new GameObject("PlayerSmells");
            pSmellsGo.transform.SetParent(_modalRoot.transform);
            _playerSmellsText = pSmellsGo.AddComponent<TextMeshProUGUI>();

            var oppSmellsGo = new GameObject("OppSmells");
            oppSmellsGo.transform.SetParent(_modalRoot.transform);
            _opponentSmellsText = oppSmellsGo.AddComponent<TextMeshProUGUI>();

            var menuBtnGo = new GameObject("MainMenuBtn");
            menuBtnGo.transform.SetParent(_modalRoot.transform);
            _mainMenuBtn = menuBtnGo.AddComponent<Button>();

            var restartBtnGo = new GameObject("RestartBtn");
            restartBtnGo.transform.SetParent(_modalRoot.transform);
            _restartBtn = restartBtnGo.AddComponent<Button>();

            SetPrivateField(_controller, "_modalRoot", _modalRoot);
            SetPrivateField(_controller, "_matchResultText", _matchResultText);
            SetPrivateField(_controller, "_finalScoreboardText", _finalScoreboardText);
            SetPrivateField(_controller, "_playerSmellsReportText", _playerSmellsText);
            SetPrivateField(_controller, "_opponentSmellsReportText", _opponentSmellsText);
            SetPrivateField(_controller, "_mainMenuButton", _mainMenuBtn);
            SetPrivateField(_controller, "_restartButton", _restartBtn);

            _endContext = new FakeMatchEndContext();
            _navActions = new FakeNavigationActions();
            _turnManager = new TurnManager();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Initialize_StartsClosed()
        {
            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);

            Assert.IsFalse(_controller.IsModalOpen);
        }

        [Test]
        public void ShowModal_WhenPlayerOneWins_ShowsVitoria()
        {
            _endContext.P1History.Add(new EvaluationResult(50, 0, new List<string>(), new List<SmellOccurrence>()));
            _endContext.P2History.Add(new EvaluationResult(20, 0, new List<string>(), new List<SmellOccurrence>()));

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            Assert.IsTrue(_controller.IsModalOpen);
            StringAssert.Contains("VITÓRIA!", _matchResultText.text);
            StringAssert.Contains("50 pts", _finalScoreboardText.text);
        }

        [Test]
        public void ShowModal_WhenPlayerOneLoses_ShowsDerrota()
        {
            _endContext.P1History.Add(new EvaluationResult(10, 0, new List<string>(), new List<SmellOccurrence>()));
            _endContext.P2History.Add(new EvaluationResult(50, 0, new List<string>(), new List<SmellOccurrence>()));

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("DERROTA!", _matchResultText.text);
        }

        [Test]
        public void ShowModal_WithSmells_FormatsReport()
        {
            var smell = new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { "GerenciadorTudo" });
            _endContext.P1History.Add(new EvaluationResult(30, 10, new List<string>(), new List<SmellOccurrence> { smell }));

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("Large Class", _playerSmellsText.text);
            StringAssert.Contains("GerenciadorTudo", _playerSmellsText.text);
        }

        [Test]
        public void MainMenuButtonClicked_InvokesNavigationAction()
        {
            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _mainMenuBtn.onClick.Invoke();

            Assert.IsTrue(_navActions.ReturnToMainMenuCalled);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
