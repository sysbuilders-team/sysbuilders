using System;
using Core.Discovery;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Centralizador único de navegação entre os painéis da MenuScene.
    /// Nenhuma controller ou serviço de rede manipula SetActive de painéis diretamente;
    /// todas as intenções de transição passam por ShowScreen.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuNavigator : MonoBehaviour, IMenuNavigationActions
    {
        [Header("Painéis do Menu")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _lobbyBrowserPanel;
        [SerializeField] private GameObject _roomWaitPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Fonte de Estado da Sala (Opcional)")]
        [Tooltip("Deixe vazio para resolver automaticamente o IRoomLobbySource na cena.")]
        [SerializeField] private MonoBehaviour _lobbySourceBehaviour;

        private IRoomLobbySource _lobbySource;
        private IRoomLobbySource _subscribedSource;

        private bool _wasInRoom;

        public MenuScreen CurrentScreen { get; private set; } = MenuScreen.Main;

        public event Action<MenuScreen> OnScreenChanged;

        public void ConfigurePanels(
            GameObject mainPanel,
            GameObject lobbyBrowserPanel,
            GameObject roomWaitPanel,
            GameObject settingsPanel = null,
            GameObject tutorialPanel = null)
        {
            _mainPanel = mainPanel;
            _lobbyBrowserPanel = lobbyBrowserPanel;
            _roomWaitPanel = roomWaitPanel;
            _settingsPanel = settingsPanel;
            _tutorialPanel = tutorialPanel;
        }

        public void SetLobbySource(IRoomLobbySource lobbySource)
        {
            UnsubscribeLobbySource();

            _lobbySource = lobbySource;
            _wasInRoom = IsAlive(lobbySource) && lobbySource.IsInRoom;

            if (_lobbySource != null)
            {
                _lobbySource.OnSlotsChanged += HandleLobbyStateChanged;
                _subscribedSource = _lobbySource;
            }
        }

        private void Awake()
        {
            EnsureLobbySource();
            _wasInRoom = IsAlive(_lobbySource) && _lobbySource.IsInRoom;

            ShowScreen(_wasInRoom ? MenuScreen.RoomWait : MenuScreen.Main);
        }

        private void Update()
        {
            EnsureLobbySource();
            SyncScreenWithRoomState();
        }

        private void OnEnable()
        {
            EnsureLobbySource();
        }

        private void OnDestroy()
        {
            UnsubscribeLobbySource();
        }

        private void UnsubscribeLobbySource()
        {
            if (_subscribedSource == null) return;
            _subscribedSource.OnSlotsChanged -= HandleLobbyStateChanged;
            _subscribedSource = null;
        }

        private void HandleLobbyStateChanged() => SyncScreenWithRoomState();

        public void ShowScreen(MenuScreen screen)
        {
            CurrentScreen = screen;

            if (_mainPanel != null) _mainPanel.SetActive(screen == MenuScreen.Main);
            if (_lobbyBrowserPanel != null) _lobbyBrowserPanel.SetActive(screen == MenuScreen.LobbyBrowser);
            if (_roomWaitPanel != null) _roomWaitPanel.SetActive(screen == MenuScreen.RoomWait);
            if (_settingsPanel != null) _settingsPanel.SetActive(screen == MenuScreen.Settings);
            if (_tutorialPanel != null) _tutorialPanel.SetActive(screen == MenuScreen.Tutorial);

            OnScreenChanged?.Invoke(screen);
        }

        public void ShowMain() => ShowScreen(MenuScreen.Main);
        public void ShowLobbyBrowser() => ShowScreen(MenuScreen.LobbyBrowser);
        public void ShowRoomWait() => ShowScreen(MenuScreen.RoomWait);
        public void ShowSettings() => ShowScreen(MenuScreen.Settings);
        public void ShowTutorial() => ShowScreen(MenuScreen.Tutorial);

        private void SyncScreenWithRoomState()
        {
            if (!IsAlive(_lobbySource)) return;

            bool inRoom = _lobbySource.IsInRoom;
            if (inRoom == _wasInRoom) return;

            _wasInRoom = inRoom;

            if (inRoom)
            {
                if (CurrentScreen != MenuScreen.RoomWait) ShowScreen(MenuScreen.RoomWait);
            }
            else if (CurrentScreen == MenuScreen.RoomWait)
            {
                ShowScreen(MenuScreen.Main);
            }
        }
        private void EnsureLobbySource()
        {
            if(IsAlive(_lobbySource)) return;

            UnsubscribeLobbySource();
            _lobbySource = null;

            var resolved = _lobbySourceBehaviour as IRoomLobbySource;
            if (!IsAlive(resolved)) resolved = FindPort<IRoomLobbySource>();
            if (resolved != null) SetLobbySource(resolved);
        }

        private static bool IsAlive(IRoomLobbySource source)
            => source != null && (source is not UnityEngine.Object unityObject  || unityObject != null);

        private static TPort FindPort<TPort>() where TPort : class
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is TPort port) return port;
            }

            return null;
        }
    }
}
