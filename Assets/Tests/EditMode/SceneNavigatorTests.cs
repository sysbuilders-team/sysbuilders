using System;
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

            // ReturnToMainMenu fires the callback first, then calls
            // SceneManager.LoadScene. In EditMode the scene name is not in
            // build settings, so LoadScene throws. The callback assertion is
            // what this test actually cares about.
            try { navigator.ReturnToMainMenu(); }
            catch (Exception) { }

            Assert.IsTrue(invoked);
        }

        [Test]
        public void RestartMatch_WhenNetworkMatch_CallsReturnToMainMenu()
        {
            bool returnInvoked = false;
            var navigator = new SceneNavigator("Menu", () => true, () => returnInvoked = true);

            try { navigator.RestartMatch(); }
            catch (Exception) { }

            Assert.IsTrue(returnInvoked);
        }
    }
}
