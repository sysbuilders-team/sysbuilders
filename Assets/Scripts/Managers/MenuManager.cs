using UnityEngine;
using TMPro;

namespace Managers
{
    public class MenuManager : MonoBehaviour
    {   
        [Header("Painéis")]
        [SerializeField] private GameObject _startPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Dados do Jogador")]
        [SerializeField] private TMP_InputField _playerNameInputField;
        [SerializeField] private string _defaultPlayerName = "Jogador";
        
        public void SavePlayerData()
        {
            string playerName = _playerNameInputField != null && !string.IsNullOrWhiteSpace(_playerNameInputField.text)
                ? _playerNameInputField.text.Trim()
                : _defaultPlayerName;

            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
        }

        public void AbrirStart()
        {
            _startPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            _tutorialPanel.SetActive(false); 
        }

        public void AbrirTutorial()
        {
            _startPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _tutorialPanel.SetActive(true);
        }

        public void AbrirSettings()
        {
            _startPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            _tutorialPanel.SetActive(false);
        }
    }
}