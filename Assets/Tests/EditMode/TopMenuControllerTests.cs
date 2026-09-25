using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    public class TopMenuControllerTests
    {
        private class FakeRequirementContext : IRequirementContext
        {
            public Requirement CurrentRequirement { get; set; }
            public IReadOnlyList<Requirement> RequirementsHistory => new List<Requirement>();
            public event Action<Requirement> OnRequirementChanged;

            public void TriggerRequirementChanged(Requirement req)
            {
                CurrentRequirement = req;
                OnRequirementChanged?.Invoke(req);
            }
        }

        private GameObject _go;
        private TopMenuController _controller;
        private TextMeshProUGUI _turnInfoText;
        private TextMeshProUGUI _requirementText;
        private TextMeshProUGUI _timerText;
        private TurnManager _turnManager;
        private FakeRequirementContext _requirementContext;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TopMenuTest");
            _controller = _go.AddComponent<TopMenuController>();

            var turnInfoGo = new GameObject("TurnInfo");
            turnInfoGo.transform.SetParent(_go.transform);
            _turnInfoText = turnInfoGo.AddComponent<TextMeshProUGUI>();

            var reqGo = new GameObject("Requirement");
            reqGo.transform.SetParent(_go.transform);
            _requirementText = reqGo.AddComponent<TextMeshProUGUI>();

            var timerGo = new GameObject("Timer");
            timerGo.transform.SetParent(_go.transform);
            _timerText = timerGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(_controller, "_turnInfoText", _turnInfoText);
            SetPrivateField(_controller, "_requirementText", _requirementText);
            SetPrivateField(_controller, "_timerText", _timerText);

            _turnManager = new TurnManager();
            _requirementContext = new FakeRequirementContext
            {
                CurrentRequirement = new Requirement("req_1", "Cadastro de Livro", new List<MethodData>())
            };
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
        public void Initialize_DisplaysInitialTurnAndRequirement()
        {
            _controller.Initialize(_turnManager, _requirementContext);

            StringAssert.Contains("Turno 1", _turnInfoText.text);
            StringAssert.Contains("Apresentação", _turnInfoText.text);
            StringAssert.Contains("Cadastro de Livro", _requirementText.text);
        }

        [Test]
        public void OnPhaseChange_UpdatesPhaseInTurnInfo()
        {
            _controller.Initialize(_turnManager, _requirementContext);

            _turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            _turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);

            Assert.AreEqual(TurnPhase.Build, _turnManager.CurrentPhase);
            StringAssert.Contains("Construção", _turnInfoText.text);
        }

        [Test]
        public void OnRequirementChanged_UpdatesRequirementDescription()
        {
            _controller.Initialize(_turnManager, _requirementContext);

            var newReq = new Requirement("req_2", "Empréstimo de Livro", new List<MethodData>());
            _requirementContext.TriggerRequirementChanged(newReq);

            StringAssert.Contains("Empréstimo de Livro", _requirementText.text);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
