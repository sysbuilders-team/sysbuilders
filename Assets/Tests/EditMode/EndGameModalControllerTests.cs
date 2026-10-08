using System;
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

            // Placar oficial, exposto explicitamente como no MatchSession (nao e a soma do historico).
            public int PlayerOneScore { get; set; }
            public int PlayerTwoScore { get; set; }

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
            _endContext.PlayerOneScore = 50;
            _endContext.PlayerTwoScore = 20;

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
            _endContext.PlayerOneScore = 10;
            _endContext.PlayerTwoScore = 50;

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
        public void ShowModal_UsesOfficialScore_InsteadOfSummingHistory()
        {
            // Historico de RawScore acumulado (100 -> 200) com penalidade no ultimo turno:
            // somar TotalScore daria 300 e 150, mas o placar oficial e 170 e 100.
            _endContext.P1History.Add(new EvaluationResult(100, 0, new List<string>(), new List<SmellOccurrence>()));
            _endContext.P1History.Add(new EvaluationResult(200, 30, new List<string>(), new List<SmellOccurrence>()));
            _endContext.P2History.Add(new EvaluationResult(50, 0, new List<string>(), new List<SmellOccurrence>()));
            _endContext.P2History.Add(new EvaluationResult(100, 0, new List<string>(), new List<SmellOccurrence>()));
            _endContext.PlayerOneScore = 170;
            _endContext.PlayerTwoScore = 100;

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("Sua Pontuação: 170 pts", _finalScoreboardText.text);
            StringAssert.Contains("Adversário: 100 pts", _finalScoreboardText.text);
            StringAssert.Contains("VITÓRIA!", _matchResultText.text);
        }

        [Test]
        public void ShowModal_WhenScoresAreEqual_ShowsEmpate()
        {
            _endContext.PlayerOneScore = 100;
            _endContext.PlayerTwoScore = 100;
            _endContext.IsDraw = false;   // contexto inconsistente: o empate pelos numeros deve prevalecer

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("EMPATE!", _matchResultText.text);
        }

        [Test]
        public void ShowModal_WhenLocalSlotIsPlayerTwo_ShowsVitoriaWithPlayerTwoScore()
        {
            _endContext.PlayerOneScore = 80;
            _endContext.PlayerTwoScore = 120;

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerTwo);
            _controller.ShowModal();

            StringAssert.Contains("VITÓRIA!", _matchResultText.text);
            StringAssert.Contains("Sua Pontuação: 120 pts", _finalScoreboardText.text);
            StringAssert.Contains("Adversário: 80 pts", _finalScoreboardText.text);
        }

        [Test]
        public void MainMenuButtonClicked_InvokesNavigationAction()
        {
            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _mainMenuBtn.onClick.Invoke();

            Assert.IsTrue(_navActions.ReturnToMainMenuCalled);
        }

        [Test]
        public void ShowModal_WhenSameSmellPersistsAcrossTurns_CountsItOnce()
        {
            // O detector roda sobre o diagrama inteiro a cada turno: o mesmo LargeClass nunca
            // corrigido aparece no DetectedSmells de todos os turnos e nao pode virar "Ocorrencias: 3".
            var persistentSmell = new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { "GerenciadorTudo" });

            for (int turn = 0; turn < 3; turn++)
            {
                _endContext.P1History.Add(new EvaluationResult((turn + 1) * 100, 30, new List<string>(), new List<SmellOccurrence> { persistentSmell }));
            }
            _endContext.P2History.Add(new EvaluationResult(50, 0, new List<string>(), new List<SmellOccurrence> { persistentSmell }));
            _endContext.PlayerOneScore = 170;
            _endContext.PlayerTwoScore = 50;

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("(Ocorrências: 1)", _playerSmellsText.text);
            Assert.AreEqual(1, CountOccurrences(_playerSmellsText.text, "GerenciadorTudo"),
                $"a classe nao deve se repetir no relatorio:\n{_playerSmellsText.text}");

            StringAssert.Contains("(Ocorrências: 1)", _opponentSmellsText.text);
            Assert.AreEqual(1, CountOccurrences(_opponentSmellsText.text, "GerenciadorTudo"),
                $"a classe nao deve se repetir no relatorio do adversario:\n{_opponentSmellsText.text}");
        }

        [Test]
        public void ShowModal_WhenSmellWasFixedInLastTurn_DoesNotReportIt()
        {
            var smell = new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { "GerenciadorTudo" });
            _endContext.P1History.Add(new EvaluationResult(100, 30, new List<string>(), new List<SmellOccurrence> { smell }));
            _endContext.P1History.Add(new EvaluationResult(200, 0, new List<string>(), new List<SmellOccurrence>()));

            _controller.Initialize(_endContext, _navActions, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            StringAssert.Contains("Arquitetura Limpa", _playerSmellsText.text);
            StringAssert.Contains("estado final", _playerSmellsText.text);
        }

        private static int CountOccurrences(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;

            int count = 0;
            int index = text.IndexOf(value, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
            }

            return count;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
