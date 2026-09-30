using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Core.Evaluation;
using Core.Session;
using Managers;

namespace Network
{
    public class MatchSyncBridge : NetworkBehaviour
    {
        private const char ClassSeparator = '\u001f';

        [SerializeField] private GameManager gameManager;
        [SerializeField] private RequirementManager requirementManager;

        [SyncVar] private string _theme;
        [SyncVar(hook = nameof(OnTrailSynced))] private string _trailId;

        private readonly HashSet<PlayerSlot> _readySlots = new HashSet<PlayerSlot>();
        private MatchSession _subscribedSession;

        public override void OnStartServer()
        {
            base.OnStartServer();

            var roomManager = NetworkManager.singleton as GameRoomManager;
            _theme = roomManager != null && !string.IsNullOrEmpty(roomManager.ChosenTheme)
                ? roomManager.ChosenTheme
                : PlayerPrefs.GetString("SelectedTheme", "Biblioteca");

            if (requirementManager == null)
            {
                requirementManager = GetComponent<RequirementManager>() ?? FindFirstObjectByType<RequirementManager>();
            }

            if (requirementManager == null)
            {
                Debug.LogError("[MatchSyncBridge] RequirementManager não encontrado para sortear a trilha.");
                return;
            }

            _trailId = requirementManager.GetRandomTrailId(_theme);

            Debug.Log($"[MatchSyncBridge] Server: tema={_theme} | trail={_trailId}");
            
            StartLocalMatch(_theme, _trailId, PlayerSlot.PlayerOne);
        }

        private void OnTrailSynced(string oldTrailId, string newTrailId)
        {
            if (!isClientOnly) return; 
            if (string.IsNullOrEmpty(newTrailId)) return;

            Debug.Log($"[MatchSyncBridge] Cliente sincronizou trilha: {newTrailId} (Tema: {_theme})");
            StartLocalMatch(_theme, newTrailId, PlayerSlot.PlayerTwo);
        }

        private void StartLocalMatch(string theme, string trailId, PlayerSlot slot)
        {
            gameManager.RequireOpponentResult = true;
            gameManager.StartMatch(theme, trailId, slot);

            // A Session só existe depois do StartMatch (no OnEnable ela ainda era nula,
            // então a assinatura antiga nunca acontecia e o placar não era enviado).
            if (gameManager.Session != null && _subscribedSession != gameManager.Session)
            {
                _subscribedSession = gameManager.Session;
                _subscribedSession.OnEvaluationCompleted += HandleLocalEvaluationCompleted;
            }
        }

        public void NotifyLocalPlayerReady(PlayerSlot slot) => CmdSetReady((int)slot);

        [Command(requiresAuthority = false)]
        private void CmdSetReady(int slotIndex)
        {
            var slot = (PlayerSlot)slotIndex;
            
            _readySlots.Add(slot);
            Debug.Log($"[MatchSyncBridge] Server: slot {slot} pronto ({_readySlots.Count}/2)");

            if (_readySlots.Count >= 2)
            {
                _readySlots.Clear();
                RpcAdvancePhase();
            }
        }

        [ClientRpc]
        private void RpcAdvancePhase()
        {
            Debug.Log("[MatchSyncBridge] RpcAdvancePhase recebido — avançar TurnManager local.");
            gameManager.Session.TurnManager.ForceAdvancePhase();
        }
        
        private void OnEnable()
        {
            if (gameManager == null) return;

            gameManager.OnLocalPlayerReady += HandleLocalPlayerReady;
            gameManager.OnReturnToMenuRequested += HandleReturnToMenu;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;

            gameManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
            gameManager.OnReturnToMenuRequested -= HandleReturnToMenu;
        }

        private void OnDestroy()
        {
            if (_subscribedSession != null)
            {
                _subscribedSession.OnEvaluationCompleted -= HandleLocalEvaluationCompleted;
                _subscribedSession = null;
            }
        }

        private void HandleLocalPlayerReady()
        {
            if (gameManager == null || gameManager.Session == null) return;
            NotifyLocalPlayerReady(gameManager.Session.LocalSlot);
        }

        private void HandleReturnToMenu()
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopClient();
            }
        }

        private void HandleLocalEvaluationCompleted(EvaluationResult localResult)
        {
            if (gameManager == null || gameManager.Session == null) return;
            EncodeSmells(localResult.DetectedSmells, out int[] smellTypes, out int[] smellSeverities, out string[] smellClasses);
            CmdSendEvaluationResult((int)gameManager.Session.LocalSlot, localResult.RawScore, localResult.PenaltyScore,
                smellTypes, smellSeverities, smellClasses);
        }

        // Só primitivos pela rede: os smells viajam como arrays (tipo, severidade, classes juntas por separador).
        private static void EncodeSmells(IReadOnlyList<SmellOccurrence> smells,
            out int[] types, out int[] severities, out string[] classes)
        {
            types = new int[smells.Count];
            severities = new int[smells.Count];
            classes = new string[smells.Count];

            for (int i = 0; i < smells.Count; i++)
            {
                types[i] = (int)smells[i].Type;
                severities[i] = smells[i].Severity;
                classes[i] = string.Join(ClassSeparator.ToString(), smells[i].AffectedClassNames);
            }
        }

        private static List<SmellOccurrence> DecodeSmells(int[] types, int[] severities, string[] classes)
        {
            var smells = new List<SmellOccurrence>();
            if (types == null || severities == null || classes == null) return smells;

            int count = Math.Min(types.Length, Math.Min(severities.Length, classes.Length));
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var names = classes[i].Split(ClassSeparator);
                    smells.Add(new SmellOccurrence((CodeSmellType)types[i], names, Math.Max(1, severities[i])));
                }
                catch (ArgumentException)
                {
                    // dado inválido vindo da rede: ignora esta ocorrência
                }
            }

            return smells;
        }

        [Command(requiresAuthority = false)]
        private void CmdSendEvaluationResult(int slotIndex, int rawScore, int penaltyScore,
            int[] smellTypes, int[] smellSeverities, string[] smellClasses)
        {
            RpcReceiveEvaluationResult(slotIndex, rawScore, penaltyScore, smellTypes, smellSeverities, smellClasses);
        }

        [ClientRpc]
        private void RpcReceiveEvaluationResult(int targetSlot, int rawScore, int penaltyScore,
            int[] smellTypes, int[] smellSeverities, string[] smellClasses)
        {
            if (gameManager == null || gameManager.Session == null) return;
            if ((int)gameManager.Session.LocalSlot == targetSlot) return;

            var rivalResult = new EvaluationResult(
                rawScore,
                penaltyScore,
                new List<string>(),
                DecodeSmells(smellTypes, smellSeverities, smellClasses)
            );

            Debug.Log($"[MatchSyncBridge] Slot rival {targetSlot} — raw:{rawScore} penalty:{penaltyScore} total:{rivalResult.TotalScore}");
            gameManager.ReceiveOpponentEvaluation(rivalResult);
        }
    }
}