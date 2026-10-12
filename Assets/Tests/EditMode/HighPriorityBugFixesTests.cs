using System.Collections.Generic;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class HighPriorityBugFixesTests
    {
        private List<Requirement> CreateDummyRequirements()
        {
            var reqs = new List<Requirement>();
            for (int i = 0; i < Trail.TurnCount; i++)
            {
                reqs.Add(new Requirement($"req_{i + 1}", $"Desc {i + 1}", new List<MethodData>
                {
                    new MethodData($"Method_{i + 1}", "CategoryA")
                }));
            }
            return reqs;
        }

        #region B1 Tests: Class Creation and Unique Naming
        [Test]
        public void B1_TryCreateClass_WithNullOrEmptyName_GeneratesUniqueSequentialNames()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Now in Build phase

            bool firstCreated = session.TryCreateClass(null, out var firstNode);
            Assert.IsTrue(firstCreated);
            Assert.IsNotNull(firstNode);
            Assert.AreEqual("Classe1", firstNode.Name);
            Assert.AreEqual(firstNode, session.SelectedClass, "Nova classe criada deve ser auto-selecionada.");

            bool secondCreated = session.TryCreateClass(string.Empty, out var secondNode);
            Assert.IsTrue(secondCreated);
            Assert.IsNotNull(secondNode);
            Assert.AreEqual("Classe2", secondNode.Name);
            Assert.AreEqual(secondNode, session.SelectedClass);

            bool thirdCreated = session.TryCreateClass("   ", out var thirdNode);
            Assert.IsTrue(thirdCreated);
            Assert.IsNotNull(thirdNode);
            Assert.AreEqual("Classe3", thirdNode.Name);
        }

        [Test]
        public void B1_TryCreateClass_WithExplicitName_UsesGivenNameAndRejectsDuplicate()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            string failureMessage = null;
            session.OnActionFailed += msg => failureMessage = msg;

            bool firstCreated = session.TryCreateClass("Usuario", out var firstNode);
            Assert.IsTrue(firstCreated);
            Assert.AreEqual("Usuario", firstNode.Name);
            Assert.IsNull(failureMessage);

            bool duplicateCreated = session.TryCreateClass("Usuario", out var duplicateNode);
            Assert.IsFalse(duplicateCreated);
            Assert.IsNull(duplicateNode);
            Assert.IsNotNull(failureMessage);
            StringAssert.Contains("Usuario", failureMessage);
        }
        #endregion

        #region B2 Tests: Refactor Point Policy and Refactor Execution
        [Test]
        public void B2_DefaultRefactorPointPolicy_ProvidesTwoInitialPoints()
        {
            var policy = new DefaultRefactorPointPolicy();
            Assert.AreEqual(2, policy.InitialPoints);
            Assert.AreEqual(0, policy.GetPointsForTurn(1));
            Assert.AreEqual(0, policy.GetPointsForTurn(2));
        }

        [Test]
        public void B2_MatchSession_AwardsInitialRefactorPointsFromPolicy()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);

            Assert.AreEqual(2, session.RefactorPoints);
            Assert.AreEqual(2, session.LocalPlayer.RefactorPoints);
            Assert.AreEqual(2, session.OpponentPlayer.RefactorPoints);
            Assert.AreEqual(1, session.RefactorCost);
        }

        [Test]
        public void B2_TryRefactorClass_ConsumesPointUnlocksClassAndRaisesEvents()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Build Turn 1

            session.TryCreateClass("Servico", out var node);
            node.Lock(); // Simula bloqueio de turno anterior
            Assert.IsTrue(node.IsLocked);

            int reportedPoints = -1;
            bool diagramChangedFired = false;
            session.OnRefactorPointsChanged += pts => reportedPoints = pts;
            session.OnDiagramChanged += () => diagramChangedFired = true;

            Assert.IsTrue(session.CanRefactor(node));
            bool refactorResult = session.TryRefactorClass(node);

            Assert.IsTrue(refactorResult);
            Assert.IsFalse(node.IsLocked, "A classe deve ser destravada após a refatoração.");
            Assert.AreEqual(1, session.RefactorPoints, "Deve ter debitado 1 ponto de refatoração.");
            Assert.AreEqual(1, reportedPoints, "Evento OnRefactorPointsChanged deve ter sido disparado com o novo valor.");
            Assert.IsTrue(diagramChangedFired, "Evento OnDiagramChanged deve ter sido disparado para atualizar a UI.");
        }
        #endregion

        #region B3 Tests: RemoveClass Behavior
        [Test]
        public void B3_RemoveClass_SuccessfullyRemovesClassAndRaisesDiagramChanged()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            session.TryCreateClass("Temporaria", out var node);
            Assert.AreEqual(node, session.SelectedClass);
            Assert.AreEqual(1, session.LocalPlayer.Diagram.Classes.Count);

            bool diagramChangedFired = false;
            session.OnDiagramChanged += () => diagramChangedFired = true;

            bool removed = session.RemoveClass(node);

            Assert.IsTrue(removed, "RemoveClass deve retornar true quando a classe for removida com sucesso.");
            Assert.AreEqual(0, session.LocalPlayer.Diagram.Classes.Count);
            Assert.IsNull(session.SelectedClass, "Seleção deve ser limpa se a classe selecionada foi removida.");
            Assert.IsTrue(diagramChangedFired, "OnDiagramChanged deve ser invocado após a remoção da classe.");
        }

        [Test]
        public void B3_RemoveClass_WhenClassIsLocked_ReturnsFalse()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            session.TryCreateClass("Fixa", out var node);
            node.Lock();

            bool removed = session.RemoveClass(node);

            Assert.IsFalse(removed, "Não deve remover classe bloqueada sem refatorá-la antes.");
            Assert.AreEqual(1, session.LocalPlayer.Diagram.Classes.Count);
        }
        #endregion
    }
}
