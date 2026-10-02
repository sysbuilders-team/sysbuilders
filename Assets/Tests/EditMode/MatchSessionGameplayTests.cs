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
            var session = new MatchSession("Alice", "Bob", _requirements, PlayerSlot.PlayerOne);

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
            bool created = session.TryCreateClass("Leitor", out var leitorNode);
            Assert.IsTrue(created);
            Assert.IsNotNull(leitorNode);
            Assert.AreEqual(leitorNode, session.SelectedClass, "A nova classe criada deve ser selecionada automaticamente.");

            // 3. Alocação de método do requisito na classe selecionada
            var req1 = session.CurrentRequirement;
            var metodoUsuario = req1.RequirementMethods.First(m => m.Category == "Usuario");
            bool methodAdded = session.AddMethodToClass(session.SelectedClass, metodoUsuario);
            Assert.IsTrue(methodAdded);
            Assert.AreEqual(1, leitorNode.Methods.Count);

            // 4. Criação de segunda classe e modo de associação
            session.SetAssociationMode(true);
            Assert.IsTrue(session.IsAssociationMode);

            session.TryCreateClass("Notificador", out var notificadorNode);
            Assert.AreEqual(notificadorNode, session.SelectedClass);

            var metodoNotificacao = req1.RequirementMethods.First(m => m.Category == "Notificacao");
            session.AddMethodToClass(session.SelectedClass, metodoNotificacao);

            // Cria associação entre Leitor e Notificador
            bool assocAdded = session.AddRelation(leitorNode, notificadorNode, RelationType.Association);
            Assert.IsTrue(assocAdded);
            Assert.IsFalse(session.IsAssociationMode, "Após criar a relação, o modo de associação deve desligar automaticamente.");

            // 5. Finalizar Turno 1 -> Avaliação
            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo);
            Assert.AreEqual(TurnPhase.Evaluate, session.TurnManager.CurrentPhase);

            // Classes foram congeladas na avaliação
            Assert.IsTrue(leitorNode.IsLocked);
            Assert.IsTrue(notificadorNode.IsLocked);
            Assert.IsNull(session.SelectedClass, "A seleção deve ser limpa ao sair da fase de construção.");

            // 6. Avançar para Turno 2 (Show -> Build)
            session.TurnManager.ForceAdvancePhase(); // Vai para Show do Turno 2
            Assert.AreEqual(2, session.TurnManager.CurrentTurn);
            Assert.AreEqual(TurnPhase.Show, session.TurnManager.CurrentPhase);

            session.SetPlayerReady(PlayerSlot.PlayerOne);
            session.SetPlayerReady(PlayerSlot.PlayerTwo); // Vai para Build do Turno 2
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            // 7. Teste de Refatoração via IRefactorContext
            session.SelectClass(leitorNode);
            Assert.AreEqual(leitorNode, session.SelectedClass);
            Assert.IsTrue(session.CanRefactorSelected());

            int initialPoints = session.LocalPlayer.RefactorPoints;
            bool refactored = session.TryRefactorSelectedClass();
            Assert.IsTrue(refactored);
            Assert.IsFalse(leitorNode.IsLocked, "A classe deve ser destravada após ser refatorada.");
            Assert.AreEqual(initialPoints - session.LocalPlayer.RefactorCost, session.LocalPlayer.RefactorPoints);

            // Consegue adicionar novo método na classe destravada
            var req2 = session.CurrentRequirement;
            var novoMetodo = req2.RequirementMethods[0];
            bool addedAfterUnlock = session.AddMethodToClass(session.SelectedClass, novoMetodo);
            Assert.IsTrue(addedAfterUnlock);
        }
    }
}
