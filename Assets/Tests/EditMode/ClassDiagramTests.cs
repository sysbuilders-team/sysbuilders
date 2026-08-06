using Core.Diagram;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class ClassDiagramTests
    {
        [Test]
        public void AddClass_And_FindClassByName_ShouldReturnCorrectNode()
        {
            // Arrange: Prepara o cenário
            var diagram = new ClassDiagram();
            var expectedNode = new ClassNode("ControleEmprestimo");

            // Act: Executa a ação
            diagram.AddClass(expectedNode);
            var result = diagram.FindClassByName("ControleEmprestimo");

            // Assert: Verifica se o resultado é o esperado
            Assert.IsNotNull(result, "O diagrama deveria encontrar a classe adicionada.");
            Assert.AreEqual(expectedNode, result, "A classe retornada deve ser a exata mesma instância adicionada.");
        }

        [Test]
        public void FindClassByName_WhenClassNotFound_ShouldReturnNull()
        {
            // Arrange
            var diagram = new ClassDiagram();
            diagram.AddClass(new ClassNode("Usuario"));

            // Act
            var result = diagram.FindClassByName("Biblioteca");

            // Assert
            Assert.IsNull(result, "Buscar uma classe inexistente deve retornar null.");
        }
    }
}