using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using Managers;
using NUnit.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tests.EditMode
{
    [TestFixture]
    public class MethodCardUIDragAndAdvanceTests
    {
        private class FakeScoreContext : IScoreContext
        {
            public string PlayerOneName => "P1";
            public string PlayerTwoName => "P2";
            public int PlayerOneScore => 0;
            public int PlayerTwoScore => 0;
            public event Action OnScoreUpdated { add { } remove { } }
        }

        private class FakeRefactorContext : IRefactorContext
        {
            public int RefactorPoints => 0;
            public int RefactorCost => 1;
            public bool CanRefactor(ClassNode classNode) => false;
            public bool TryRefactorClass(ClassNode classNode) => false;
            public bool CanRefactorSelected() => false;
            public bool TryRefactorSelectedClass() => false;
            public event Action<int> OnRefactorPointsChanged { add { } remove { } }
        }

        private class FakeTurnSummaryContext : ITurnSummaryContext
        {
            public int RoundPointsEarned => 10;
            public int RoundPenaltyPoints => 0;
            public int RoundOpponentPointsEarned => 5;
            public int RoundOpponentPenaltyPoints => 0;
            public int RoundDomainPoints => 0;
            public int PendingAssociationsCount => 0;
            public int AssociationsTotalCount => 0;
        }

        private class FakeRequirementContext : IRequirementContext
        {
            public Requirement CurrentRequirement { get; set; }
            public IReadOnlyList<Requirement> RequirementsHistory => Array.Empty<Requirement>();
            public bool HasPendingMethods { get; set; }
            public event Action<Requirement> OnRequirementChanged;

            public void NotifyRequirementChanged() => OnRequirementChanged?.Invoke(CurrentRequirement);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        #region End-Turn Pending Methods Gating & Modal Advance Tests
        [Test]
        public void EvaluationModalAdvance_FiresTurnManagerOnPlayerReady_EvenWhenRequirementHasPendingMethods()
        {
            var modalGo = new GameObject("EvalModalTest");
            var modal = modalGo.AddComponent<EvaluationEndTurnController>();

            var btnGo = new GameObject("AdvanceBtn");
            btnGo.transform.SetParent(modalGo.transform);
            var advanceBtn = btnGo.AddComponent<Button>();
            SetPrivateField(modal, "_advanceButton", advanceBtn);

            var turnManager = new TurnManager(requiredPlayers: 2);
            var summaryContext = new FakeTurnSummaryContext();

            modal.Initialize(summaryContext, turnManager, turnManager, PlayerSlot.PlayerOne);
            modal.ShowModal();

            bool localReadyFired = false;
            turnManager.OnPlayerReady += slot =>
            {
                if (slot == PlayerSlot.PlayerOne) localReadyFired = true;
            };

            advanceBtn.onClick.Invoke();

            Assert.IsTrue(localReadyFired, "Advancing from the evaluation modal must mark the player ready regardless of pending methods.");

            UnityEngine.Object.DestroyImmediate(modalGo);
        }

        [Test]
        public void RightBarEndTurn_BlocksSetPlayerReady_WhenRequirementContextHasPendingMethods()
        {
            var rbGo = new GameObject("RightBarTest");
            var rb = rbGo.AddComponent<RightBarController>();

            var endBtnGo = new GameObject("EndTurnBtn");
            endBtnGo.transform.SetParent(rbGo.transform);
            var endBtn = endBtnGo.AddComponent<Button>();
            var endBtnTextGo = new GameObject("EndTurnText");
            endBtnTextGo.transform.SetParent(endBtnGo.transform);
            var endBtnText = endBtnTextGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(rb, "_endTurnButton", endBtn);
            SetPrivateField(rb, "_endTurnButtonText", endBtnText);

            var turnManager = new TurnManager(requiredPlayers: 2);
            // Advance to Build phase
            turnManager.ForceAdvancePhase();
            Assert.AreEqual(TurnPhase.Build, turnManager.CurrentPhase);

            var reqContext = new FakeRequirementContext { HasPendingMethods = true };

            rb.Initialize(
                scoreContext: new FakeScoreContext(),
                refactorContext: new FakeRefactorContext(),
                turnPlayerActions: turnManager,
                localSlot: PlayerSlot.PlayerOne,
                turnContext: turnManager,
                requirementContext: reqContext
            );

            bool localReadyFired = false;
            turnManager.OnPlayerReady += _ => localReadyFired = true;

            Assert.IsFalse(endBtn.interactable, "End turn button must be non-interactable while HasPendingMethods is true.");
            StringAssert.Contains("Distribua os Métodos", endBtnText.text);

            endBtn.onClick.Invoke();
            Assert.IsFalse(localReadyFired, "Clicking end turn must not mark player ready while HasPendingMethods is true.");

            // Once all methods are distributed, End Turn becomes enabled and marks player ready
            reqContext.HasPendingMethods = false;
            reqContext.NotifyRequirementChanged();

            Assert.IsTrue(endBtn.interactable, "End turn button must be interactable when HasPendingMethods becomes false.");
            endBtn.onClick.Invoke();
            Assert.IsTrue(localReadyFired, "Clicking end turn when HasPendingMethods is false must mark player ready.");

            UnityEngine.Object.DestroyImmediate(rbGo);
        }
        #endregion

        #region MethodCardUI Drag and Drop Tests
        [Test]
        public void MethodCardUI_OnBeginDrag_ReparentsToCanvasAndDisablesRaycasts()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();

            var containerGo = new GameObject("Container", typeof(RectTransform));
            containerGo.transform.SetParent(canvasGo.transform);

            var cardGo = new GameObject("MethodCard", typeof(RectTransform), typeof(CanvasGroup));
            cardGo.transform.SetParent(containerGo.transform);

            var cardUI = cardGo.AddComponent<MethodCardUI>();
            cardUI.Setup(new MethodData("CadastrarLivro", Core.Evaluation.CodeSmellType.LargeClass.ToString()));

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(100f, 100f)
            };

            cardUI.OnBeginDrag(eventData);

            Assert.AreEqual(canvas.transform, cardGo.transform.parent, "Card should be reparented to canvas during drag.");
            var canvasGroup = cardGo.GetComponent<CanvasGroup>();
            Assert.IsFalse(canvasGroup.blocksRaycasts, "Raycasts must be disabled during drag so drop targets can receive events.");

            cardUI.OnEndDrag(eventData);

            Assert.AreEqual(containerGo.transform, cardGo.transform.parent, "Card should return to container after drag ends if not placed.");
            Assert.IsTrue(canvasGroup.blocksRaycasts, "Raycasts must be re-enabled after drag ends.");

            UnityEngine.Object.DestroyImmediate(canvasGo);
        }
        #endregion
    }
}
