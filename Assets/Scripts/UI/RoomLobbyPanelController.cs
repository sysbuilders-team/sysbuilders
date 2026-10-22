using System.Collections.Generic;
using Core.Discovery;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class RoomLobbyPanelController : MonoBehaviour
    {
        [Header("Painel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Transform _slotsContainer;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private string _roomTitle = "SALA DE ESPERA";

        [Header("Painéis do menu que a sala substitui")]
        [Tooltip("Desligados enquanto a sala está na frente (não dependemos de ordem de renderização).")]
        [SerializeField] private GameObject[] _panelsToHide;
        [Tooltip("Volta a aparecer quando o jogador sai da sala, senão a tela ficaria vazia.")]
        [SerializeField] private GameObject _fallbackPanel;
        [Tooltip("Se este painel estiver aberto (lista de salas), a sala sai da frente em vez de cobri-lo.")]
        [SerializeField] private GameObject _lobbyListPanel;

        [Header("Ações")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private TMP_Text _readyButtonText;

        [Header("Porta (opcional: deixe vazio para resolver na cena)")]
        [SerializeField] private MonoBehaviour _sourceBehaviour;

        private const float SlotHeight = 48f;
        private const float SlotSpacing = 56f;

        private readonly List<SlotRow> _rows = new List<SlotRow>(2);
        
        private IRoomLobbySource _source;
        private bool _subscribed;
        private float _nextSourceSearch;

        private sealed class SlotRow
        {
            public GameObject Root;
            public TMP_Text Label;
        }

        private void Awake()
        {
            if (_panelRoot == null) _panelRoot = gameObject;

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveListener(ToggleReady);
                _readyButton.onClick.AddListener(ToggleReady);
            }

            if (_titleText != null) _titleText.text = _roomTitle;
            EnsureSource();
        }

        private void OnEnable()
        {
            EnsureSource();

            if (_source != null && !_subscribed)
            {
                _source.OnSlotsChanged += HandleSlotsChanged;
                _subscribed = true;
            }

            // Whatever the adapter holds right now is what the player should see. Waiting for a
            // hash change is not enough: enabling the panel does not change the room state, so
            // the rows would never be built when the panel starts active.
            HandleSlotsChanged();
        }

        private void OnDestroy()
        {
			if(_subscribed && _source != null)
			{
				_source.OnSlotsChanged -= HandleSlotsChanged;
			}
			
            if (_readyButton != null) _readyButton.onClick.RemoveListener(ToggleReady);
            ClearRows();
        }

        private void Update()
        {
            EnsureSource();

            if (_source != null && !_subscribed)
            {
                _source.OnSlotsChanged += HandleSlotsChanged;
                _subscribed = true;
                HandleSlotsChanged();
            }

            bool inRoom = _source != null && _source.IsInRoom;
            bool wasShowing = _panelRoot != null && _panelRoot.activeSelf;

            if (inRoom)
            {
                // Joining a room takes priority — close the lobby list if it is open.
                if (_lobbyListPanel != null && _lobbyListPanel.activeSelf)
                    _lobbyListPanel.SetActive(false);

                if (_panelRoot != null && !_panelRoot.activeSelf)
                {
                    _panelRoot.SetActive(true);
                    HandleSlotsChanged();
                }

                HideMenuPanels();
            }
            else if (wasShowing)
            {
                if (_panelRoot != null) _panelRoot.SetActive(false);
                RestoreFallbackPanel();
            }
        }

		private void HandleSlotsChanged()
		{
		    if (_panelRoot == null || !_panelRoot.activeSelf) return;
		    if (_source == null) return;

		    SyncRows(_source.Slots);
		    UpdateControls();
		} 
        
        private void HideMenuPanels()
        {
            if (_panelsToHide == null) return;

            foreach (var panel in _panelsToHide)
            {
                if (panel != null && panel.activeSelf) panel.SetActive(false);
            }
        }

        private void RestoreFallbackPanel()
        {
            if (_fallbackPanel != null && !_fallbackPanel.activeSelf) _fallbackPanel.SetActive(true);
        }

        private void ToggleReady()
        {
            EnsureSource();
            if (_source == null) return;

            _source.SetLocalReady(!_source.IsLocalPlayerReady);
        }

        private void EnsureSource()
        {
            if (_source != null) return;

            if (_sourceBehaviour != null)
            {
                _source = _sourceBehaviour as IRoomLobbySource;
                if (_source != null) return;
            }

            SceneServices.TryResolve(ref _source, ref _nextSourceSearch);
        }
        

        private void SyncRows(IReadOnlyList<RoomSlotInfo> slots)
        {
            while (_rows.Count > slots.Count)
            {
                int last = _rows.Count - 1;
                DestroyRow(_rows[last]);
                _rows.RemoveAt(last);
            }

            while (_rows.Count < slots.Count)
            {
                _rows.Add(CreateRow(_rows.Count));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var row = _rows[i];

                if (row.Label == null) continue;

                row.Label.text = slot.IsLocalPlayer
                    ? $"{slot.DisplayName} (você) — {slot.ReadyLabel}"
                    : $"{slot.DisplayName} — {slot.ReadyLabel}";

                row.Label.color = slot.IsReady
                    ? new Color(0.55f, 0.95f, 0.6f, 1f)
                    : new Color(1f, 1f, 1f, 0.85f);
            }
        }

        private SlotRow CreateRow(int index)
        {
            var rowObject = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowObject.transform.SetParent(_slotsContainer != null ? _slotsContainer : transform, false);

            var image = rowObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.06f);
            image.raycastTarget = false;

            var rect = rowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, SlotHeight);
            rect.anchoredPosition = new Vector2(0f, -SlotSpacing * index);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rowObject.transform, false);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 26;
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(16f, 0f, 16f, 0f);
            label.raycastTarget = false;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return new SlotRow { Root = rowObject, Label = label };
        }

        private void UpdateControls()
        {
            bool canSetReady = _source.CanSetReady;
            bool localReady = _source.IsLocalPlayerReady;
            int ready = _source.ReadyCount;
            int total = _source.Slots.Count;
            int max = _source.MaxPlayers;

            if (_readyButtonText != null) _readyButtonText.text = localReady ? "Cancelar" : "Pronto";
            if (_readyButton != null) _readyButton.interactable = canSetReady;

            if (_statusText == null) return;

            if (!canSetReady)
            {
                _statusText.text = "Entrando na sala…";
            }
            else if (total < max)
            {
                _statusText.text = $"Aguardando jogadores… ({total}/{max})";
            }
            else if (ready >= total)
            {
                _statusText.text = "Todos prontos! Iniciando a partida…";
            }
            else if (localReady)
            {
                _statusText.text = $"Você está pronto. Aguardando o adversário… ({ready}/{total})";
            }
            else
            {
                _statusText.text = $"Marque Pronto para começar. ({ready}/{total} prontos)";
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                DestroyRow(row);
            }

            _rows.Clear();
        }

        private void DestroyRow(SlotRow row)
        {
            if (row == null || row.Root == null) return;

            if (Application.isPlaying) Destroy(row.Root);
            else DestroyImmediate(row.Root);
        }
    }
}
