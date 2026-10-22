using System;
using System.Collections.Generic;
using Core.Discovery;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyRoomListController : MonoBehaviour
    {
        [Header("Lista")]
        [SerializeField] private Transform _roomsContainer;
        [SerializeField] private GameObject _roomEntryPrefab;
        [SerializeField] private GameObject _emptyStateRoot;

        [Header("Ações")]
        [SerializeField] private Button _refreshButton;

        [Header("TOFU — chave trocada")]
        [Tooltip("Exibido quando a chave pública do host não confere com a armazenada. " +
                 "Clicar confia na nova chave e reconecta.")]
        [SerializeField] private Button _trustNewKeyButton;
        [SerializeField] private TMP_Text _trustNewKeyButtonText;

        [Header("Portas (opcional: deixe vazio para resolver na cena)")]
        [Tooltip("Componente que implementa IRoomDirectory (ex.: RoomDiscoveryService).")]
        [SerializeField] private MonoBehaviour _directorySource;

        [Tooltip("Componente que implementa IRoomJoinActions (ex.: MenuManager).")]
        [SerializeField] private MonoBehaviour _joinActionsSource;

        [SerializeField] private TMP_Text _statusText;

        private readonly List<RoomListEntryUI> _entries = new List<RoomListEntryUI>();

        /// <summary>Altura de cada linha e espacamento vertical (o container da cena nao tem layout group).</summary>
        private const float EntryHeight = 56f;
        private const float RowSpacing = 62f;

        private IRoomDirectory _directory;
        private IRoomJoinActions _joinActions;
        private float _nextDirectorySearch;
        private float _nextJoinActionsSearch;

        public IReadOnlyList<RoomListEntryUI> Entries => _entries;

        private void Awake()
        {
            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveListener(Refresh);
                _refreshButton.onClick.AddListener(Refresh);
            }

            if (_trustNewKeyButton != null)
            {
                _trustNewKeyButton.onClick.RemoveListener(HandleTrustNewKeyClicked);
                _trustNewKeyButton.onClick.AddListener(HandleTrustNewKeyClicked);
            }

            HideTrustNewKeyButton();

            EnsurePorts();
        }
        
        private void OnEnable()
        {
            EnsurePorts();

            if (_directory != null)
            {
                _directory.OnRoomsChanged += Rebuild;
                _directory.OnDiagnostic += HandleDiagnostic;
            }

            if (_joinActions != null)
            {
                _joinActions.OnHostKeyMismatch += HandleHostKeyMismatch;
            }

            HideTrustNewKeyButton();
            Rebuild();
        }

        private void OnDisable()
        {
            if (_directory != null)
            {
                _directory.OnRoomsChanged -= Rebuild;
                _directory.OnDiagnostic -= HandleDiagnostic;
            }

            if (_joinActions != null)
            {
                _joinActions.OnHostKeyMismatch -= HandleHostKeyMismatch;
            }

            HideTrustNewKeyButton();
        }

        private void OnDestroy()
        {
            if (_refreshButton != null) _refreshButton.onClick.RemoveListener(Refresh);
            if (_trustNewKeyButton != null) _trustNewKeyButton.onClick.RemoveListener(HandleTrustNewKeyClicked);
            ClearEntries();
        }
        public void Refresh()
        {
            EnsurePorts();

            if (_directory == null)
            {
                SetStatus("Descoberta indisponível nesta build. Use o IP direto ao lado.");
                return;
            }

            _directory.Refresh();
            SetStatus($"Procurando salas em {_directory.ActiveInterface}...");
        }

        public void StopBrowsing()
        {
            _directory?.StopBrowsing();
            Rebuild();
        }

        private void EnsurePorts()
        {
            if (_directory == null)
            {
                if (_directorySource != null)
                    _directory = _directorySource as IRoomDirectory;

                if (_directory == null)
                    SceneServices.TryResolve(ref _directory, ref _nextDirectorySearch);
            }

            if (_joinActions == null)
            {
                if (_joinActionsSource != null)
                    _joinActions = _joinActionsSource as IRoomJoinActions;

                if (_joinActions == null)
                    SceneServices.TryResolve(ref _joinActions, ref _nextJoinActionsSearch);
            }
        }

        private void Rebuild()
        {
            ClearEntries();
            EnsurePorts();

            if (_directory == null)
            {
                UpdateEmptyState();
                return;
            }

            int index = 0;
            foreach (var room in _directory.Rooms)
            {
                var entry = CreateEntry(room, index);
                if (entry == null) continue;

                entry.OnClicked += HandleEntryClicked;
                _entries.Add(entry);
                index++;
            }

            UpdateEmptyState();
        }

        private RoomListEntryUI CreateEntry(DiscoveredRoom room, int index)
        {
            GameObject entryObject;

            if (_roomEntryPrefab != null && _roomsContainer != null)
            {
                entryObject = Instantiate(_roomEntryPrefab, _roomsContainer);
            }
            else
            {
                // Sem prefab: linha mínima funcional (rótulo único + botão), como nos outros controllers.
                entryObject = new GameObject($"Room_{room.Room.RoomName}", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(Button));
                entryObject.transform.SetParent(_roomsContainer != null ? _roomsContainer : transform, false);

                var image = entryObject.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.08f);
            }

            // O container da cena não tem layout group: empilha as linhas a partir do topo dele.
            var rect = entryObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, EntryHeight);
                rect.anchoredPosition = new Vector2(0f, -RowSpacing * index);
            }

            var entry = entryObject.GetComponent<RoomListEntryUI>() ?? entryObject.AddComponent<RoomListEntryUI>();

            if (_roomEntryPrefab == null || _roomsContainer == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(entryObject.transform, false);

                var label = labelObject.GetComponent<TextMeshProUGUI>();
                label.fontSize = 16;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Left;
                label.margin = new Vector4(12f, 0f, 12f, 0f);
                label.raycastTarget = false;

                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                entry.Bind(label, entryObject.GetComponent<Button>());
            }

            entry.Setup(room);
            return entry;
        }

        private void HandleEntryClicked(DiscoveredRoom room)
        {
            EnsurePorts();

            if (room.Room.PlayerCount >= room.Room.MaxPlayers)
            {
                SetStatus($"A sala '{room.Room.RoomName}' está cheia ({room.Room.PlayerCount}/{room.Room.MaxPlayers}).");
                return;
            }

            if (_joinActions == null)
            {
                SetStatus("Não encontrei quem trate o join nesta cena.");
                return;
            }

            SetStatus($"Entrando na sala '{room.Room.RoomName}'...");
            _joinActions.JoinRoom(room);
        }

        private void HandleDiagnostic(string message)
        {
            SetStatus(message);
        }
        
        private void HandleHostKeyMismatch(string host, string presentedFingerprint)
        {
            if (_trustNewKeyButtonText != null)
            {
                _trustNewKeyButtonText.text =
                    $"A chave do host em {host} mudou. Confiar na nova chave e reconectar?";
            }

            if (_trustNewKeyButton != null) _trustNewKeyButton.gameObject.SetActive(true);

            SetStatus($"Falha ao verificar a chave do host {host}. Confirme para tentar novamente.");
        }

        private void HideTrustNewKeyButton()
        {
            if (_trustNewKeyButton != null) _trustNewKeyButton.gameObject.SetActive(false);
        }

        private void HandleTrustNewKeyClicked()
        {
            EnsurePorts();
            if (_joinActions == null) return;

            HideTrustNewKeyButton();

            if (!_joinActions.TrustPendingKeyAndRetry())
            {
                SetStatus("Não foi possível reconectar automaticamente. Tente novamente.");
            }
        }

        private void UpdateEmptyState()
        {
            bool empty = _entries.Count == 0;

            if (_emptyStateRoot != null) _emptyStateRoot.SetActive(empty);

            if (empty) SetStatus("Nenhuma sala encontrada. Peça o IP do host ou verifique o firewall/perfil de rede.");
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
        }

        private void ClearEntries()
        {
            foreach (var entry in _entries)
            {
                if (entry == null) continue;

                entry.OnClicked -= HandleEntryClicked;

                if (Application.isPlaying) Destroy(entry.gameObject);
                else DestroyImmediate(entry.gameObject);
            }

            _entries.Clear();
        }
    }
}
