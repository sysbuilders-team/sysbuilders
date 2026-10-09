using System.Collections.Generic;
using System.Text;
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
        private string _signature;

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

        private void OnDestroy()
        {
            if (_readyButton != null) _readyButton.onClick.RemoveListener(ToggleReady);
            ClearRows();
        }

        private void Update()
        {
            EnsureSource();

            bool inRoom = _source != null && _source.IsInRoom;
            bool lobbyOpen = _lobbyListPanel != null && _lobbyListPanel.activeSelf;
            bool shouldShow = inRoom && !lobbyOpen;
            bool wasShowing = _panelRoot != null && _panelRoot.activeSelf;

            if (_panelRoot != null && wasShowing != shouldShow) _panelRoot.SetActive(shouldShow);

            if (shouldShow)
            {
                HideMenuPanels();
            }
            else if (wasShowing && !inRoom)
            {
                RestoreFallbackPanel();
            }

            if (!shouldShow)
            {
                _signature = null;
                return;
            }

            var slots = _source.Slots;
            string signature = BuildSignature(slots, _source.IsLocalPlayerReady, _source.CanSetReady);

            // Só mexe na UI quando algo mudou (o Update roda a cada frame).
            if (signature == _signature) return;
            _signature = signature;

            SyncRows(slots);
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
            _signature = null;   // força a atualização no próximo frame
        }

        private void EnsureSource()
        {
            if (_source != null) return;

            _source = _sourceBehaviour as IRoomLobbySource ?? FindPort<IRoomLobbySource>();
        }
        
        private static TPort FindPort<TPort>() where TPort : class
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is TPort port) return port;
            }

            return null;
        }

        private static string BuildSignature(IReadOnlyList<RoomSlotInfo> slots, bool localReady, bool canSetReady)
        {
            var builder = new StringBuilder(64);
            builder.Append(canSetReady ? '1' : '0');
            builder.Append(localReady ? '1' : '0');

            foreach (var slot in slots)
            {
                builder.Append('|').Append(slot.Index).Append(':').Append(slot.DisplayName).Append(':').Append(slot.IsReady ? '1' : '0');
            }

            return builder.ToString();
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
