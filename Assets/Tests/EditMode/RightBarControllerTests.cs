using System;
using System.Reflection;
using Core.Diagram;
using Core.Session;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    public class RightBarControllerTests
    {
        private class FakeScoreContext : IScoreContext
        {
            public string PlayerOneName { get; set; } = "Alice";
            public string PlayerTwoName { get; set; } = "Bob";
            public int PlayerOneScore { get; set; } = 10;
            public int PlayerTwoScore { get; set; } = 5;
            public event Action OnScoreUpdated;

            public void TriggerScoreUpdated() => OnScoreUpdated?.Invoke();
        }

        private class FakeRefactorContext : IRefactorContext
        {
            public int RefactorPoints { get; set; } = 2;
            public int RefactorCost { get; set; } = 1;
            public bool CanRefactor(ClassNode classNode) => RefactorPoints >= RefactorCost;
            public bool TryRefactorClass(ClassNode classNode) => true;
            public event Action<int> OnRefactorPointsChanged;

            public void TriggerRefactorChanged(int points)
            {
                RefactorPoints = points;
                OnRefactorPointsChanged?.Invoke(points);
            }
        }

        private GameObject _go;
        private RightBarController _controller;
        private TextMeshProUGUI _playerScoreText;
        private TextMeshProUGUI _enemyScoreText;
        private TextMeshProUGUI _refactorPointsText;
        private Button _refactorButton;
        private Button _endTurnButton;
        private TextMeshProUGUI _endTurnButtonText;

        private FakeScoreContext _scoreContext;
        private FakeRefactorContext _refactorContext;
        private TurnManager _turnManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RightBarTest");
            _controller = _go.AddComponent<RightBarController>();

            var pScoreGo = new GameObject("PlayerScore");
            pScoreGo.transform.SetParent(_go.transform);
            _playerScoreText = pScoreGo.AddComponent<TextMeshProUGUI>();

            var eScoreGo = new GameObject("EnemyScore");
            eScoreGo.transform.SetParent(_go.transform);
            _enemyScoreText = eScoreGo.AddComponent<TextMeshProUGUI>();

            var refactorTextGo = new GameObject("RefactorPoints");
            refactorTextGo.transform.SetParent(_go.transform);
            _refactorPointsText = refactorTextGo.AddComponent<TextMeshProUGUI>();

            var refactorBtnGo = new GameObject("RefactorBtn");
            refactorBtnGo.transform.SetParent(_go.transform);
            _refactorButton = refactorBtnGo.AddComponent<Button>();

            var endTurnBtnGo = new GameObject("EndTurnBtn");
            endTurnBtnGo.transform.SetParent(_go.transform);
            _endTurnButton = endTurnBtnGo.AddComponent<Button>();

            var endTurnTextGo = new GameObject("EndTurnText");
            endTurnTextGo.transform.SetParent(endTurnBtnGo.transform);
            _endTurnButtonText = endTurnTextGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(_controller, "_playerScoreText", _playerScoreText);
            SetPrivateField(_controller, "_enemyScoreText", _enemyScoreText);
            SetPrivateField(_controller, "_refactorPointsText", _refactorPointsText);
            SetPrivateField(_controller, "_refactorClassButton", _refactorButton);
            SetPrivateField(_controller, "_endTurnButton", _endTurnButton);
            SetPrivateField(_controller, "_endTurnButtonText", _endTurnButtonText);

            _scoreContext = new FakeScoreContext();
            _refactorContext = new FakeRefactorContext();
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
        public void Initialize_WhenPlayerOne_ShowsAliceAsPlayerAndBobAsEnemy()
        {
            _controller.Initialize(_scoreContext, _refactorContext, _turnManager, PlayerSlot.PlayerOne, _turnManager);

            StringAssert.Contains("Alice: 10 pts", _playerScoreText.text);
            StringAssert.Contains("Bob: 5 pts", _enemyScoreText.text);
        }

        [Test]
        public void Initialize_WhenPlayerTwo_ShowsBobAsPlayerAndAliceAsEnemy()
        {
            _controller.Initialize(_scoreContext, _refactorContext, _turnManager, PlayerSlot.PlayerTwo, _turnManager);

            StringAssert.Contains("Bob: 5 pts", _playerScoreText.text);
            StringAssert.Contains("Alice: 10 pts", _enemyScoreText.text);
        }

        [Test]
        public void RefactorPointsChanged_UpdatesTextAndButtonInteractable()
        {
            _controller.Initialize(_scoreContext, _refactorContext, _turnManager, PlayerSlot.PlayerOne, _turnManager);

            Assert.IsTrue(_refactorButton.interactable);

            _refactorContext.TriggerRefactorChanged(0);

            StringAssert.Contains("0", _refactorPointsText.text);
            Assert.IsFalse(_refactorButton.interactable);
        }

        [Test]
        public void EndTurnClicked_CallsSetPlayerReadyAndSetsWaitingState()
        {
            _controller.Initialize(_scoreContext, _refactorContext, _turnManager, PlayerSlot.PlayerOne, _turnManager);
                
            var awakeMethod = typeof(RightBarController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awakeMethod?.Invoke(_controller, null);

            _endTurnButton.onClick.Invoke();

            Assert.IsFalse(_endTurnButton.interactable);
            StringAssert.Contains("Aguardando adversário...", _endTurnButtonText.text);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
