using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Core.Discovery
{
    /// <summary>IPv4 interface candidate to carry multicast (plain data, so the heuristic is testable).</summary>
    public sealed class InterfaceCandidate
    {
        public string Name { get; }
        public string Description { get; }
        public IPAddress Address { get; }
        public bool IsUp { get; }
        public bool SupportsMulticast { get; }
        public bool HasGateway { get; }
        public bool IsVirtualOrTunnel { get; }

        public InterfaceCandidate(string name, string description, IPAddress address, bool isUp,
            bool supportsMulticast, bool hasGateway, bool isVirtualOrTunnel)
        {
            Name = name;
            Description = description;
            Address = address;
            IsUp = isUp;
            SupportsMulticast = supportsMulticast;
            HasGateway = hasGateway;
            IsVirtualOrTunnel = isVirtualOrTunnel;
        }

        public override string ToString() => $"{Name} ({Address}){(HasGateway ? " [gateway]" : string.Empty)}";
    }

    /// <summary>
    /// Picks the primary interface for multicast. Measured in the field: a machine with LAN + Tailscale
    /// + a VirtualBox host-only adapter sends the packet through the wrong interface when nobody
    /// pins IP_MULTICAST_IF, and discovery "just does not work" with no visible error.
    /// The automatic choice is the interface with a gateway (the primary one); if several qualify,
    /// the first non-virtual one. The explicit override stays available in the Inspector.
    /// </summary>
    public static class NetworkInterfaceSelector
    {
        private static readonly string[] VirtualMarkers =
        {
            "virtual", "vmware", "hyper-v", "vethernet", "wsl", "docker", "tailscale", "zerotier",
            "openvpn", "tap-", "tun", "loopback", "bluetooth", "npcap", "radmin", "hamachi"
        };

        public static IReadOnlyList<InterfaceCandidate> Enumerate()
        {
            var result = new List<InterfaceCandidate>();

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    IPInterfaceProperties properties;
                    try
                    {
                        properties = nic.GetIPProperties();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    bool hasGateway = properties.GatewayAddresses
                        .Any(g => g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork);

                    foreach (var unicast in properties.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(unicast.Address)) continue;

                        result.Add(new InterfaceCandidate(
                            nic.Name,
                            nic.Description ?? string.Empty,
                            unicast.Address,
                            nic.OperationalStatus == OperationalStatus.Up,
                            nic.SupportsMulticast,
                            hasGateway,
                            LooksVirtual(nic)));
                    }
                }
            }
            catch (Exception)
            {
                // Platform without enumeration support: return whatever we have.
            }

            return result;
        }

        private static bool LooksVirtual(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;

            string haystack = ((nic.Name ?? string.Empty) + " " + (nic.Description ?? string.Empty)).ToLowerInvariant();
            return VirtualMarkers.Any(marker => haystack.Contains(marker));
        }

        /// <summary>
        /// Pure heuristic (testable without a network): the primary one is up, multicast-capable,
        /// has a gateway and is not virtual/tunnel. If nothing qualifies, fall back to "up + multicast".
        /// </summary>
        public static bool TrySelectPrimary(IReadOnlyList<InterfaceCandidate> candidates,
            out InterfaceCandidate selected, out string reason)
        {
            selected = null;
            reason = string.Empty;

            if (candidates == null || candidates.Count == 0)
            {
                reason = "nenhuma interface IPv4 encontrada";
                return false;
            }

            var preferred = candidates
                .Where(c => c.IsUp && c.SupportsMulticast && c.HasGateway && !c.IsVirtualOrTunnel)
                .ToList();

            if (preferred.Count == 0)
            {
                preferred = candidates.Where(c => c.IsUp && c.SupportsMulticast && !c.IsVirtualOrTunnel).ToList();
                if (preferred.Count > 0)
                {
                    reason = "nenhuma interface com gateway; usando a primeira interface fisica ativa";
                }
            }

            if (preferred.Count == 0)
            {
                preferred = candidates.Where(c => c.IsUp && c.SupportsMulticast).ToList();
                if (preferred.Count > 0)
                {
                    reason = "somente interfaces virtuais/tunel disponiveis";
                }
            }

            if (preferred.Count == 0)
            {
                reason = "nenhuma interface ativa com suporte a multicast";
                return false;
            }

            selected = preferred[0];
            if (string.IsNullOrEmpty(reason))
            {
                reason = preferred.Count > 1
                    ? $"interface principal escolhida entre {preferred.Count} candidatas"
                    : "interface principal (gateway) escolhida";
            }

            return true;
        }

        public static bool TryFindByAddress(IReadOnlyList<InterfaceCandidate> candidates, IPAddress address,
            out InterfaceCandidate selected)
        {
            selected = null;
            if (candidates == null || address == null) return false;

            selected = candidates.FirstOrDefault(c => c.Address != null && c.Address.Equals(address));
            return selected != null;
        }
    }
}
