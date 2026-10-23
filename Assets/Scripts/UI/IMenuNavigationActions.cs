using System;

namespace UI
{
    public enum MenuScreen
    {
        Main,
        LobbyBrowser,
        RoomWait,
        Settings,
        Tutorial
    }

    /// <summary>
    /// Porta de intenção de navegação entre telas/painéis do Menu.
    /// As controllers e serviços apenas declaram para qual tela desejam ir;
    /// o MenuNavigator centraliza a ativação/desativação dos painéis.
    /// </summary>
    public interface IMenuNavigationActions
    {
        MenuScreen CurrentScreen { get; }
        event Action<MenuScreen> OnScreenChanged;
        void ShowScreen(MenuScreen screen);
    }
}
