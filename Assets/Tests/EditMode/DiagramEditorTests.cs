using System.Collections.Generic;
using Core.Diagram;
using Core.Session;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class DiagramEditorTests
    {
        private PlayerState _player;
        private bool _canModify;
        private DiagramEditor _editor;

        [SetUp]
        public void SetUp()
        {
            _player = new PlayerState("Player1");
            _canModify = true;
            _editor = new DiagramEditor(() => _player, () => _canModify);
        }

        [Test]
        public void TryCreateClass_WhenAllowed_CreatesAndSelectsClass()
        {
            bool created = _editor.TryCreateClass("Pedido", out var node);

            Assert.IsTrue(created);
            Assert.IsNotNull(node);
            Assert.AreEqual("Pedido", node.Name);
            Assert.AreEqual(node, _editor.SelectedClass);
            Assert.AreEqual(1, _player.Diagram.Classes.Count);
        }

        [Test]
        public void TryCreateClass_WhenCanModifyIsFalse_ReturnsFalse()
        {
            _canModify = false;
            bool created = _editor.TryCreateClass("Pedido", out var node);

            Assert.IsFalse(created);
            Assert.IsNull(node);
        }

        [Test]
        public void RemoveClass_WhenNodeBelongsToOtherDiagram_ReturnsFalse()
        {
            var otherNode = new ClassNode("Alien");
            bool removed = _editor.RemoveClass(otherNode);

            Assert.IsFalse(removed);
        }

        [Test]
        public void AddMethodToClass_PreventsDuplicateMethods()
        {
            _editor.TryCreateClass("Servico", out var node);
            var method = new MethodData("Processar", "Vendas");

            bool first = _editor.AddMethodToClass(node, method);
            Assert.IsTrue(first);

            string failure = null;
            _editor.OnActionFailed += msg => failure = msg;

            bool duplicate = _editor.AddMethodToClass(node, method);
            Assert.IsFalse(duplicate, "Não deve permitir o mesmo método adicionado repetidamente.");
            Assert.IsNotNull(failure);
            Assert.AreEqual(1, node.Methods.Count);
        }

        [Test]
        public void AddRelation_BoundaryChecks_RejectsExternalNodesAndSelfRelation()
        {
            _editor.TryCreateClass("Origem", out var origin);
            var foreign = new ClassNode("Externo");

            bool selfAssoc = _editor.AddRelation(origin, origin);
            Assert.IsFalse(selfAssoc);

            bool foreignAssoc = _editor.AddRelation(origin, foreign);
            Assert.IsFalse(foreignAssoc);

            _editor.TryCreateClass("Destino", out var destination);
            bool validAssoc = _editor.AddRelation(origin, destination);
            Assert.IsTrue(validAssoc);
            Assert.IsFalse(_editor.IsAssociationMode, "Deve desativar o modo de associação após criar.");
        }

        [Test]
        public void RenameClass_WhenValid_RenamesClassAndFiresDiagramChanged()
        {
            _editor.TryCreateClass("AntigoNome", out var node);
            bool diagramChangedFired = false;
            _editor.OnDiagramChanged += () => diagramChangedFired = true;

            bool renamed = _editor.RenameClass(node, "NovoNome");

            Assert.IsTrue(renamed);
            Assert.AreEqual("NovoNome", node.Name);
            Assert.IsTrue(diagramChangedFired);
            Assert.IsNotNull(_player.Diagram.FindClassByName("NovoNome"));
            Assert.IsNull(_player.Diagram.FindClassByName("AntigoNome"));
        }

        [Test]
        public void RenameClass_WhenLocked_FailsAndFiresActionFailed()
        {
            _editor.TryCreateClass("ClasseTravada", out var node);
            node.Lock();

            string failure = null;
            _editor.OnActionFailed += msg => failure = msg;

            bool renamed = _editor.RenameClass(node, "Tentativa");

            Assert.IsFalse(renamed);
            Assert.AreEqual("ClasseTravada", node.Name);
            Assert.IsNotNull(failure);
        }

        [Test]
        public void RenameClass_WhenDuplicateName_FailsAndKeepsOriginalName()
        {
            _editor.TryCreateClass("ClasseA", out var nodeA);
            _editor.TryCreateClass("ClasseB", out var nodeB);

            string failure = null;
            _editor.OnActionFailed += msg => failure = msg;

            bool renamed = _editor.RenameClass(nodeB, "ClasseA");

            Assert.IsFalse(renamed);
            Assert.AreEqual("ClasseB", nodeB.Name);
            Assert.IsNotNull(failure);
        }

        [Test]
        public void RenameClass_WhenEmptyOrWhitespace_Fails()
        {
            _editor.TryCreateClass("Valida", out var node);

            Assert.IsFalse(_editor.RenameClass(node, ""));
            Assert.IsFalse(_editor.RenameClass(node, "   "));
            Assert.IsFalse(_editor.RenameClass(node, null));
            Assert.AreEqual("Valida", node.Name);
        }

        [Test]
        public void RenameClass_WhenCanModifyIsFalse_ReturnsFalse()
        {
            _editor.TryCreateClass("NomeInicial", out var node);
            _canModify = false;

            bool renamed = _editor.RenameClass(node, "OutroNome");

            Assert.IsFalse(renamed);
            Assert.AreEqual("NomeInicial", node.Name);
        }
    }
}
