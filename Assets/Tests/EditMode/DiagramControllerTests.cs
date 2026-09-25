using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Diagram;
using NUnit.Framework;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    public class DiagramControllerTests
    {
        private class FakeDiagramActions : IDiagramActions
        {
            public readonly List<Relation> CreatedRelations = new List<Relation>();
            public event Action OnDiagramChanged;

            public bool TryCreateClass(string className, out ClassNode createdNode)
            {
                createdNode = new ClassNode(className);
                OnDiagramChanged?.Invoke();
                return true;
            }

            public bool RemoveClass(ClassNode node)
            {
                OnDiagramChanged?.Invoke();
                return true;
            }

            public bool AddMethodToClass(ClassNode node, MethodData method)
            {
                node.AddMethod(method);
                OnDiagramChanged?.Invoke();
                return true;
            }

            public bool RemoveMethodFromClass(ClassNode node, MethodData method)
            {
                bool removed = node.RemoveMethod(method);
                OnDiagramChanged?.Invoke();
                return removed;
            }

            public bool AddRelation(ClassNode origin, ClassNode destination, RelationType type = RelationType.Association)
            {
                CreatedRelations.Add(new Relation(origin, destination, type));
                OnDiagramChanged?.Invoke();
                return true;
            }

            public bool RemoveRelation(ClassNode origin, ClassNode destination)
            {
                OnDiagramChanged?.Invoke();
                return true;
            }

            public void TriggerDiagramChanged() => OnDiagramChanged?.Invoke();
        }

        private GameObject _go;
        private DiagramController _controller;
        private RectTransform _content;
        private ClassDiagram _diagram;
        private FakeDiagramActions _actions;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DiagramTest");
            _controller = _go.AddComponent<DiagramController>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_go.transform);
            _content = contentGo.GetComponent<RectTransform>();

            SetPrivateField(_controller, "_diagramContent", _content);

            _diagram = new ClassDiagram();
            _actions = new FakeDiagramActions();
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
        public void Initialize_WhenDiagramHasClasses_RendersClassNodes()
        {
            _diagram.AddClass(new ClassNode("Usuario"));
            _diagram.AddClass(new ClassNode("Livro"));

            _controller.Initialize(_diagram, _actions);

            Assert.AreEqual(2, _controller.ActiveNodes.Count);
            Assert.IsTrue(_controller.ActiveNodes.ContainsKey("Usuario"));
            Assert.IsTrue(_controller.ActiveNodes.ContainsKey("Livro"));
        }

        [Test]
        public void RenderDiagram_WhenClassAdded_SpawnsNewNodeUI()
        {
            _controller.Initialize(_diagram, _actions);
            Assert.AreEqual(0, _controller.ActiveNodes.Count);

            _diagram.AddClass(new ClassNode("Emprestimo"));
            _actions.TriggerDiagramChanged();

            Assert.AreEqual(1, _controller.ActiveNodes.Count);
            Assert.IsTrue(_controller.ActiveNodes.ContainsKey("Emprestimo"));
        }

        [Test]
        public void AssociationMode_ConnectingTwoNodes_CallsAddRelation()
        {
            var nodeA = new ClassNode("Usuario");
            var nodeB = new ClassNode("Livro");
            _diagram.AddClass(nodeA);
            _diagram.AddClass(nodeB);

            _controller.Initialize(_diagram, _actions);
            _controller.SetAssociationMode(true);

            var uiA = _controller.ActiveNodes["Usuario"];
            var uiB = _controller.ActiveNodes["Livro"];

            uiA.OnPointerClick(null);
            Assert.AreEqual(uiA, _controller.SelectedNode);

            uiB.OnPointerClick(null);

            Assert.AreEqual(1, _actions.CreatedRelations.Count);
            Assert.AreEqual(nodeA, _actions.CreatedRelations[0].Origin);
            Assert.AreEqual(nodeB, _actions.CreatedRelations[0].Destination);
            Assert.IsFalse(_controller.IsAssociationMode);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
