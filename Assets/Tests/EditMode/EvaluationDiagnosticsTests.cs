using System;
using System.Collections.Generic;
using System.Linq;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Regressoes dos defeitos encontrados no diagnostico do motor de avaliacao:
    /// EVAL-01 (requisito vazio), EVAL-02 (Ids duplicados), EVAL-05 (dedup de par),
    /// EVAL-06 (smell sem penalidade), EVAL-07 (aliasing), EVAL-09 (severidade de duplicacao),
    /// EVAL-10 (ClassDiagram) e EVAL-11 (duracao da partida).
    /// </summary>
    [TestFixture]
    public class EvaluationDiagnosticsTests
    {
        private static Requirement RequirementWith(string id, params MethodData[] methods)
        {
            return new Requirement(id, id, new List<MethodData>(methods));
        }

        // ---------------------------------------------------------------- EVAL-01
        [Test]
        public void CheckRequirements_RequirementWithoutMethods_IsNotFulfilled()
        {
            var engine = new EvaluationEngine();
            var requirement = RequirementWith("r_vazio");

            var result = engine.Evaluate(new ClassDiagram(), new List<Requirement> { requirement });

            Assert.IsEmpty(result.FulfilledRequirementIds);
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.TotalScore);
        }

        [Test]
        public void CheckRequirements_RequirementWithMethodsInEmptyDiagram_IsNotFulfilled()
        {
            var engine = new EvaluationEngine();
            var requirement = RequirementWith("r1", new MethodData("Matricular", "Usuario"));

            var result = engine.Evaluate(new ClassDiagram(), new List<Requirement> { requirement });

            Assert.IsEmpty(result.FulfilledRequirementIds);
        }

        [Test]
        public void CheckRequirements_MethodsInDifferentButAssociatedClasses_IsFulfilled()
        {
            var diagram = new ClassDiagram();
            var classA = new ClassNode("A");
            classA.AddMethod(new MethodData("Matricular", "Usuario"));
            var classB = new ClassNode("B");
            classB.AddMethod(new MethodData("Cancelar", "Usuario"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);
            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));

            var requirement = RequirementWith("r1",
                new MethodData("Matricular", "Usuario"),
                new MethodData("Cancelar", "Usuario"));

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { requirement });

            Assert.AreEqual(1, result.FulfilledRequirementIds.Count);
        }

        // ---------------------------------------------------------------- EVAL-02
        [Test]
        public void Evaluate_DuplicateRequirementIds_CountsBothFulfilledRequirements()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(new MethodData("Matricular", "Usuario"));
            diagram.AddClass(classNode);

            var sharedMethods = new List<MethodData> { new MethodData("Matricular", "Usuario") };
            var requirements = new List<Requirement>
            {
                new Requirement("req_01", "Primeiro requisito", sharedMethods),
                new Requirement("req_01", "Segundo requisito com o mesmo Id", sharedMethods),
            };

            var result = engine.Evaluate(diagram, requirements);

            Assert.AreEqual(2, result.FulfilledRequirementIds.Count);
            Assert.AreEqual(200, result.RawScore);
        }

        // ---------------------------------------------------------------- EVAL-05
        [Test]
        public void DetectCodeSmells_BidirectionalAssociationBetweenSamePair_ReportsSingleCouplingSmell()
        {
            var diagram = new ClassDiagram();
            var classA = new ClassNode("A");
            classA.AddMethod(new MethodData("m1", "CatX"));
            var classB = new ClassNode("B");
            classB.AddMethod(new MethodData("m2", "CatY"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);

            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));
            diagram.AddRelation(new Relation(classB, classA, RelationType.Association));

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement>());
            var coupling = result.DetectedSmells.Where(s => s.Type == CodeSmellType.LowAffinityCoupling).ToList();

            Assert.AreEqual(1, coupling.Count);
            Assert.AreEqual(30, result.PenaltyScore);
        }

        // ---------------------------------------------------------------- EVAL-09
        [Test]
        public void DetectCodeSmells_DuplicateCodeAcrossThreeClasses_ScalesSeverity()
        {
            var diagram = new ClassDiagram();
            foreach (var className in new[] { "A", "B", "C" })
            {
                var classNode = new ClassNode(className);
                classNode.AddMethod(new MethodData("Validar", "Usuario"));
                diagram.AddClass(classNode);
            }

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement>());
            var duplicate = result.DetectedSmells.Single(s => s.Type == CodeSmellType.DuplicateCode);

            Assert.AreEqual(3, duplicate.AffectedClassNames.Count);
            Assert.AreEqual(2, duplicate.Severity);
            Assert.AreEqual(100, result.PenaltyScore);
        }

        [Test]
        public void DetectCodeSmells_DuplicateCodeAcrossTwoClasses_KeepsSeverityOne()
        {
            var diagram = new ClassDiagram();
            foreach (var className in new[] { "A", "B" })
            {
                var classNode = new ClassNode(className);
                classNode.AddMethod(new MethodData("Validar", "Usuario"));
                diagram.AddClass(classNode);
            }

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement>());
            var duplicate = result.DetectedSmells.Single(s => s.Type == CodeSmellType.DuplicateCode);

            Assert.AreEqual(1, duplicate.Severity);
            Assert.AreEqual(50, result.PenaltyScore);
        }

        // ---------------------------------------------------------------- EVAL-06
        [Test]
        public void CalculatePenalty_EveryCodeSmellType_HasMappedPenalty()
        {
            var calculator = new SmellPenaltyCalculator();

            foreach (CodeSmellType type in Enum.GetValues(typeof(CodeSmellType)))
            {
                // DuplicateCode exige ao menos duas classes afetadas.
                var affected = type == CodeSmellType.DuplicateCode
                    ? new List<string> { "A", "B" }
                    : new List<string> { "A" };

                var smells = new List<SmellOccurrence> { new SmellOccurrence(type, affected) };

                Assert.DoesNotThrow(() => calculator.CalculatePenalty(smells),
                    $"CodeSmellType.{type} nao possui penalidade mapeada.");
                Assert.Greater(calculator.CalculatePenalty(smells), 0,
                    $"CodeSmellType.{type} deveria penalizar.");
            }
        }

        [Test]
        public void CalculatePenalty_UnknownSmellType_Throws()
        {
            var calculator = new SmellPenaltyCalculator();
            var unknown = new SmellOccurrence((CodeSmellType)99, new List<string> { "A" });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => calculator.CalculatePenalty(new List<SmellOccurrence> { unknown }));
        }

        // ---------------------------------------------------------------- EVAL-07
        [Test]
        public void EvaluationResult_WhenCallerMutatesInputCollections_StaysUnchanged()
        {
            var ids = new List<string> { "r1" };
            var smells = new List<SmellOccurrence>
            {
                new SmellOccurrence(CodeSmellType.LargeClass, new List<string> { "A" })
            };

            var result = new EvaluationResult(100, 0, ids, smells);

            ids.Add("r2");
            smells.Clear();

            Assert.AreEqual(1, result.FulfilledRequirementIds.Count);
            Assert.AreEqual(1, result.DetectedSmells.Count);
        }

        [Test]
        public void SmellOccurrence_WhenCallerMutatesAffectedClassNames_StaysUnchanged()
        {
            var names = new List<string> { "A", "B" };
            var smell = new SmellOccurrence(CodeSmellType.DuplicateCode, names);

            names.Add("C");

            Assert.AreEqual(2, smell.AffectedClassNames.Count);
        }

        // ---------------------------------------------------------------- EVAL-10
        [Test]
        public void ClassDiagram_AddingDuplicateName_ReturnsFalseAndKeepsSingleNode()
        {
            var diagram = new ClassDiagram();

            Assert.IsTrue(diagram.AddClass(new ClassNode("Aluno")));
            Assert.IsFalse(diagram.AddClass(new ClassNode("Aluno")));
            Assert.AreEqual(1, diagram.Classes.Count);
            Assert.AreEqual("Aluno", diagram.FindClassByName("Aluno").Name);
        }

        [Test]
        public void ClassDiagram_RemoveClass_DropsRelationsAndFreesName()
        {
            var diagram = new ClassDiagram();
            var classA = new ClassNode("A");
            var classB = new ClassNode("B");
            diagram.AddClass(classA);
            diagram.AddClass(classB);
            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));

            Assert.IsTrue(diagram.RemoveClass(classA));
            Assert.IsFalse(diagram.RemoveClass(classA));
            Assert.AreEqual(0, diagram.Relations.Count);
            Assert.IsNull(diagram.FindClassByName("A"));
            Assert.IsTrue(diagram.AddClass(new ClassNode("A")));
        }

        [Test]
        public void GetConnectedComponents_ChainOfClasses_ReturnsSingleComponent()
        {
            var diagram = new ClassDiagram();
            var nodes = new List<ClassNode>();
            for (int i = 0; i < 5; i++)
            {
                var node = new ClassNode("C" + i);
                diagram.AddClass(node);
                nodes.Add(node);
            }
            for (int i = 1; i < nodes.Count; i++)
            {
                diagram.AddRelation(new Relation(nodes[i - 1], nodes[i], RelationType.Association));
            }

            var components = diagram.GetConnectedComponents();

            Assert.AreEqual(1, components.Count);
            Assert.AreEqual(5, components[0].Count);
        }

        [Test]
        public void GetConnectedComponents_UnconnectedClasses_ReturnsOneComponentPerClass()
        {
            var diagram = new ClassDiagram();
            diagram.AddClass(new ClassNode("A"));
            diagram.AddClass(new ClassNode("B"));

            var components = diagram.GetConnectedComponents();

            Assert.AreEqual(2, components.Count);
            Assert.IsTrue(components.All(c => c.Count == 1));
        }

        [Test]
        public void GetConnectedComponents_RelationToUnregisteredClass_IsIgnored()
        {
            var diagram = new ClassDiagram();
            var registered = new ClassNode("Registrada");
            var notRegistered = new ClassNode("ForaDoDiagrama");
            diagram.AddClass(registered);
            diagram.AddRelation(new Relation(registered, notRegistered, RelationType.Association));

            var components = diagram.GetConnectedComponents();

            Assert.AreEqual(1, components.Count);
            Assert.AreEqual(1, components[0].Count);
            Assert.AreEqual("Registrada", components[0][0].Name);
        }

        // ---------------------------------------------------------------- EVAL-11
        [Test]
        public void MatchSession_WithRequirementCountOtherThanFive_Throws()
        {
            var fourRequirements = new List<Requirement>();
            for (int i = 0; i < 4; i++)
            {
                fourRequirements.Add(RequirementWith($"r{i}", new MethodData("m", "c")));
            }

            Assert.Throws<ArgumentException>(
                () => new MatchSession("A", "B", fourRequirements, PlayerSlot.PlayerOne));
        }

        [Test]
        public void TurnManager_MaxTurnsFollowsTrailTurnCount()
        {
            Assert.AreEqual(Trail.TurnCount, new TurnManager().MaxTurns);
            Assert.AreEqual(3, new TurnManager(maxTurns: 3).MaxTurns);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = new TurnManager(maxTurns: 0);
            });
        }

        // ---------------------------------------------------------------- EVAL-12
        [Test]
        public void MatchSession_TryCreateClass_WithDuplicateName_ReturnsFalseAndKeepsSingleNode()
        {
            var requirements = new List<Requirement>();
            for (int i = 0; i < Trail.TurnCount; i++)
            {
                requirements.Add(RequirementWith($"r{i}", new MethodData("m", "c")));
            }

            var session = new MatchSession("A", "B", requirements, PlayerSlot.PlayerOne);
            session.TurnManager.ForceAdvancePhase();   // Show -> Build (fase em que o diagrama aceita edicao)

            Assert.IsTrue(session.TryCreateClass("Aluno", out var first));
            Assert.IsFalse(session.TryCreateClass("Aluno", out var second));
            Assert.IsNull(second);
            Assert.AreEqual(1, session.LocalPlayer.Diagram.Classes.Count);
            Assert.AreEqual(first, session.LocalPlayer.Diagram.FindClassByName("Aluno"));
        }
    }
}
