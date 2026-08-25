using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Managers{
    public class MenuManager : MonoBehaviour
    {   
        [Header("Painéis")]
        [SerializeField] private GameObject _startPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Tema Selecionado")]
        private string _selectedTheme = "Biblioteca";   

        public void SelectTheme(string themeName)
        {
            _selectedTheme = themeName;
            Debug.Log($"Tema Selecionado: {_selectedTheme}");
            //add feature button hover
        }

        public void StartGame() 
        {
            Debug.Log("Iniciando Jogo");
            PlayerPrefs.SetString("SelectedTheme", _selectedTheme);
            PlayerPrefs.Save();

            SceneManager.LoadScene("GameScene");
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