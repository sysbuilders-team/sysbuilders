using System.Collections.Generic;
using NUnit.Framework;
using Core.Diagram;
using Managers.Evaluation;
using Core.Requirements;

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

            var requirement = new Requirement("req_login", "Login Req", new List<MethodData>{ method });
            
            var result = manager.RunEvaluation(diagram, new List<Requirement> { requirement });

            Assert.IsNotNull(result);
            Assert.AreEqual(100, result.TotalScore);
            Assert.AreEqual(1, result.FulfilledRequirementIds.Count);
        }
    }
}