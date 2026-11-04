using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Fluxo visual do menu: abas laterais, cards de tema e botões de conexão.
    /// Também mantém o breadcrumb e impede que os painéis nativos de lobby/sala
    /// fiquem sobrepostos às abas Tutorial/Opções/Sair.
    /// </summary>
    public class SysBuildersMenuFlow : MonoBehaviour
    {
        [Header("MenuUIController & Navigator")]
        [SerializeField] private MenuUIController _menuUIController;
        [SerializeField] private MenuNavigator _navigator;

        [Header("Breadcrumb")]
        [SerializeField] private TMP_Text _crumbText;

        [Header("Abas Laterais")]
        [SerializeField] private MenuSidebarTab _tabJogar;
        [SerializeField] private MenuSidebarTab _tabTutorial;
        [SerializeField] private MenuSidebarTab _tabOpcoes;
        [SerializeField] private MenuSidebarTab _tabSair;

        [Header("Cards de Tema")]
        [SerializeField] private MenuThemeCard[] _themeCards;

        [Header("Telas Principais (Panels)")]
        [SerializeField] private GameObject _panelJogar;
        [SerializeField] private GameObject _panelTutorial;
        [SerializeField] private GameObject _panelOpcoes;
        [SerializeField] private GameObject _panelSair;

        [Header("Painéis Nativos de Rede")]
        [SerializeField] private GameObject _panelLobbyNative;
        [SerializeField] private GameObject _panelRoomNative;

        [Header("Botões de Conexão")]
        [SerializeField] private Button _btnCreate;
        [SerializeField] private Button _btnSearch;
        [SerializeField] private Button _btnJoin;
        [SerializeField] private Button _btnQuitReturn;

        [Header("Botões de Lobby / Sala")]
        [SerializeField] private Button _btnBackLobby;
        [SerializeField] private Button _btnExitRoom;

        private string _selectedTheme = "Biblioteca";
        private string _currentSection = "jogar";

        private void Start()
        {
            if (_menuUIController == null)
                _menuUIController = FindAnyObjectByType<MenuUIController>(FindObjectsInactive.Include);

            if (_navigator == null)
                _navigator = FindAnyObjectByType<MenuNavigator>(FindObjectsInactive.Include);

            WireTabs();
            WireThemeCards();
            WireConnectionButtons();

            if (_navigator != null)
            {
                _navigator.OnScreenChanged += HandleScreenChanged;
            }

            string savedTheme = PlayerPrefs.GetString("SelectedTheme", "Biblioteca");
            SelectTheme(savedTheme);
            ShowSection("jogar");
        }

        private void OnDestroy()
        {
            if (_navigator != null)
            {
                _navigator.OnScreenChanged -= HandleScreenChanged;
            }
        }

        private void WireTabs()
        {
            WireTab(_tabJogar, "jogar", false);
            WireTab(_tabTutorial, "tutorial", false);
            WireTab(_tabOpcoes, "opcoes", false);
            WireTab(_tabSair, "sair", true);
        }

        private void WireTab(MenuSidebarTab tab, string section, bool isExit)
        {
            if (tab == null) return;

            tab.Setup(
                tab.GetComponent<Button>(),
                tab.transform.Find("Visual/Body")?.GetComponent<Image>(),
                tab.transform.Find("Visual/Tab")?.GetComponent<Image>(),
                tab.transform.Find("Visual/IconBox/Icon")?.GetComponent<Image>(),
                tab.transform.Find("Visual/Label")?.GetComponent<TMP_Text>(),
                isExit,
                () => ShowSection(section),
                tab.transform.Find("Visual") as RectTransform,
                tab.transform.Find("Visual/IconBox")?.GetComponent<Image>());
        }

        private void WireThemeCards()
        {
            if (_themeCards == null) return;

            foreach (var card in _themeCards)
            {
                if (card == null) continue;

                // Fallback: se o nome serializado estiver vazio, usa o texto do título.
                string themeName = card.ThemeName;
                if (string.IsNullOrWhiteSpace(themeName))
                {
                    var title = card.transform.Find("Surface/Content/Title")?.GetComponent<TMP_Text>();
                    if (title != null) themeName = title.text.Trim();
                    card.SetThemeName(themeName);
                }

                card.Setup(
                    themeName,
                    card.GetComponent<Button>(),
                    card.transform.Find("Surface")?.GetComponent<Image>(),
                    card.transform.Find("Surface/Content/IconRow/Icon")?.GetComponent<Image>(),
                    card.transform.Find("Surface/Content/Title")?.GetComponent<TMP_Text>(),
                    card.transform.Find("Surface/Content/Desc")?.GetComponent<TMP_Text>(),
                    card.transform.Find("Surface/TagSelected")?.gameObject,
                    card.GetComponent<Image>(),
                    SelectTheme);
            }
        }

        private void WireConnectionButtons()
        {
            if (_btnCreate != null)
            {
                _btnCreate.onClick.RemoveAllListeners();
                _btnCreate.onClick.AddListener(OnCreateRoom);
            }

            if (_btnSearch != null)
            {
                _btnSearch.onClick.RemoveAllListeners();
                _btnSearch.onClick.AddListener(OnSearchGames);
            }

            if (_btnJoin != null)
            {
                _btnJoin.onClick.RemoveAllListeners();
                _btnJoin.onClick.AddListener(OnJoinByIp);
            }

            if (_btnQuitReturn != null)
            {
                _btnQuitReturn.onClick.RemoveAllListeners();
                _btnQuitReturn.onClick.AddListener(() => ShowSection("jogar"));
            }

            if (_btnBackLobby != null)
            {
                _btnBackLobby.onClick.RemoveAllListeners();
                _btnBackLobby.onClick.AddListener(OnBackFromLobby);
            }

            if (_btnExitRoom != null)
            {
                _btnExitRoom.onClick.RemoveAllListeners();
                _btnExitRoom.onClick.AddListener(OnExitRoom);
            }
        }

        private void OnBackFromLobby()
        {
            _menuUIController?.PararBusca();
            _navigator?.ShowMain();
        }

        private void OnExitRoom()
        {
            _menuUIController?.SairDaSala();
        }

        public void ShowSection(string section)
        {
            if (string.IsNullOrWhiteSpace(section)) section = "jogar";
            _currentSection = section;

            // Jogar é a tela "raiz" do MenuNavigator (lobby e sala são sub-telas dela).
            // Mesmo ao trocar para outra aba, volta o navegador para Main: assim lobby/sala
            // não ficam "presos" por trás e o breadcrumb acompanha a aba escolhida.
            _navigator?.ShowMain();
            HideNativeNetworkPanels();

            if (_panelJogar != null) _panelJogar.SetActive(section == "jogar");
            if (_panelTutorial != null) _panelTutorial.SetActive(section == "tutorial");
            if (_panelOpcoes != null) _panelOpcoes.SetActive(section == "opcoes");
            if (_panelSair != null) _panelSair.SetActive(section == "sair");

            _tabJogar?.SetSelected(section == "jogar");
            _tabTutorial?.SetSelected(section == "tutorial");
            _tabOpcoes?.SetSelected(section == "opcoes");
            _tabSair?.SetSelected(section == "sair");

            UpdateCrumb();
        }

        public void SelectTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName)) return;

            _selectedTheme = themeName.Trim();
            _menuUIController?.SelectTheme(_selectedTheme);

            if (_themeCards != null)
            {
                foreach (var card in _themeCards)
                {
                    if (card == null) continue;
                    card.SetSelected(string.Equals(card.ThemeName, _selectedTheme));
                }
            }
        }

        private void HandleScreenChanged(MenuScreen screen)
        {
            if (screen != MenuScreen.Main)
            {
                // Lobby/Sala pertencem à aba Jogar: esconde abas auxiliares para não sobrepor.
                if (_panelTutorial != null) _panelTutorial.SetActive(false);
                if (_panelOpcoes != null) _panelOpcoes.SetActive(false);
                if (_panelSair != null) _panelSair.SetActive(false);
                _tabJogar?.SetSelected(true);
                _tabTutorial?.SetSelected(false);
                _tabOpcoes?.SetSelected(false);
                _tabSair?.SetSelected(false);
                _currentSection = "jogar";
            }

            UpdateCrumb();
        }

        private void HideNativeNetworkPanels()
        {
            if (_panelLobbyNative != null) _panelLobbyNative.SetActive(false);
            if (_panelRoomNative != null) _panelRoomNative.SetActive(false);
        }

        private void UpdateCrumb()
        {
            if (_crumbText == null) return;

            string screen = _navigator != null ? _navigator.CurrentScreen.ToString() : "Main";

            _crumbText.text = screen switch
            {
                nameof(MenuScreen.LobbyBrowser) => "menu / jogar / buscar",
                nameof(MenuScreen.RoomWait) => "menu / jogar / sala",
                nameof(MenuScreen.Tutorial) => "menu / tutorial",
                nameof(MenuScreen.Settings) => "menu / opções",
                _ => $"menu / {_currentSection}"
            };
        }

        private void OnCreateRoom()
        {
            if (_menuUIController != null)
            {
                _menuUIController.HostGame();
            }
            else
            {
                Debug.LogWarning("[SysBuildersMenuFlow] MenuUIController não encontrado ao criar sala.");
            }
        }

        private void OnSearchGames()
        {
            if (_menuUIController != null)
            {
                _menuUIController.BuscarPartidas();
            }
            else
            {
                Debug.LogWarning("[SysBuildersMenuFlow] MenuUIController não encontrado ao buscar partidas.");
            }
        }

        private void OnJoinByIp()
        {
            if (_menuUIController != null)
            {
                _menuUIController.JoinByIP();
            }
            else
            {
                Debug.LogWarning("[SysBuildersMenuFlow] MenuUIController não encontrado ao entrar por IP.");
            }
        }
    }
}
