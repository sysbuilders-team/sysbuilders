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

            var decoded = (List<SmellOccurrence>)InvokePrivateStatic(
                typeof(MatchSyncBridge),
                "DecodeSmells",
                types,
                severities,
                classes
            );

            Assert.AreEqual(0, decoded.Count, "Entrada com string vazia não deve gerar smell corrompido com classe vazia.");
        }

        [Test]
        public void SmellSerialization_WithMultipleClasses_PreservesAllNames()
        {
            var originalSmell = new SmellOccurrence(CodeSmellType.DuplicateCode, new List<string> { "Player", "Enemy" }, 3);
            var smellsList = new List<SmellOccurrence> { originalSmell };

            object[] encodeArgs = new object[] { smellsList, null, null, null };
            typeof(MatchSyncBridge).GetMethod("EncodeSmells", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, encodeArgs);

            int[] types = (int[])encodeArgs[1];
            int[] severities = (int[])encodeArgs[2];
            string[] classes = (string[])encodeArgs[3];

            var decoded = (List<SmellOccurrence>)InvokePrivateStatic(
                typeof(MatchSyncBridge),
                "DecodeSmells",
                types,
                severities,
                classes
            );

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

            var decoded = (List<SmellOccurrence>)InvokePrivateStatic(
                typeof(MatchSyncBridge),
                "DecodeSmells",
                types,
                severities,
                classes
            );

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

        #region Item 1 & 2: Transição Solo vs Rede no GameManager
        [Test]
        public void HandleLocalReady_InSoloMode_AdvancesTurnManagerPhaseDirectly()
        {
            var go = new GameObject("GameManagerSoloTest");
            var gm = go.AddComponent<GameManager>();

            try
            {
                var turnManager = new TurnManager();
                turnManager.ForceAdvancePhase(); // Show -> Build
                Assert.AreEqual(TurnPhase.Build, turnManager.CurrentPhase);

                // Configura GameManager para solo (RequireOpponentResult = false)
                gm.RequireOpponentResult = false;

                // Cria MatchSession com pelo menos 1 requisito e injeta
                var reqList = new List<Core.Requirements.Requirement>
                {
                    new Core.Requirements.Requirement("req1", "Req 1", new List<Core.Diagram.MethodData>())
                };
                var session = new MatchSession("P1", "P2", reqList, PlayerSlot.PlayerOne);
                session.TurnManager.ForceAdvancePhase(); // Move para Build
                Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

                SetPrivateField(gm, "<Session>k__BackingField", session);

                // Invoca HandleLocalReady
                InvokePrivate(gm, "HandleLocalReady");

                Assert.AreEqual(TurnPhase.Evaluate, session.TurnManager.CurrentPhase, 
                    "Em modo solo, HandleLocalReady deve avançar a fase imediatamente sem esperar rede.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HandleLocalReady_InNetworkMode_DoesNotAdvanceLocally()
        {
            var go = new GameObject("GameManagerNetworkTest");
            var gm = go.AddComponent<GameManager>();

            try
            {
                gm.RequireOpponentResult = true; // Modo Rede

                // Cria MatchSession com pelo menos 1 requisito e injeta
                var reqList = new List<Core.Requirements.Requirement>
                {
                    new Core.Requirements.Requirement("req1", "Req 1", new List<Core.Diagram.MethodData>())
                };
                var session = new MatchSession("P1", "P2", reqList, PlayerSlot.PlayerOne);
                session.TurnManager.ForceAdvancePhase(); // Move para Build
                Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase);

                SetPrivateField(gm, "<Session>k__BackingField", session);

                // Invoca HandleLocalReady
                InvokePrivate(gm, "HandleLocalReady");

                Assert.AreEqual(TurnPhase.Build, session.TurnManager.CurrentPhase,
                    "Em modo rede, HandleLocalReady NÃO deve avançar a fase localmente (aguarda RpcAdvancePhase do servidor).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        #endregion
    }
}
