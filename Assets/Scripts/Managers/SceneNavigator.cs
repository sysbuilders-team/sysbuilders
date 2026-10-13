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
            if (_onReturnToMenuRequested != null)
            {
                _onReturnToMenuRequested.Invoke();
            }
            else
            {
                SceneManager.LoadScene(_menuSceneName);
            }
        }
    }
}
