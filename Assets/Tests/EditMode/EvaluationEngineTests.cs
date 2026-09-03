using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;

namespace Tests.EditMode
{
    public class EvaluationEngineTests
    {
        private static MethodData Method(string name, string category) => new MethodData(name, category);

        private static Requirement RequirementFor(string id, string description, params MethodData[] methods)
        {
            return new Requirement(id, description, methods);
        }

        [Test]
        public void Evaluate_NullDiagram_ReturnsZeroScoreAndEmptyResults()
        {
            var engine = new EvaluationEngine();

            var result = engine.Evaluate(null, new List<Requirement>());

            Assert.AreEqual(0, result.TotalScore);
            Assert.IsEmpty(result.FulfilledRequirementIds);
            Assert.IsEmpty(result.DetectedSmells);
        }

        [Test]
        public void Evaluate_EmptyDiagram_ReturnsZeroScoreAndEmptyResults()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.AreEqual(0, result.TotalScore);
            Assert.IsEmpty(result.FulfilledRequirementIds);
            Assert.IsEmpty(result.DetectedSmells);
        }

        [Test]
        public void CheckRequirements_AllRequiredMethodsInSameClass_IsFulfilled()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            classNode.AddMethod(Method("Cancelar", "Usuario"));
            diagram.AddClass(classNode);

            var requirement = RequirementFor("reqBiblioteca_01", "Gerenciar matrícula",
                Method("Matricular", "Usuario"), Method("Cancelar", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { requirement });

            Assert.That(result.FulfilledRequirementIds, Has.Member(requirement.Id));
        }

        [Test]
        public void CheckRequirements_RequiredMethodsScatteredAcrossClasses_IsNotFulfilled()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classA = new ClassNode("ClasseA");
            classA.AddMethod(Method("Matricular", "Usuario"));
            var classB = new ClassNode("ClasseB");
            classB.AddMethod(Method("Cancelar", "Usuario"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);

            var requirement = RequirementFor("reqBiblioteca_02", "Gerenciar matrícula",
                Method("Matricular", "Usuario"), Method("Cancelar", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { requirement });

            Assert.IsEmpty(result.FulfilledRequirementIds);
        }

        [Test]
        public void CheckRequirements_ClassHasExtraUnrelatedMethods_StillFulfilled()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            classNode.AddMethod(Method("EnviarEmail", "Notificacao"));
            diagram.AddClass(classNode);

            var requirement = RequirementFor("reqBiblioteca_03", "Matricular aluno", Method("Matricular", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { requirement });

            Assert.That(result.FulfilledRequirementIds, Has.Member(requirement.Id));
        }

        [Test]
        public void DetectCodeSmells_ClassWithFiveMethods_TriggersLargeClass()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("GerenciadorTotal");
            for (int i = 0; i < 5; i++)
            {
                classNode.AddMethod(Method($"Metodo{i}", "Usuario"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass));
        }

        [Test]
        public void DetectCodeSmells_ClassWithExactlyFourMethods_DoesNotTriggerLargeClass()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            for (int i = 0; i < 4; i++)
            {
                classNode.AddMethod(Method($"Metodo{i}", "Usuario"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass));
        }

        [Test]
        public void DetectCodeSmells_ClassWithTwoCategories_TriggersLowCohesion()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            classNode.AddMethod(Method("EnviarEmail", "Notificacao"));
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowCohesion));
        }

        [Test]
        public void DetectCodeSmells_ClassWithSingleCategory_DoesNotTriggerLowCohesion()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            classNode.AddMethod(Method("Cancelar", "Usuario"));
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowCohesion));
        }

        [Test]
        public void DetectCodeSmells_SameMethodNameInTwoClasses_TriggersDuplicateCodeWithBothClassNames()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classA = new ClassNode("ClasseA");
            classA.AddMethod(Method("Validar", "Usuario"));
            var classB = new ClassNode("ClasseB");
            classB.AddMethod(Method("Validar", "Usuario"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            var duplicate = result.DetectedSmells.SingleOrDefault(s => s.Type == CodeSmellType.DuplicateCode);
            Assert.IsNotNull(duplicate);
            Assert.AreEqual(2, duplicate.AffectedClassNames.Count);
            Assert.That(duplicate.AffectedClassNames, Has.Member("ClasseA"));
            Assert.That(duplicate.AffectedClassNames, Has.Member("ClasseB"));
        }

        [Test]
        public void DetectCodeSmells_ClassTriggersLargeClassAndLowCohesionSimultaneously_ReturnsBothOccurrences()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("GerenciadorTotal");
            classNode.AddMethod(Method("Metodo0", "Usuario"));
            classNode.AddMethod(Method("Metodo1", "Usuario"));
            classNode.AddMethod(Method("Metodo2", "Notificacao"));
            classNode.AddMethod(Method("Metodo3", "Emprestimo"));
            classNode.AddMethod(Method("Metodo4", "Emprestimo"));
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass));
            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowCohesion));
        }

        [Test]
        public void CalculateScore_OneFulfilledRequirementNoSmells_ReturnsOneHundred()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            diagram.AddClass(classNode);
            var requirement = RequirementFor("reqBiblioteca_04", "Matricular aluno", Method("Matricular", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { requirement });

            Assert.AreEqual(100, result.TotalScore);
        }

        [Test]
        public void CalculateScore_PenaltiesExceedEarnedPoints_ScoreNeverGoesBelowZero()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("GerenciadorTotal");
            for (int i = 0; i < 5; i++)
            {
                classNode.AddMethod(Method($"Metodo{i}", $"Categoria{i}"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.AreEqual(0, result.TotalScore);
        }
    }
}