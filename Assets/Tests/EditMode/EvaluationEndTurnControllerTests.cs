using System.Reflection;
using Core.Session;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode
{
    public class EvaluationEndTurnControllerTests
    {
        private class FakeSummaryContext : ITurnSummaryContext
        {
            public int RoundPointsEarned { get; set; } = 40;
            public int RoundPenaltyPoints { get; set; } = 15;
            public int RoundOpponentPointsEarned { get; set; } = 30;
            public int RoundOpponentPenaltyPoints { get; set; } = 5;
            public int RoundDomainPoints { get; set; } = 0;
            public int PendingAssociationsCount { get; set; } = 0;
            public int AssociationsTotalCount { get; set; } = 0;
        }

        private GameObject _go;
        private GameObject _modalRoot;
        private EvaluationEndTurnController _controller;
        private TMP_Text _playerPointsText;
        private TMP_Text _playerPenaltiesText;
        private TMP_Text _playerTotalText;
        private TMP_Text _domainBonusText;
        private TMP_Text _pendingAssociationsText;
        private Button _advanceBtn;
        private TMP_Text _advanceBtnText;

        private FakeSummaryContext _summaryContext;
        private TurnManager _turnManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EvalModalTest");
            _modalRoot = new GameObject("ModalRoot");
            _modalRoot.transform.SetParent(_go.transform);

            _controller = _go.AddComponent<EvaluationEndTurnController>();

            var pPtsGo = new GameObject("PlayerPoints");
            pPtsGo.transform.SetParent(_modalRoot.transform);
            _playerPointsText = pPtsGo.AddComponent<TextMeshProUGUI>();

            var pPenGo = new GameObject("PlayerPenalties");
            pPenGo.transform.SetParent(_modalRoot.transform);
            _playerPenaltiesText = pPenGo.AddComponent<TextMeshProUGUI>();

            var pTotGo = new GameObject("PlayerTotal");
            pTotGo.transform.SetParent(_modalRoot.transform);
            _playerTotalText = pTotGo.AddComponent<TextMeshProUGUI>();

            var advBtnGo = new GameObject("AdvanceBtn");
            advBtnGo.transform.SetParent(_modalRoot.transform);
            _advanceBtn = advBtnGo.AddComponent<Button>();

            var advBtnTextGo = new GameObject("AdvanceBtnText");
            advBtnTextGo.transform.SetParent(advBtnGo.transform);
            _advanceBtnText = advBtnTextGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(_controller, "_modalRoot", _modalRoot);
            SetPrivateField(_controller, "_playerPointsEarnedText", _playerPointsText);
            SetPrivateField(_controller, "_playerPenaltiesText", _playerPenaltiesText);
            SetPrivateField(_controller, "_playerTotalText", _playerTotalText);

            var domainBonusGo = new GameObject("DomainBonus");
            domainBonusGo.transform.SetParent(_modalRoot.transform);
            _domainBonusText = domainBonusGo.AddComponent<TextMeshProUGUI>();

            var pendingAssocGo = new GameObject("PendingAssociations");
            pendingAssocGo.transform.SetParent(_modalRoot.transform);
            _pendingAssociationsText = pendingAssocGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(_controller, "_domainSectionRoot", domainBonusGo);
            SetPrivateField(_controller, "_domainBonusText", _domainBonusText);
            SetPrivateField(_controller, "_pendingAssociationsText", _pendingAssociationsText);
            SetPrivateField(_controller, "_advanceButton", _advanceBtn);
            SetPrivateField(_controller, "_advanceButtonText", _advanceBtnText);

            _summaryContext = new FakeSummaryContext();
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
            _controller.Initialize(_summaryContext, _turnManager, _turnManager, PlayerSlot.PlayerOne);

            Assert.IsFalse(_controller.IsModalOpen);
        }

        [Test]
        public void ShowModal_DisplaysCalculatedRoundPoints()
        {
            _controller.Initialize(_summaryContext, _turnManager, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            Assert.IsTrue(_controller.IsModalOpen);
            StringAssert.Contains("+40", _playerPointsText.text);
            StringAssert.Contains("-15", _playerPenaltiesText.text);
            StringAssert.Contains("25 pts", _playerTotalText.text);
        }

        [Test]
        public void AdvanceButtonClicked_DisablesButtonAndSetsWaiting()
        {
            _controller.Initialize(_summaryContext, _turnManager, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();

            _advanceBtn.onClick.Invoke();

            Assert.IsFalse(_advanceBtn.interactable);
            StringAssert.Contains("Aguardando adversário...", _advanceBtnText.text);
        }

        [Test]
        public void PhaseChangedToBuild_AutomaticallyClosesModal()
        {
            _controller.Initialize(_summaryContext, _turnManager, _turnManager, PlayerSlot.PlayerOne);
            _controller.ShowModal();
            Assert.IsTrue(_controller.IsModalOpen);

            // Both players ready in Show phase advances to Build phase
            _turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            _turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);

            Assert.AreEqual(TurnPhase.Build, _turnManager.CurrentPhase);
            Assert.IsFalse(_controller.IsModalOpen);
        }

        [Test]
        public void ShowModal_WithDomainDeltaFormatsWithSignAndHidesWhenNoAssociations()
        {
            _controller.Initialize(_summaryContext, _turnManager, _turnManager, PlayerSlot.PlayerOne);

            _summaryContext.RoundDomainPoints = 25;
            _summaryContext.AssociationsTotalCount = 2;
            _summaryContext.PendingAssociationsCount = 1;
            _controller.ShowModal();
            StringAssert.Contains("+25", _domainBonusText.text);
            StringAssert.Contains("Pendentes: 1", _pendingAssociationsText.text);

            _summaryContext.RoundDomainPoints = -10;
            _controller.ShowModal();
            StringAssert.Contains("-10", _domainBonusText.text);

            _summaryContext.AssociationsTotalCount = 0;
            _controller.ShowModal();
            Assert.IsFalse(_pendingAssociationsText.gameObject.activeSelf);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
