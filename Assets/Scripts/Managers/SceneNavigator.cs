using System;
using UI;
using UnityEngine.SceneManagement;

namespace Managers
{
    public class SceneNavigator : ISceneNavigationActions
    {
        private readonly string _menuSceneName;
        private readonly Func<bool> _isNetworkMatchProvider;
        private readonly Action _onReturnToMenuRequested;

        public SceneNavigator(
            string menuSceneName = "Menu",
            Func<bool> isNetworkMatchProvider = null,
            Action onReturnToMenuRequested = null)
        {
            _menuSceneName = string.IsNullOrWhiteSpace(menuSceneName) ? "Menu" : menuSceneName;
            _isNetworkMatchProvider = isNetworkMatchProvider ?? (() => false);
            _onReturnToMenuRequested = onReturnToMenuRequested;
        }

        public void RestartMatch()
        {
            if (_isNetworkMatchProvider())
            {
                ReturnToMainMenu();
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
			// Fire the event first so handlers (e.g. MatchSyncBridge) can tear
            // down a running network before the scene is destroyed. Navigation
            // happens unconditionally after: Mirror's StopHost/StopClient do
            // not load a scene, and the previous fallback only ran when the
            // callback was null — which it never is from GameManager.Awake.
            _onReturnToMenuRequested?.Invoke();
            SceneManager.LoadScene(_menuSceneName);
         }
        }
    }
}
