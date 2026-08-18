using NUnit.Framework;
using Core.Diagram;
using Core.Turns;

namespace Tests.EditMode
{
    public class TurnManagerTests
    {
        [Test]
        public void AdvanceTurnTestFalse()
        {
            var diagram = new ClassDiagram();
            var turn =  new TurnManager();
            
            diagram.AddClass("User");//nao completo, terminar depois
            
            turn.AdvanceTurn(diagram);
            
            var result = turn.State
                
            Assert.AreEqual();    
        }

        [Test]
        public void AdvanceTurnTestTrue()
        {
                
        }
        
    }
}