using NUnit.Framework;
using Core.Diagram;
using Core.Turns;

namespace Tests.EditMode
{
    public class TurnManagerTests
    {
        [Test]
        public void AdvanceTurn_WhenClassIsStillUnlocked()
        {
            var diagram = new ClassDiagram();
            var turn = new TurnManager();

            diagram.AddClass(new ClassNode("User"));

            var result = turn.AdvanceTurn(diagram);

            Assert.AreEqual(TurnManager.AdvanceTurnResult.ClassesStillUnlocked, result,
                "AdvanceTurn deveria falhar com ClassesStillUnlocked se existem classes ainda destravadas.");
            Assert.AreEqual(1, turn.CurrentTurn, "CurrentTurn não deveria mudar quando AdvanceTurn falha.");
        }

        [Test]
        public void AdvanceTurn_WhenAllClassesAreLocked()
        {
            var diagram = new ClassDiagram();
            var turn = new TurnManager();

            var userClass = new ClassNode("User");
            userClass.Lock();
            diagram.AddClass(userClass);

            var result = turn.AdvanceTurn(diagram);

            Assert.AreEqual(TurnManager.AdvanceTurnResult.Success, result,
                "AdvanceTurn deveria ter sucesso quando todas as classes estão locked.");
            Assert.AreEqual(2, turn.CurrentTurn, "CurrentTurn deveria incrementar após um AdvanceTurn bem-sucedido.");
        }

        [Test]
        public void AdvanceTurn_WhenDiagramIsNull()
        {
            var turn = new TurnManager();

            var result = turn.AdvanceTurn(null);

            Assert.AreEqual(TurnManager.AdvanceTurnResult.DiagramIsNull, result,
                "AdvanceTurn deveria retornar DiagramIsNull para um diagrama nulo.");
            Assert.AreEqual(1, turn.CurrentTurn, "CurrentTurn não deveria mudar quando o diagrama é nulo.");
        }

        [Test]
        public void AdvanceTurn_OnFifthTurn()
        {
            var turn = new TurnManager();

            for (int i = 0; i < 4; i++)
            {
                var diagram = new ClassDiagram();
                var lockedClass = new ClassNode("Class" + i);
                lockedClass.Lock();
                diagram.AddClass(lockedClass);

                turn.AdvanceTurn(diagram);
            }

            Assert.AreEqual(5, turn.CurrentTurn, "Após 4 avanços a partir do turno 1, deveríamos estar no turno 5.");
            Assert.AreEqual(TurnManager.TurnState.Active, turn.State, "A partida ainda não deveria ter acabado antes do 5º avanço.");

            var finalDiagram = new ClassDiagram();
            var finalClass = new ClassNode("FinalClass");
            finalClass.Lock();
            finalDiagram.AddClass(finalClass);

            var result = turn.AdvanceTurn(finalDiagram);

            Assert.AreEqual(TurnManager.AdvanceTurnResult.Success, result,
                "O 5º avanço deveria ter sucesso e encerrar a partida.");
            Assert.AreEqual(5, turn.CurrentTurn, "CurrentTurn não deveria passar de 5, só o State deveria mudar para Ended.");
            Assert.AreEqual(TurnManager.TurnState.Ended, turn.State, "Após o 5º avanço, o State deveria ser Ended.");
        }

        [Test]
        public void AdvanceTurn_WhenMatchAlreadyEnded()
        {
            var turn = new TurnManager();

            for (int i = 0; i < 5; i++)
            {
                var diagram = new ClassDiagram();
                var lockedClass = new ClassNode("Class" + i);
                lockedClass.Lock();
                diagram.AddClass(lockedClass);

                turn.AdvanceTurn(diagram);
            }

            Assert.AreEqual(TurnManager.TurnState.Ended, turn.State, "A partida deveria estar Ended após 5 avanços.");

            var extraDiagram = new ClassDiagram();
            var extraClass = new ClassNode("ExtraClass");
            extraClass.Lock();
            extraDiagram.AddClass(extraClass);

            var result = turn.AdvanceTurn(extraDiagram);

            Assert.AreEqual(TurnManager.AdvanceTurnResult.MatchAlreadyEnded, result,
                "Chamar AdvanceTurn após o fim da partida deveria retornar MatchAlreadyEnded.");
            Assert.AreEqual(5, turn.CurrentTurn, "CurrentTurn não deveria mudar após o fim da partida.");
        }
    }
}