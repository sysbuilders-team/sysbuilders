namespace UI
{
    // Consumido por EndGameModalController, implementado pelo GameManager
    public interface ISceneNavigationActions
    {
        void ReturnToMainMenu();
        void RestartMatch();
    }
}