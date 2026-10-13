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
    }
}
