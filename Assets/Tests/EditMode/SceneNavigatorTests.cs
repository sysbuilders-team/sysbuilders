using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Core.Discovery;
using Managers;
using Network;
using NUnit.Framework;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class SceneNavigatorTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdObjects)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _createdObjects.Clear();
        }

        private GameObject TrackedObject(string name)
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

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

        [Test]
        public void MenuNavigator_ShowScreen_ActivatesOnlyTargetPanelAndFiresEvent()
        {
            var root = TrackedObject("Canvas");
            var mainPanel = TrackedObject("MainPanel");
            var lobbyPanel = TrackedObject("LobbyPanel");
            var roomPanel = TrackedObject("RoomPanel");
            var settingsPanel = TrackedObject("SettingsPanel");
            var tutorialPanel = TrackedObject("TutorialPanel");

            var navigator = root.AddComponent<MenuNavigator>();
            navigator.ConfigurePanels(mainPanel, lobbyPanel, roomPanel, settingsPanel, tutorialPanel);

            MenuScreen? reportedScreen = null;
            navigator.OnScreenChanged += screen => reportedScreen = screen;

            navigator.ShowScreen(MenuScreen.LobbyBrowser);
            Assert.AreEqual(MenuScreen.LobbyBrowser, navigator.CurrentScreen);
            Assert.AreEqual(MenuScreen.LobbyBrowser, reportedScreen);
            Assert.IsFalse(mainPanel.activeSelf);
            Assert.IsTrue(lobbyPanel.activeSelf);
            Assert.IsFalse(roomPanel.activeSelf);
            Assert.IsFalse(settingsPanel.activeSelf);
            Assert.IsFalse(tutorialPanel.activeSelf);

            navigator.ShowScreen(MenuScreen.RoomWait);
            Assert.AreEqual(MenuScreen.RoomWait, navigator.CurrentScreen);
            Assert.IsFalse(mainPanel.activeSelf);
            Assert.IsFalse(lobbyPanel.activeSelf);
            Assert.IsTrue(roomPanel.activeSelf);

            navigator.ShowScreen(MenuScreen.Main);
            Assert.AreEqual(MenuScreen.Main, navigator.CurrentScreen);
            Assert.IsTrue(mainPanel.activeSelf);
            Assert.IsFalse(roomPanel.activeSelf);
        }

        [Test]
        public void MenuUIController_JoinRoom_ImmediatelyNavigatesToRoomWaitWithoutNeedingBackButton()
        {
            var root = TrackedObject("Canvas");
            var mainPanel = TrackedObject("MainPanel");
            var lobbyPanel = TrackedObject("LobbyPanel");
            var roomPanel = TrackedObject("RoomPanel");

            var navigator = root.AddComponent<MenuNavigator>();
            navigator.ConfigurePanels(mainPanel, lobbyPanel, roomPanel);

            root.AddComponent<MenuNetworkController>();
            var menuUi = root.AddComponent<MenuUIController>();
            menuUi.SetNavigator(navigator);

            // Jogador abre o navegador de salas
            menuUi.BuscarPartidas();
            Assert.AreEqual(MenuScreen.LobbyBrowser, navigator.CurrentScreen);
            Assert.IsTrue(lobbyPanel.activeSelf);
            Assert.IsFalse(roomPanel.activeSelf);

            // Jogador clica em uma sala descoberta para entrar: deve ir direto para RoomWait
            var ad = new RoomAdvertisement("sess-1", "Sala Teste", "Host", "Biblioteca", 7777, 1, 2);
            var discovered = new DiscoveredRoom(ad, IPAddress.Loopback, DiscoveryTier.Mdns, DateTime.UtcNow);

            menuUi.JoinRoom(discovered);

            Assert.AreEqual(MenuScreen.RoomWait, navigator.CurrentScreen);
            Assert.IsFalse(lobbyPanel.activeSelf, "O painel de lista de salas deve fechar imediatamente ao entrar na sala.");
            Assert.IsTrue(roomPanel.activeSelf, "O painel da sala de espera deve aparecer imediatamente sem exigir clique em Voltar.");
        }

        [Test]
        public void MenuNavigator_WhenClientJoiningAndNotYetConnected_KeepsRoomWaitOpenUntilConnectedAndReturnsOnDisconnect()
        {
            var root = TrackedObject("Canvas");
            var mainPanel = TrackedObject("MainPanel");
            var lobbyPanel = TrackedObject("LobbyPanel");
            var roomPanel = TrackedObject("RoomPanel");

            var fakeLobbySource = new FakeRoomLobbySource { IsInRoom = false };
            var navigator = root.AddComponent<MenuNavigator>();
            navigator.ConfigurePanels(mainPanel, lobbyPanel, roomPanel);
            navigator.SetLobbySource(fakeLobbySource);

            // Cliente clicou para entrar na sala: a tela vai para RoomWait enquanto o handshake ainda está em andamento (IsInRoom == false)
            navigator.ShowScreen(MenuScreen.RoomWait);
            InvokeUpdate(navigator);

            Assert.AreEqual(MenuScreen.RoomWait, navigator.CurrentScreen,
                "Enquanto o handshake está em curso (_wasInRoom == false), o MenuNavigator não deve expulsar o jogador da tela RoomWait.");
            Assert.IsTrue(roomPanel.activeSelf);

            // Handshake conclui (IsInRoom passa para true)
            fakeLobbySource.IsInRoom = true;
            InvokeUpdate(navigator);
            Assert.AreEqual(MenuScreen.RoomWait, navigator.CurrentScreen);

            // Host encerra a sala ou conexão cai (IsInRoom cai de true para false)
            fakeLobbySource.IsInRoom = false;
            InvokeUpdate(navigator);
            Assert.AreEqual(MenuScreen.Main, navigator.CurrentScreen,
                "Quando a sessão da sala encerra (true -> false), o MenuNavigator deve retornar automaticamente ao menu principal.");
            Assert.IsTrue(mainPanel.activeSelf);
            Assert.IsFalse(roomPanel.activeSelf);
        }

        private static void InvokeUpdate(MonoBehaviour target)
        {
            var method = target.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method);
            method.Invoke(target, null);
        }

        private sealed class FakeRoomLobbySource : IRoomLobbySource
        {
            public bool IsInRoom { get; set; }
            public int MaxPlayers => 2;
            public IReadOnlyList<RoomSlotInfo> Slots { get; } = Array.Empty<RoomSlotInfo>();
            public int ReadyCount => 0;
            public bool IsLocalPlayerReady => false;
            public bool CanSetReady => IsInRoom;
            public void SetLocalReady(bool ready) { }
            public event Action OnSlotsChanged;
        }
    }
}
