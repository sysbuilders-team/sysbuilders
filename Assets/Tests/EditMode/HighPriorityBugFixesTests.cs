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
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Now in Build phase

            bool firstCreated = editor.TryCreateClass(null, out var firstNode);
            Assert.IsTrue(firstCreated);
            Assert.IsNotNull(firstNode);
            Assert.AreEqual("Classe1", firstNode.Name);
            Assert.AreEqual(firstNode, editor.SelectedClass, "Nova classe criada deve ser auto-selecionada.");

            bool secondCreated = editor.TryCreateClass(string.Empty, out var secondNode);
            Assert.IsTrue(secondCreated);
            Assert.IsNotNull(secondNode);
            Assert.AreEqual("Classe2", secondNode.Name);
            Assert.AreEqual(secondNode, editor.SelectedClass);

            // Avança para o Turno 2 para liberar a cota de criação por turno (2 por turno)
            session.TurnManager.ForceAdvancePhase(); // Build -> Evaluate
            session.TurnManager.ForceAdvancePhase(); // Evaluate -> Show (Turno 2)
            session.TurnManager.ForceAdvancePhase(); // Show -> Build (Turno 2)

            bool thirdCreated = editor.TryCreateClass("   ", out var thirdNode);
            Assert.IsTrue(thirdCreated);
            Assert.IsNotNull(thirdNode);
            Assert.AreEqual("Classe3", thirdNode.Name);
        }

        [Test]
        public void B1_TryCreateClass_WithExplicitName_UsesGivenNameAndRejectsDuplicate()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            string failureMessage = null;
            editor.OnActionFailed += msg => failureMessage = msg;

            bool firstCreated = editor.TryCreateClass("Usuario", out var firstNode);
            Assert.IsTrue(firstCreated);
            Assert.AreEqual("Usuario", firstNode.Name);
            Assert.IsNull(failureMessage);

            bool duplicateCreated = editor.TryCreateClass("Usuario", out var duplicateNode);
            Assert.IsFalse(duplicateCreated);
            Assert.IsNull(duplicateNode);
            Assert.IsNotNull(failureMessage);
            StringAssert.Contains("Usuario", failureMessage);
        }
        #endregion

        #region B2 Tests: Refactor Point Policy and Refactor Execution
        [Test]
        public void B2_DefaultRefactorPointPolicy_ProvidesProgressivePoints()
        {
            var policy = new DefaultRefactorPointPolicy();
            Assert.AreEqual(1, policy.InitialPoints);
            Assert.AreEqual(0, policy.GetPointsForTurn(1));
            Assert.AreEqual(1, policy.GetPointsForTurn(2));
            Assert.AreEqual(1, policy.GetPointsForTurn(3));
            Assert.AreEqual(2, policy.MaxStoredPoints);
        }

        [Test]
        public void B2_MatchSession_AwardsInitialRefactorPointsFromPolicy()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            var refactor = session.RefactorContext;

            Assert.AreEqual(1, refactor.RefactorPoints);
            Assert.AreEqual(1, session.LocalPlayer.RefactorPoints);
            Assert.AreEqual(1, session.OpponentPlayer.RefactorPoints);
            Assert.AreEqual(1, refactor.RefactorCost);
        }

        [Test]
        public void B2_PlayerState_CapsRefactorPointsAtMax()
        {
            var player = new PlayerState("Alice");
            Assert.AreEqual(2, player.MaxRefactorPoints);
            player.AddRefactorPoints(1);
            Assert.AreEqual(1, player.RefactorPoints);
            player.AddRefactorPoints(1);
            Assert.AreEqual(2, player.RefactorPoints);
            player.AddRefactorPoints(5);
            Assert.AreEqual(2, player.RefactorPoints, "Pontos não devem ultrapassar MaxRefactorPoints (2).");
        }

        [Test]
        public void B2_TryRefactorClass_ConsumesPointUnlocksClassAndRaisesEvents()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;
            var refactor = session.RefactorContext;
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Build Turn 1

            editor.TryCreateClass("Servico", out var node);
            node.Lock(); // Simula bloqueio de turno anterior
            Assert.IsTrue(node.IsLocked);

            int reportedPoints = -1;
            bool diagramChangedFired = false;
            refactor.OnRefactorPointsChanged += pts => reportedPoints = pts;
            editor.OnDiagramChanged += () => diagramChangedFired = true;

            Assert.IsTrue(refactor.CanRefactor(node));
            bool refactorResult = refactor.TryRefactorClass(node);

            Assert.IsTrue(refactorResult);
            Assert.IsFalse(node.IsLocked, "A classe deve ser destravada após a refatoração.");
            Assert.AreEqual(0, refactor.RefactorPoints, "Deve ter debitado 1 ponto de refatoração.");
            Assert.AreEqual(0, reportedPoints, "Evento OnRefactorPointsChanged deve ter sido disparado com o novo valor.");
            Assert.IsTrue(diagramChangedFired, "Evento OnDiagramChanged deve ter sido disparado para atualizar a UI.");
        }

        [Test]
        public void B2_TryRefactorClass_RejectsUnlockedOrForeignClassWithoutFiringEvents()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;
            var refactor = session.RefactorContext;
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Build Turn 1

            editor.TryCreateClass("Desbloqueada", out var unlockedNode);

            bool pointsEventFired = false;
            bool diagramEventFired = false;
            refactor.OnRefactorPointsChanged += _ => pointsEventFired = true;
            editor.OnDiagramChanged += () => diagramEventFired = true;

            Assert.IsFalse(refactor.CanRefactor(unlockedNode));
            Assert.IsFalse(refactor.TryRefactorClass(unlockedNode), "Não deve refatorar classe já desbloqueada.");
            Assert.IsFalse(pointsEventFired);
            Assert.IsFalse(diagramEventFired);

            var foreignLockedNode = new ClassNode("Externa");
            foreignLockedNode.Lock();

            Assert.IsFalse(refactor.CanRefactor(foreignLockedNode));
            Assert.IsFalse(refactor.TryRefactorClass(foreignLockedNode), "Não deve refatorar classe que não pertence ao diagrama local.");
            Assert.IsTrue(foreignLockedNode.IsLocked);
            Assert.AreEqual(1, refactor.RefactorPoints);
        }
        #endregion

        #region B3 Tests: Class Creation Quotas & RemoveClass Behavior
        [Test]
        public void B3_CanModifyDiagram_IsOnlyTrueDuringBuildPhase()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            var editor = session.DiagramActions;

            Assert.IsFalse(editor.CanModifyDiagram, "Na fase Show o diagrama não pode ser modificado.");

            session.SetPlayerReady(PlayerSlot.PlayerOne);
            Assert.IsTrue(editor.CanModifyDiagram, "Na fase Build o diagrama pode ser modificado.");

            session.SetPlayerReady(PlayerSlot.PlayerOne);
            Assert.IsFalse(editor.CanModifyDiagram, "Na fase Evaluate o diagrama não pode ser modificado.");
        }

        [Test]
        public void B3_RemoveClass_RemovesAssociationsFreesMethodsAndRefundsTurnQuota()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            var editor = session.DiagramActions;
            session.SetPlayerReady(PlayerSlot.PlayerOne);

            editor.TryCreateClass("Origem", out var origem);
            editor.TryCreateClass("Destino", out var destino);
            Assert.AreEqual(2, editor.ClassesCreatedThisTurn);
            Assert.IsFalse(editor.CanCreateClass);

            var metodo = session.CurrentRequirement.RequirementMethods[0];
            Assert.IsTrue(editor.AddMethodToClass(origem, metodo));
            Assert.IsTrue(editor.AddRelation(origem, destino));
            Assert.AreEqual(1, session.LocalPlayer.Diagram.Relations.Count);

            Assert.IsTrue(editor.RemoveClass(origem));

            Assert.AreEqual(1, editor.ClassesCreatedThisTurn, "Remover uma classe criada neste turno deve devolver o slot de criação do turno.");
            Assert.IsTrue(editor.CanCreateClass);
            Assert.AreEqual(0, session.LocalPlayer.Diagram.Relations.Count, "Associações de origem e destino devem ser removidas.");
            Assert.IsTrue(editor.AddMethodToClass(destino, metodo), "O método deve ficar disponível para outra classe após a exclusão.");
        }

        [Test]
        public void B3_RemoveClass_RejectsLockedClassAndFiresOnActionFailed()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne);
            var editor = session.DiagramActions;
            session.SetPlayerReady(PlayerSlot.PlayerOne);

            editor.TryCreateClass("Fixa", out var node);
            node.Lock();

            string failureMessage = null;
            editor.OnActionFailed += msg => failureMessage = msg;

            Assert.IsFalse(editor.RemoveClass(node));
            Assert.IsNotNull(failureMessage);
            Assert.AreEqual(1, session.LocalPlayer.Diagram.Classes.Count);
        }

        [Test]
        public void B3_TryCreateClass_EnforcesTurnAndMatchQuotasAcrossSession()
        {
            var session = new MatchSession("Alice", "Bob", CreateDummyRequirements(), PlayerSlot.PlayerOne, requiredPlayers: 2, maxClassesPerMatch: 3);
            var editor = session.DiagramActions;
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Build Turn 1

            Assert.AreEqual(2, editor.MaxClassesPerTurn);
            Assert.AreEqual(3, editor.MaxClassesPerMatch);

            Assert.IsTrue(editor.TryCreateClass("C1", out _));
            Assert.IsTrue(editor.TryCreateClass("C2", out _));
            Assert.AreEqual(2, editor.ClassesCreatedThisTurn);
            Assert.AreEqual(2, editor.TotalClassesCreated);
            Assert.IsFalse(editor.CanCreateClass);
            Assert.IsFalse(editor.TryCreateClass("C3_BloqueadaNoTurno1", out _));

            // Avança para Turno 2 (Evaluate -> Show -> Build)
            session.TurnManager.ForceAdvancePhase();
            session.TurnManager.ForceAdvancePhase();
            session.TurnManager.ForceAdvancePhase();

            Assert.AreEqual(0, editor.ClassesCreatedThisTurn, "Cota do turno deve resetar no novo turno.");
            Assert.AreEqual(2, editor.TotalClassesCreated, "Total da partida deve persistir entre turnos.");
            Assert.IsTrue(editor.CanCreateClass);

            Assert.IsTrue(editor.TryCreateClass("C3", out _));
            Assert.AreEqual(1, editor.ClassesCreatedThisTurn);
            Assert.AreEqual(3, editor.TotalClassesCreated);
            Assert.IsFalse(editor.CanCreateClass, "Limite máximo da partida (3) foi atingido.");
            Assert.IsFalse(editor.TryCreateClass("C4_BloqueadaNaPartida", out _));
        }
        #endregion
    }
}
