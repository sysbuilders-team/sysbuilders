using System;
using System.Net;
using Mirror;
using Mirror.Discovery;
using Mirror.Transports.Encryption;
using UnityEngine;

namespace Network
{
    public struct SysDiscoveryRequest : NetworkMessage { }
    public struct SysDiscoveryResponse : NetworkMessage
    {
        public Uri Uri;
        public string HostName;
    }
    
    [DisallowMultipleComponent]
    [AddComponentMenu("SysBuilders/Network/Sys Network Discovery")]
    public class SysNetworkDiscovery
        : NetworkDiscoveryBase<SysDiscoveryRequest, SysDiscoveryResponse>
    {
        protected override SysDiscoveryResponse ProcessRequest(
            SysDiscoveryRequest request, IPEndPoint endpoint)
        {
            try
            {
                return new SysDiscoveryResponse
                {
                    Uri      = transport.ServerUri(),
                    HostName = PlayerPrefs.GetString("PlayerName", "Host")
                };
            }
            catch (NotImplementedException)
            {
                Debug.LogError(
                    $"[SysNetworkDiscovery] O transport '{transport}' não suporta ServerUri(). " +
                    "Escolha um transport compatível com Network Discovery.");
                throw;
            }
        }
        protected override SysDiscoveryRequest GetRequest() => new SysDiscoveryRequest();
        
        protected override void ProcessResponse(
            SysDiscoveryResponse response, IPEndPoint endpoint)
        {
            UriBuilder correctedUri = new UriBuilder(response.Uri)
            {
                Host = endpoint.Address.ToString()
            };
            response.Uri = correctedUri.Uri;

            OnServerFound.Invoke(response);
        }
    }
}
