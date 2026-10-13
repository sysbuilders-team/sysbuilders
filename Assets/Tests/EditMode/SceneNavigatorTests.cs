using Managers;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class SceneNavigatorTests
    {
        [Test]
        public void ReturnToMainMenu_WhenCustomActionProvided_InvokesCallback()
        {
            bool invoked = false;
            var navigator = new SceneNavigator("Menu", () => false, () => invoked = true);

            navigator.ReturnToMainMenu();

            Assert.IsTrue(invoked);
        }

        [Test]
        public void RestartMatch_WhenNetworkMatch_CallsReturnToMainMenu()
        {
            bool returnInvoked = false;
            var navigator = new SceneNavigator("Menu", () => true, () => returnInvoked = true);

            navigator.RestartMatch();

            Assert.IsTrue(returnInvoked);
        }
    }
}
