using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Core.Evaluation;
using Core.Session;
using Managers;
using Mirror;
using Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [TestFixture]
    public class DiagnosticFixesTests
    {
        #region Helpers for Reflection
        private static object InvokePrivateStatic(Type type, string methodName, params object[] parameters)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"Método estático {methodName} não encontrado em {type.Name}.");
            return method.Invoke(null, parameters);
        }

        private static object InvokePrivate(object instance, string methodName, params object[] parameters)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Método {methodName} não encontrado em {instance.GetType().Name}.");
            return method.Invoke(instance, parameters);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Campo {fieldName} não encontrado em {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Campo {fieldName} não encontrado em {target.GetType().Name}.");
            return field.GetValue(target);
        }
        #endregion

        #region Item 4: Fidelidade em Encode/Decode de Smells com lista vazia
        [Test]
        public void DecodeSmells_WhenClassStringIsEmpty_DoesNotProduceCorruptedOccurrence()
        {
            // O bug antigo: "".Split() gerava [""], criando SmellOccurrence com nome de classe ""
            // O comportamento correto: string vazia vira Array.Empty<string>(), e não polui com classe de nome vazio.
            int[] types = new int[] { (int)CodeSmellType.LargeClass };
            int[] severities = new int[] { 2 };
            string[] classes = new string[] { string.Empty };

            var decoded = SmellWireCodec.Decode(types, severities, classes);
        
                    Assert.AreEqual(0, decoded.Count, "Entrada com string vazia não deve gerar smell corrompido com classe vazia.");
        }

        [Test]
        public void SmellSerialization_WithMultipleClasses_PreservesAllNames()
        {
            var originalSmell = new SmellOccurrence(CodeSmellType.DuplicateCode, new List<string> { "Player", "Enemy" }, 3);
            var smellsList = new List<SmellOccurrence> { originalSmell };

			SmellWireCodec.Encode(smellsList, out int[] types, out int[] severities, out string[] classes);
            var decoded = SmellWireCodec.Decode(types, severities, classes);			

            Assert.AreEqual(1, decoded.Count);
            Assert.AreEqual(2, decoded[0].AffectedClassNames.Count);
            Assert.AreEqual("Player", decoded[0].AffectedClassNames[0]);
            Assert.AreEqual("Enemy", decoded[0].AffectedClassNames[1]);
        }
        #endregion

        #region Item 5: Teto de Segurança em DecodeSmells
        [Test]
        public void DecodeSmells_WhenPayloadExceedsCap_LimitsToMaxSmellsPerResult()
        {
            int oversizedCount = 150;
            int[] types = new int[oversizedCount];
            int[] severities = new int[oversizedCount];
            string[] classes = new string[oversizedCount];

            for (int i = 0; i < oversizedCount; i++)
            {
                types[i] = (int)CodeSmellType.LargeClass;
                severities[i] = 1;
                classes[i] = "ClassA";
            }

			var decoded = SmellWireCodec.Decode(types, severites, classes);

            Assert.AreEqual(50, decoded.Count, "DecodeSmells deve respeitar o teto máximo de 50 elementos por resultado.");
        }
        #endregion

        #region Item 6: GameRoomPlayer Hook de playerName
        [Test]
        public void GameRoomPlayer_WhenPlayerNameHookTriggers_FiresOnRoomPlayerNameChangedEvent()
        {
            var go = new GameObject("RoomPlayerTest");
            go.AddComponent<NetworkIdentity>();
            var player = go.AddComponent<GameRoomPlayer>();

            string notifiedNewName = null;
            GameRoomPlayer notifiedPlayer = null;

            Action<GameRoomPlayer, string> handler = (p, name) =>
            {
                notifiedPlayer = p;
                notifiedNewName = name;
            };

            GameRoomPlayer.OnRoomPlayerNameChanged += handler;

            try
            {
                InvokePrivate(player, "OnPlayerNameChanged", "Antigo", "NovoJogador");

                Assert.AreEqual(player, notifiedPlayer);
                Assert.AreEqual("NovoJogador", notifiedNewName);
            }
            finally
            {
                GameRoomPlayer.OnRoomPlayerNameChanged -= handler;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        #endregion

        #region Item 3: Validação de Autoridade de Conexão no MatchSyncBridge
        [Test]
        public void IsSenderAuthorizedForSlot_HostOnlyAuthorizedForPlayerOne()
        {
            var go = new GameObject("BridgeTest");
            go.AddComponent<NetworkIdentity>();
            var bridge = go.AddComponent<MatchSyncBridge>();

            try
            {
                // Host / localConnection (sender = null)
                bool hostForP1 = (bool)InvokePrivate(bridge, "IsSenderAuthorizedForSlot", null, PlayerSlot.PlayerOne);
                bool hostForP2 = (bool)InvokePrivate(bridge, "IsSenderAuthorizedForSlot", null, PlayerSlot.PlayerTwo);

                Assert.IsTrue(hostForP1, "Host deve ter autoridade para PlayerOne");
                Assert.IsFalse(hostForP2, "Host não deve ter autoridade para se passar por PlayerTwo");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        #endregion

        #region Item 1 & 2: Transição Solo vs Rede no TurnManager / MatchSession
        [Test]
        public void MatchSession_InSoloMode_SetPlayerReadyAdvancesPhaseAutomatically()
        {
            var reqList = new List<Core.Requirements.Requirement>
            {
                new Core.Requirements.Requirement("req1", "Req 1", new List<Core.Diagram.MethodData>())
            };
            var session = new MatchSession("P1", "P2", reqList, PlayerSlot.PlayerOne, isNetworkMatch: false);
            session.TurnManager.ForceAdvancePhase(); // Show -> Build
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            // Em modo solo (1 player necessário), SetPlayerReady atinge o quórum e avança direto
            var result = session.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(SetPlayerReadyResult.Success, result);
            Assert.AreEqual(TurnPhase.Evaluate, session.TurnManager.CurrentPhase, 
                "Em modo solo, SetPlayerReady no TurnManager deve atingir o quórum de 1 e avançar direto na raiz.");
        }

        [Test]
        public void MatchSession_InNetworkMode_SetPlayerReadyDoesNotAdvanceLocally()
        {
            var reqList = new List<Core.Requirements.Requirement>
            {
                new Core.Requirements.Requirement("req1", "Req 1", new List<Core.Diagram.MethodData>())
            };
            var session = new MatchSession("P1", "P2", reqList, PlayerSlot.PlayerOne, isNetworkMatch: true);
            session.TurnManager.ForceAdvancePhase(); // Show -> Build
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

            // Em modo rede (2 players necessários), SetPlayerReady registra a prontidão (1/2), mas não avança até RpcAdvancePhase
            var result = session.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(SetPlayerReadyResult.Success, result);
            Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase,
                "Em modo rede, SetPlayerReady registra a prontidão (1/2), aguardando o RpcAdvancePhase do servidor.");
        }
        #endregion

        #region Item 9: Failfast quando sorteio de trilha falhar no servidor
        [Test]
        public void OnStartServer_WhenRequirementManagerMissing_GracefullyAbortsWithoutException()
        {
            var go = new GameObject("BridgeFailTest");
            go.AddComponent<NetworkIdentity>();
            var bridge = go.AddComponent<MatchSyncBridge>();

            try
            {
                // OnStartServer com requirementManager nulo deve capturar o erro e não lançar exceção não tratada
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Erro crítico ao iniciar partida no servidor.*"));
                Assert.DoesNotThrow(() => bridge.OnStartServer());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        #endregion
    }
}
