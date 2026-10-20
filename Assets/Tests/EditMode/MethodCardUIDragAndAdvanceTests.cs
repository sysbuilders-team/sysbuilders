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
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object InvokePrivateMethod(object target, string methodName, params object[] parameters)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}");
            return method.Invoke(target, parameters);
        }

        #region GameManager Advance Modal Tests
        [Test]
        public void HandleAdvanceClicked_InvokesOnLocalPlayerReady_EvenWhenToolboxHasPendingMethods()
        {
            var gmGo = new GameObject("GameManagerTest");
            var gm = gmGo.AddComponent<GameManager>();

            var tbGo = new GameObject("Toolbox");
            var tb = tbGo.AddComponent<ToolboxController>();

            var containerGo = new GameObject("MethodsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(tbGo.transform);
            SetPrivateField(tb, "_methodsContainer", containerGo.transform);

            // Add a pending card to toolbox to simulate pending methods
            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(containerGo.transform);
            var cardUI = cardGo.AddComponent<MethodCardUI>();
            cardUI.Setup(new MethodData("TestMethod", "TestCategory"), isCurrentTurn: true);
            var activeCards = new List<MethodCardUI> { cardUI };
            SetPrivateField(tb, "_activeCards", activeCards);

            Assert.IsTrue(tb.HasPendingMethods, "Toolbox should report pending methods.");

            SetPrivateField(gm, "_toolBox", tb);

            bool localReadyFired = false;
            gm.OnLocalPlayerReady += () => localReadyFired = true;

            // Invoking HandleAdvanceClicked (modal advance) must NOT be blocked by pending methods
            InvokePrivateMethod(gm, "HandleAdvanceClicked");

            Assert.IsTrue(localReadyFired, "HandleAdvanceClicked must fire OnLocalPlayerReady regardless of pending methods.");

            UnityEngine.Object.DestroyImmediate(gmGo);
            UnityEngine.Object.DestroyImmediate(tbGo);
        }

        [Test]
        public void HandleEndTurnTriggered_BlocksOnLocalPlayerReady_WhenToolboxHasPendingMethods()
        {
            var gmGo = new GameObject("GameManagerTest");
            var gm = gmGo.AddComponent<GameManager>();

            var tbGo = new GameObject("Toolbox");
            var tb = tbGo.AddComponent<ToolboxController>();

            var containerGo = new GameObject("MethodsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(tbGo.transform);
            SetPrivateField(tb, "_methodsContainer", containerGo.transform);

            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(containerGo.transform);
            var cardUI = cardGo.AddComponent<MethodCardUI>();
            cardUI.Setup(new MethodData("TestMethod", "TestCategory"), isCurrentTurn: true);
            var activeCards = new List<MethodCardUI> { cardUI };
            SetPrivateField(tb, "_activeCards", activeCards);

            SetPrivateField(gm, "_toolBox", tb);

            bool localReadyFired = false;
            gm.OnLocalPlayerReady += () => localReadyFired = true;

            // Invoking HandleEndTurnTriggered (right bar during build) MUST be blocked
            InvokePrivateMethod(gm, "HandleEndTurnTriggered");

            Assert.IsFalse(localReadyFired, "HandleEndTurnTriggered must not fire OnLocalPlayerReady when there are pending methods.");

            UnityEngine.Object.DestroyImmediate(gmGo);
            UnityEngine.Object.DestroyImmediate(tbGo);
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
