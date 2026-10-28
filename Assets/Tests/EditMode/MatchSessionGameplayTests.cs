using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Diagram;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class MatchSessionGameplayTests
    {
        private List<Requirement> _requirements;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(Application.dataPath, "Resources", "requirements.json");
            string json = File.ReadAllText(path);
            var catalog = new RequirementCatalog(RequirementLoader.LoadFromJson(json));
            _requirements = catalog.GetRequirementsForMatch("Biblioteca", "trail_biblioteca_01").ToList();
        }

        [Test]
        public void MatchSession_FullGameLoop_CreationSelectionRefactoringAndAdvancing()
        {
            var session = new MatchSession("Alice", "Bob", _requirements, PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;
            var refactor = session.RefactorContext;

            // 1. Turno 1 - Fase Show -> Inicia em Show
            Assert.AreEqual(1, session.TurnManager.CurrentTurn);
            Assert.AreEqual(TurnPhase.Show, session.TurnManager.CurrentPhase);
            Assert.IsNotNull(session.CurrentRequirement);
            Assert.AreEqual("req_turno_1", session.CurrentRequirement.Id);

            // Avança para Build
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            // 2. Criação de classe via IDiagramActions
            bool created = editor.TryCreateClass("Leitor", out var leitorNode);
            Assert.IsTrue(created);
            Assert.IsNotNull(leitorNode);
            Assert.AreEqual(leitorNode, editor.SelectedClass, "A nova classe criada deve ser selecionada automaticamente.");

            // 3. Alocação de método do requisito na classe selecionada
            var req1 = session.CurrentRequirement;
            var metodoUsuario = req1.RequirementMethods.First(m => m.Category == "Usuario");
            bool methodAdded = editor.AddMethodToClass(editor.SelectedClass, metodoUsuario);
            Assert.IsTrue(methodAdded);
            Assert.AreEqual(1, leitorNode.Methods.Count);

            // 4. Criação de segunda classe e modo de associação
            editor.SetAssociationMode(true);
            Assert.IsTrue(editor.IsAssociationMode);

            editor.TryCreateClass("Notificador", out var notificadorNode);
            Assert.AreEqual(notificadorNode, editor.SelectedClass);

            var metodoNotificacao = req1.RequirementMethods.First(m => m.Category == "Notificacao");
            editor.AddMethodToClass(editor.SelectedClass, metodoNotificacao);

            // Cria associação entre Leitor e Notificador
            bool assocAdded = editor.AddRelation(leitorNode, notificadorNode, RelationType.Association);
            Assert.IsTrue(assocAdded);
            Assert.IsFalse(editor.IsAssociationMode, "Após criar a relação, o modo de associação deve desligar automaticamente.");

            // 5. Finalizar Turno 1 -> Avaliação
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);
            Assert.AreEqual(TurnPhase.Evaluate, session.TurnManager.CurrentPhase);

            // Classes foram congeladas na avaliação
            Assert.IsTrue(leitorNode.IsLocked);
            Assert.IsTrue(notificadorNode.IsLocked);
            Assert.IsNull(editor.SelectedClass, "A seleção deve ser limpa ao sair da fase de construção.");

            // 6. Avançar para Turno 2 (Show -> Build)
            session.TurnManager.ForceAdvancePhase(); // Vai para Show do Turno 2
            Assert.AreEqual(2, session.TurnManager.CurrentTurn);
            Assert.AreEqual(TurnPhase.Show, session.TurnManager.CurrentPhase);

            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Vai para Build do Turno 2
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            // 7. Teste de Refatoração via IRefactorContext
            editor.SelectClass(leitorNode);
            Assert.AreEqual(leitorNode, editor.SelectedClass);
            Assert.IsTrue(refactor.CanRefactorSelected());

            int initialPoints = session.LocalPlayer.RefactorPoints;
            bool refactored = refactor.TryRefactorSelectedClass();
            Assert.IsTrue(refactored);
            Assert.IsFalse(leitorNode.IsLocked, "A classe deve ser destravada após ser refatorada.");
            Assert.AreEqual(initialPoints - session.LocalPlayer.RefactorCost, session.LocalPlayer.RefactorPoints);

            // Consegue adicionar novo método na classe destravada
            var req2 = session.CurrentRequirement;
            var novoMetodo = req2.RequirementMethods[0];
            bool addedAfterUnlock = editor.AddMethodToClass(editor.SelectedClass, novoMetodo);
            Assert.IsTrue(addedAfterUnlock);
        }

        [Test]
        public void ClassDiagram_DuplicateAssociation_IsPreventedBidirectionally()
        {
            var diagram = new ClassDiagram();
            var nodeA = new ClassNode("Aluno");
            var nodeB = new ClassNode("Curso");
            diagram.AddClass(nodeA);
            diagram.AddClass(nodeB);

            // Primeira adição deve suceder
            bool firstAdded = diagram.AddRelation(new Relation(nodeA, nodeB, RelationType.Association));
            Assert.IsTrue(firstAdded);
            Assert.AreEqual(1, diagram.Relations.Count);

            // Segunda tentativa na mesma direção (A -> B) deve ser rejeitada
            bool duplicateSameDirection = diagram.AddRelation(new Relation(nodeA, nodeB, RelationType.Association));
            Assert.IsFalse(duplicateSameDirection, "Não deve permitir associação duplicada na mesma direção.");
            Assert.AreEqual(1, diagram.Relations.Count);

            // Terceira tentativa na direção oposta (B -> A) deve ser rejeitada
            bool duplicateOppositeDirection = diagram.AddRelation(new Relation(nodeB, nodeA, RelationType.Association));
            Assert.IsFalse(duplicateOppositeDirection, "Não deve permitir associação duplicada na direção contrária.");
            Assert.AreEqual(1, diagram.Relations.Count);

            // Remover relação e testar que agora pode adicionar de novo
            bool removed = diagram.RemoveRelation(nodeA, nodeB);
            Assert.IsTrue(removed);
            Assert.AreEqual(0, diagram.Relations.Count);

            bool readded = diagram.AddRelation(new Relation(nodeB, nodeA, RelationType.Association));
            Assert.IsTrue(readded);
            Assert.AreEqual(1, diagram.Relations.Count);
        }

        [Test]
        public void MatchSession_DuplicateAssociation_TriggersOnActionFailed()
        {
            var session = new MatchSession("Alice", "Bob", _requirements, PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;

            // Avança para Build
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            editor.TryCreateClass("ClasseA", out var nodeA);
            editor.TryCreateClass("ClasseB", out var nodeB);

            string failureMessage = null;
            editor.OnActionFailed += msg => failureMessage = msg;

            bool firstAdded = editor.AddRelation(nodeA, nodeB);
            Assert.IsTrue(firstAdded);
            Assert.IsNull(failureMessage);

            bool secondAdded = editor.AddRelation(nodeA, nodeB);
            Assert.IsFalse(secondAdded);
            Assert.IsNotNull(failureMessage);
            Assert.IsTrue(failureMessage.Contains("associação"), $"Mensagem de erro esperada, recebido: {failureMessage}");
        }

        [Test]
        public void MatchSession_SelfAssociation_TriggersOnActionFailed()
        {
            var session = new MatchSession("Alice", "Bob", _requirements, PlayerSlot.PlayerOne, requiredPlayers: 2);
            var editor = session.DiagramActions;

            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);

            editor.TryCreateClass("ClasseA", out var nodeA);

            string failureMessage = null;
            editor.OnActionFailed += msg => failureMessage = msg;

            bool selfAssoc = editor.AddRelation(nodeA, nodeA);
            Assert.IsFalse(selfAssoc);
            Assert.IsNotNull(failureMessage);
            Assert.IsTrue(failureMessage.Contains("si mesma"), $"Mensagem de erro esperada, recebido: {failureMessage}");
        }

        [Test]
        public void MatchSession_HasPendingMethods_TracksUnassignedCurrentRequirementMethods()
        {
            var session = new MatchSession("Alice", "Bob", _requirements, PlayerSlot.PlayerOne, requiredPlayers: 1);
            var editor = session.DiagramActions;

            // Avança de Show para Build
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            Assert.IsTrue(session.HasPendingMethods, "Deve haver métodos pendentes antes de distribuir os métodos do turno.");

            editor.TryCreateClass("ClasseUnica", out var node);
            foreach (var method in session.CurrentRequirement.RequirementMethods)
            {
                editor.AddMethodToClass(node, method);
            }

            Assert.IsFalse(session.HasPendingMethods, "Não deve haver métodos pendentes após alocar todos os métodos do requisito atual.");
        }
    }
}
