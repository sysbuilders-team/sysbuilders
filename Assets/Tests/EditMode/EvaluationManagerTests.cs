using System.Collections.Generic;
using NUnit.Framework;
using Core.Diagram;
using Core.Requirements;
using Managers.Evaluation;

namespace Tests.EditMode
{
    [TestFixture]
    public class EvaluationManagerTests
    {
        [Test]
        public void RunEvaluation_DelegatesToCoreEngine_AndReturnsResult()
        {
            var manager = new EvaluationManager();
            var diagram = new ClassDiagram();
            var node = new ClassNode("UsuarioService");
            var method = new MethodData("Login", "Usuario");
            
            node.AddMethod(method);
            diagram.AddClass(node);

            var requirement = new Requirement("Login Req");
            foreach (var methodData in new List<MethodData> { method }) requirement.AddRequirementMethods(methodData);

            var result = manager.RunEvaluation(diagram, new List<Requirement> { requirement });

            Assert.IsNotNull(result);
            Assert.AreEqual(100, result.TotalScore);
            Assert.AreEqual(1, result.FulfilledRequirementIds.Count);
        }
    }
}