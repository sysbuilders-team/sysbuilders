using System.Net;
using System.Net.Sockets;
using Mirror;
using Network;
using TMPro;
using UnityEngine;

namespace Managers
{
    public class MenuManager : MonoBehaviour
    {
        [Header("Painéis de Navegação")]
        [SerializeField] private GameObject _startPanel;
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Referências de Rede")]
        [SerializeField] private SysNetworkDiscovery _networkDiscovery;
        [SerializeField] private TMP_InputField _playerNameInputField;
        [SerializeField] private TMP_InputField _ipInputField;
        [SerializeField] private TMP_Text _localIpText;

        private void Start()
        {
            string savedName = PlayerPrefs.GetString("PlayerName", "Jogador");
            if (_playerNameInputField != null)
            {
                _playerNameInputField.text = savedName;
            }

            if (_localIpText != null)
            {
                _localIpText.text = $"Seu IP Local: {GetLocalIPAddress()}";
            }
        }

        public void SetPlayerName(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                PlayerPrefs.SetString("PlayerName", newName.Trim());
                PlayerPrefs.Save();
            }
        }

        public void HostGame()
        {
            if (NetworkManager.singleton == null) return;

            SaveCurrentPlayerName();

            NetworkManager.singleton.StartHost();

            if (_networkDiscovery != null)
            {
                _networkDiscovery.AdvertiseServer();
            }

            Debug.Log("[MenuManager] Host iniciado e anunciando na rede local.");
        }

        public void JoinByIP()
        {
            if (NetworkManager.singleton == null) return;

            SaveCurrentPlayerName();

            string ipAddress = (_ipInputField != null && !string.IsNullOrWhiteSpace(_ipInputField.text))
                ? _ipInputField.text.Trim()
                : "localhost";

            NetworkManager.singleton.networkAddress = ipAddress;
            NetworkManager.singleton.StartClient();

            Debug.Log($"[MenuManager] Conectando diretamente ao IP: {ipAddress}");
        }

        public void AbrirStart() => TrocarPainel(_startPanel);
        public void AbrirLobby() => TrocarPainel(_lobbyPanel);
        public void AbrirTutorial() => TrocarPainel(_tutorialPanel);
        public void AbrirSettings() => TrocarPainel(_settingsPanel);

        private void TrocarPainel(GameObject painelAtivo)
        {
            if (_startPanel != null) _startPanel.SetActive(_startPanel == painelAtivo);
            if (_lobbyPanel != null) _lobbyPanel.SetActive(_lobbyPanel == painelAtivo);
            if (_tutorialPanel != null) _tutorialPanel.SetActive(_tutorialPanel == painelAtivo);
            if (_settingsPanel != null) _settingsPanel.SetActive(_settingsPanel == painelAtivo);
        }

        private void SaveCurrentPlayerName()
        {
            if (_playerNameInputField != null && !string.IsNullOrWhiteSpace(_playerNameInputField.text))
            {
                PlayerPrefs.SetString("PlayerName", _playerNameInputField.text.Trim());
                PlayerPrefs.Save();
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Fallback silencioso
            }
            return "127.0.0.1";
        }
    }
}
