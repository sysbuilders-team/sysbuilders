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
            return new Requirement(id, description, new List<MethodData>(methods));
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
        public void DetectCodeSmells_ClassWithFiveMethods_DoesNotTriggerLargeClass()
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

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass),
                "Threshold foi ampliado para 5: 5 métodos é válido e não deve disparar LargeClass.");
        }

        [Test]
        public void DetectCodeSmells_ClassWithSixMethods_TriggersLargeClass()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("GerenciadorTotal");
            for (int i = 0; i < 6; i++)
            {
                classNode.AddMethod(Method($"Metodo{i}", "Usuario"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass),
                "6 métodos ultrapassa o threshold de 5 e deve disparar LargeClass.");
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
            classNode.AddMethod(Method("Metodo5", "Emprestimo")); // 6 métodos > threshold 5
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LargeClass));
            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowCohesion));
        }

        [Test]
        public void CalculateScore_OneFulfilledRequirementNoSmells_IncludesCleanBonus()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            diagram.AddClass(classNode);
            var requirement = RequirementFor("reqBiblioteca_04", "Matricular aluno", Method("Matricular", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { requirement });

            // 1 requisito cumprido (100) + Clean Architecture Bonus (50) - 0 penalidades = 150
            Assert.AreEqual(150, result.RawScore);
            Assert.AreEqual(50, result.CleanBonus);
            Assert.AreEqual(0, result.PenaltyScore);
            Assert.AreEqual(150, result.TotalScore);
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

        [Test]
        public void DetectCodeSmells_SameMethodNameInDifferentCategories_DoesNotTriggerDuplicateCode()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classA = new ClassNode("UsuarioService");
            classA.AddMethod(Method("Validar", "Autenticacao"));

            var classB = new ClassNode("PedidoService");
            classB.AddMethod(Method("Validar", "Financeiro"));

            diagram.AddClass(classA);
            diagram.AddClass(classB);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.DuplicateCode));
        }

        [Test]
        public void DetectCodeSmells_ClassWithThreeCategories_ScalesLowCohesionSeverity()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("GodClass");

            classNode.AddMethod(Method("M1", "CatA"));
            classNode.AddMethod(Method("M2", "CatB"));
            classNode.AddMethod(Method("M3", "CatC"));
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            var smell = result.DetectedSmells.SingleOrDefault(s => s.Type == CodeSmellType.LowCohesion);
            Assert.IsNotNull(smell);
            Assert.AreEqual(2, smell.Severity);
        }

        [Test]
        public void Evaluate_SeparatesRawScoreAndPenaltyScoreCorrectly()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Matricular", "Usuario"));
            classNode.AddMethod(Method("EnviarEmail", "Notificacao"));
            diagram.AddClass(classNode);

            var req = RequirementFor("req_matricular", "Matricular Aluno", Method("Matricular", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            // LowCohesion agora tem penalidade calibrada de 30 (era 40)
            Assert.AreEqual(100, result.RawScore);
            Assert.AreEqual(30, result.PenaltyScore);
            Assert.AreEqual(70, result.TotalScore);
            Assert.AreEqual(0, result.CleanBonus, "Com smells ativos não deve receber CleanBonus.");
        }

        [Test]
        public void CheckRequirements_SameMethodNameDifferentCategory_IsNotFulfilled()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();
            var classNode = new ClassNode("Aluno");
            classNode.AddMethod(Method("Validar", "Financeiro"));
            diagram.AddClass(classNode);

            var req = RequirementFor("req_validar_user", "Validar Usuario", Method("Validar", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            Assert.IsEmpty(result.FulfilledRequirementIds);
        }

        [Test]
        public void DetectCodeSmells_LowAffinityCoupling_EmptyClasses_DoesNotTriggerSmell()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classA = new ClassNode("ClasseA");
            var classB = new ClassNode("ClasseB");
            diagram.AddClass(classA);
            diagram.AddClass(classB);
            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling));
        }

        [Test]
        public void DetectCodeSmells_LowAffinityCoupling_SelfRelation_DoesNotTriggerSmell()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classA = new ClassNode("ClasseA");
            classA.AddMethod(Method("M1", "CatA"));
            diagram.AddClass(classA);
            diagram.AddRelation(new Relation(classA, classA, RelationType.Association));

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling));
        }

        [Test]
        public void DetectCodeSmells_DuplicateCode_SameClassInternalDuplicates_DeduplicatesClassNames()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classA = new ClassNode("ClasseA");
            classA.AddMethod(Method("M1", "CatA"));
            classA.AddMethod(Method("M1", "CatA"));
            diagram.AddClass(classA);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.DuplicateCode));
        }

        [Test]
        public void DetectCodeSmells_ClassWithSixMethods_ScalesLargeClassSeverity()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classNode = new ClassNode("BigClass");
            for (int i = 0; i < 6; i++)
            {
                classNode.AddMethod(Method($"M{i}", "CatA"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            var smell = result.DetectedSmells.SingleOrDefault(s => s.Type == CodeSmellType.LargeClass);
            Assert.IsNotNull(smell);
            Assert.AreEqual(1, smell.Severity, "Com threshold 5, 6 métodos gera severidade 6 - 5 = 1.");
        }

        [Test]
        public void DetectCodeSmells_ClassWithSevenMethods_ScalesLargeClassSeverity()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classNode = new ClassNode("BigClass");
            for (int i = 0; i < 7; i++)
            {
                classNode.AddMethod(Method($"M{i}", "CatA"));
            }
            diagram.AddClass(classNode);

            var result = engine.Evaluate(diagram, new List<Requirement>());

            var smell = result.DetectedSmells.SingleOrDefault(s => s.Type == CodeSmellType.LargeClass);
            Assert.IsNotNull(smell);
            Assert.AreEqual(2, smell.Severity, "Com threshold 5, 7 métodos gera severidade 7 - 5 = 2.");
        }

        #region New Tests: Clean Architecture Bonus, Anti-Inertia, Contextual LowAffinityCoupling
        [Test]
        public void Evaluate_CleanArchitectureBonus_DeniedWhenDiagramHasSmells()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classNode = new ClassNode("Leitor");
            classNode.AddMethod(Method("Cadastrar", "Usuario"));
            classNode.AddMethod(Method("Notificar", "Notificacao")); // LowCohesion smell
            diagram.AddClass(classNode);

            var req = RequirementFor("req_01", "Cadastro", Method("Cadastrar", "Usuario"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            Assert.AreEqual(100, result.RawScore);
            Assert.AreEqual(0, result.CleanBonus);
            Assert.AreEqual(30, result.PenaltyScore);
        }

        [Test]
        public void Evaluate_AntiInertia_PenalizesUnallocatedRequirementMethods()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var classNode = new ClassNode("Leitor");
            classNode.AddMethod(Method("Cadastrar", "Usuario"));
            diagram.AddClass(classNode);

            // Requisito exige 4 métodos, mas o jogador só alocou 1 no diagrama
            var req = RequirementFor("req_01", "Turno 1",
                Method("Cadastrar", "Usuario"),
                Method("Atualizar", "Usuario"),
                Method("Consultar", "Usuario"),
                Method("Notificar", "Notificacao"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            // 3 métodos abandonados * 25 = 75 pontos de penalidade
            Assert.AreEqual(75, result.UnallocatedPenalty);
            Assert.AreEqual(75, result.PenaltyScore);
        }

        [Test]
        public void Evaluate_AntiInertia_EmptyDiagramSuffersFullInactivityPenalty()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram(); // AFK / Inativo

            var req = RequirementFor("req_01", "Turno 1",
                Method("Cadastrar", "Usuario"),
                Method("Atualizar", "Usuario"),
                Method("Consultar", "Usuario"),
                Method("Notificar", "Notificacao"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            // 4 métodos exigidos abandonados * 25 = 100 pontos de penalidade
            Assert.AreEqual(100, result.UnallocatedPenalty);
            Assert.AreEqual(100, result.PenaltyScore);
            Assert.AreEqual(0, result.RawScore);
            Assert.AreEqual(0, result.TotalScore);
        }

        [Test]
        public void DetectCodeSmells_LowAffinityCoupling_AllowedWhenRequirementDemandsBothCategories()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(Method("Cadastrar", "Usuario"));

            var notificador = new ClassNode("Notificador");
            notificador.AddMethod(Method("EnviarEmail", "Notificacao"));

            diagram.AddClass(leitor);
            diagram.AddClass(notificador);
            diagram.AddRelation(new Relation(leitor, notificador, RelationType.Association));

            // O Requisito 1 pede Usuario + Notificacao no mesmo caso de uso
            var req = RequirementFor("req_01", "Turno 1",
                Method("Cadastrar", "Usuario"),
                Method("EnviarEmail", "Notificacao"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling),
                "Associação entre classes de categorias diferentes é legítima quando exigida por requisito funcional!");
        }

        [Test]
        public void DetectCodeSmells_LowAffinityCoupling_TriggeredWhenNoRequirementDemandsBothCategories()
        {
            var engine = new EvaluationEngine();
            var diagram = new ClassDiagram();

            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(Method("Cadastrar", "Usuario"));

            var financeiro = new ClassNode("Financeiro");
            financeiro.AddMethod(Method("Cobrar", "Financeiro"));

            diagram.AddClass(leitor);
            diagram.AddClass(financeiro);
            diagram.AddRelation(new Relation(leitor, financeiro, RelationType.Association));

            // O Requisito 1 apenas pede Usuario e Notificacao, nunca pediu Usuario + Financeiro
            var req = RequirementFor("req_01", "Turno 1",
                Method("Cadastrar", "Usuario"),
                Method("EnviarEmail", "Notificacao"));

            var result = engine.Evaluate(diagram, new List<Requirement> { req });

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling),
                "Associação entre classes sem afinidade e sem suporte nos requisitos funcionais ativos deve ser penalizada!");
        }

        [Test]
        public void Evaluate_WithSatisfiedDomainRule_AwardsDomainPoints()
        {
            var diagram = new ClassDiagram();
            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var notificador = new ClassNode("Notificador");
            notificador.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            diagram.AddClass(leitor);
            diagram.AddClass(notificador);
            diagram.AddRelation(new Relation(leitor, notificador, RelationType.Association));

            var rule = new AssociationRule("Usuario", "Notificacao", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>(), new List<AssociationRule> { rule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.AreEqual(25, result.DomainPoints);
            Assert.AreEqual(1, result.SatisfiedAssociations.Count);
        }

        [Test]
        public void Evaluate_WithSameClassDomainRule_SatisfiesWithoutBonus()
        {
            var diagram = new ClassDiagram();
            var godClass = new ClassNode("Tudo");
            godClass.AddMethod(new MethodData("Cadastrar", "Usuario"));
            godClass.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            diagram.AddClass(godClass);

            var rule = new AssociationRule("Usuario", "Notificacao", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>(), new List<AssociationRule> { rule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.AreEqual(0, result.DomainPoints);
            Assert.AreEqual(1, result.SatisfiedAssociations.Count);
        }

        [Test]
        public void Evaluate_RuleWithoutRelation_AwardsZeroPoints()
        {
            var diagram = new ClassDiagram();
            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var notificador = new ClassNode("Notificador");
            notificador.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            diagram.AddClass(leitor);
            diagram.AddClass(notificador);

            var rule = new AssociationRule("Usuario", "Notificacao", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>(), new List<AssociationRule> { rule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.AreEqual(0, result.DomainPoints);
            Assert.AreEqual(0, result.SatisfiedAssociations.Count);
        }

        [Test]
        public void Evaluate_CategorySpreadAcrossTwoClasses_ConnectionOnSecondStillAwardsPoints()
        {
            var diagram = new ClassDiagram();
            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var fichaleitor = new ClassNode("FichaLeitor");
            fichaleitor.AddMethod(new MethodData("Atualizar", "Usuario"));
            var emprestimo = new ClassNode("Emprestimo");
            emprestimo.AddMethod(new MethodData("Registrar", "Circulacao"));
            
            diagram.AddClass(leitor);
            diagram.AddClass(fichaleitor);
            diagram.AddClass(emprestimo);
            diagram.AddRelation(new Relation(fichaleitor, emprestimo, RelationType.Association));

            var rule = new AssociationRule("Usuario", "Circulacao", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>(), new List<AssociationRule> { rule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.AreEqual(25, result.DomainPoints);
            Assert.AreEqual(1, result.SatisfiedAssociations.Count);
        }

        [Test]
        public void Evaluate_SameRuleInTwoActiveRequirements_AwardsOnlyOnce()
        {
            var diagram = new ClassDiagram();
            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var notificador = new ClassNode("Notificador");
            notificador.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            diagram.AddClass(leitor);
            diagram.AddClass(notificador);
            diagram.AddRelation(new Relation(leitor, notificador, RelationType.Association));

            var rule1 = new AssociationRule("Usuario", "Notificacao", 25);
            var rule2 = new AssociationRule("Usuario", "Notificacao", 25);
            var req1 = new Requirement("r1", "Req1", new List<MethodData>(), new List<AssociationRule> { rule1 });
            var req2 = new Requirement("r2", "Req2", new List<MethodData>(), new List<AssociationRule> { rule2 });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req1, req2 });

            Assert.AreEqual(25, result.DomainPoints);
            Assert.AreEqual(1, result.SatisfiedAssociations.Count);
        }

        [Test]
        public void DetectCodeSmells_DomainRuleRequiredRelation_DoesNotTriggerLowAffinityCoupling()
        {
            var diagram = new ClassDiagram();
            var classA = new ClassNode("A");
            classA.AddMethod(new MethodData("M1", "CatX"));
            var classB = new ClassNode("B");
            classB.AddMethod(new MethodData("M2", "CatY"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);
            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));

            var rule = new AssociationRule("CatX", "CatY");
            var req = new Requirement("r1", "Req", new List<MethodData>(), new List<AssociationRule> { rule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.IsFalse(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling));
        }

        [Test]
        public void DetectCodeSmells_UnjustifiedRelationWithoutRule_StillTriggersLowAffinityCoupling()
        {
            var diagram = new ClassDiagram();
            var classA = new ClassNode("A");
            classA.AddMethod(new MethodData("M1", "CatX"));
            var classB = new ClassNode("B");
            classB.AddMethod(new MethodData("M2", "CatY"));
            diagram.AddClass(classA);
            diagram.AddClass(classB);
            diagram.AddRelation(new Relation(classA, classB, RelationType.Association));

            // Requirement specifies an association for another pair, so CatX -> CatY has no affinity rule
            var otherRule = new AssociationRule("CatX", "CatZ", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>
            {
                new MethodData("M1", "CatX"),
                new MethodData("M2", "CatY")
            }, new List<AssociationRule> { otherRule });

            var result = new EvaluationEngine().Evaluate(diagram, new List<Requirement> { req });

            Assert.IsTrue(result.DetectedSmells.Any(s => s.Type == CodeSmellType.LowAffinityCoupling));
        }

        [Test]
        public void RankingTest_GoodDesignBeatsConnectAllAndGodClass()
        {
            var rule1 = new AssociationRule("Usuario", "Notificacao", 25);
            var rule2 = new AssociationRule("Acervo", "Notificacao", 25);
            var req = new Requirement("r1", "Req", new List<MethodData>
            {
                new MethodData("Cadastrar", "Usuario"),
                new MethodData("EnviarEmail", "Notificacao"),
                new MethodData("CadastrarLivro", "Acervo")
            }, new List<AssociationRule> { rule1, rule2 });

            var good = new ClassDiagram();
            var leitor = new ClassNode("Leitor");
            leitor.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var notificador = new ClassNode("Notificador");
            notificador.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            var acervo = new ClassNode("Acervo");
            acervo.AddMethod(new MethodData("CadastrarLivro", "Acervo"));
            good.AddClass(leitor);
            good.AddClass(notificador);
            good.AddClass(acervo);
            good.AddRelation(new Relation(leitor, notificador, RelationType.Association));
            good.AddRelation(new Relation(acervo, notificador, RelationType.Association));
            var goodResult = new EvaluationEngine().Evaluate(good, new List<Requirement> { req });

            var god = new ClassDiagram();
            var godClass = new ClassNode("Tudo");
            godClass.AddMethod(new MethodData("Cadastrar", "Usuario"));
            godClass.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            godClass.AddMethod(new MethodData("CadastrarLivro", "Acervo"));
            god.AddClass(godClass);
            var godResult = new EvaluationEngine().Evaluate(god, new List<Requirement> { req });

            var all = new ClassDiagram();
            var a = new ClassNode("A");
            a.AddMethod(new MethodData("Cadastrar", "Usuario"));
            var b = new ClassNode("B");
            b.AddMethod(new MethodData("EnviarEmail", "Notificacao"));
            var c = new ClassNode("C");
            c.AddMethod(new MethodData("CadastrarLivro", "Acervo"));
            all.AddClass(a);
            all.AddClass(b);
            all.AddClass(c);
            all.AddRelation(new Relation(a, b, RelationType.Association));
            all.AddRelation(new Relation(a, c, RelationType.Association));
            all.AddRelation(new Relation(b, c, RelationType.Association));
            var allResult = new EvaluationEngine().Evaluate(all, new List<Requirement> { req });

            Assert.Greater(goodResult.TotalScore, godResult.TotalScore);
            Assert.Greater(goodResult.TotalScore, allResult.TotalScore);
        }
        #endregion
    }
}
