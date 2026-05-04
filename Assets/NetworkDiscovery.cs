using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Robust LAN discovery using UDP broadcast.
/// Filters out virtual adapters (VirtualBox, etc.) and prioritizes physical LAN IPs.
/// </summary>
public class NetworkDiscovery : MonoBehaviour
{
    [Header("Discovery Settings")]
    public int broadcastPort = 47777;
    public float broadcastInterval = 1.0f;
    public string gameIdentifier = "InertiaKnights";

    private List<UdpClient> broadcastClients = new List<UdpClient>();
    private UdpClient listenClient;
    private float broadcastTimer;
    private bool isBroadcasting = false;
    private bool isSearching = false;

    public event Action<string, ushort> OnServerFound;

    public void StartBroadcasting()
    {
        if (isBroadcasting) return;

        try
        {
            StopDiscovery(); // Reset
            
            // Create a broadcast client for every valid physical adapter
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (IsVirtualAdapter(ni)) continue;

                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        try
                        {
                            UdpClient client = new UdpClient();
                            client.EnableBroadcast = true;
                            // Bind to this specific local IP so we broadcast FROM the correct adapter
                            client.Client.Bind(new IPEndPoint(ip.Address, 0));
                            broadcastClients.Add(client);
                            Debug.Log($"[NetworkDiscovery] Broadcasting game on {ni.Name} ({ip.Address})");
                        }
                        catch { /* Skip failing adapters */ }
                    }
                }
            }

            if (broadcastClients.Count > 0)
            {
                isBroadcasting = true;
                broadcastTimer = 0f;
            }
            else
            {
                Debug.LogError("[NetworkDiscovery] No valid network adapters found to broadcast on!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDiscovery] Failed to start broadcasting: {e.Message}");
        }
    }

    public void StartSearch()
    {
        if (isSearching) return;

        try
        {
            listenClient = new UdpClient(broadcastPort);
            listenClient.EnableBroadcast = true;
            isSearching = true;
            listenClient.BeginReceive(OnReceiveCallback, null);
            Debug.Log($"[NetworkDiscovery] Searching for LAN games on port {broadcastPort}...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDiscovery] Failed to start search: {e.Message}");
        }
    }

    public void StopDiscovery()
    {
        isBroadcasting = false;
        isSearching = false;

        foreach (var client in broadcastClients)
        {
            try { client.Close(); } catch { }
        }
        broadcastClients.Clear();

        if (listenClient != null)
        {
            try { listenClient.Close(); } catch { }
            listenClient = null;
        }
    }

    void Update()
    {
        if (!isBroadcasting) return;

        broadcastTimer += Time.unscaledDeltaTime;
        if (broadcastTimer >= broadcastInterval)
        {
            broadcastTimer = 0f;
            SendBroadcast();
        }
    }

    private bool IsVirtualAdapter(NetworkInterface ni)
    {
        // Filter out VM adapters, VPNs, and loopbacks
        string desc = ni.Description.ToLower();
        string name = ni.Name.ToLower();

        return ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
               ni.OperationalStatus != OperationalStatus.Up ||
               desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("vbox") ||
               desc.Contains("virtualbox") || desc.Contains("vpn") || desc.Contains("pseudo") ||
               name.Contains("vnet") || name.Contains("vbox");
    }

    private void SendBroadcast()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        ushort gamePort = transport != null ? transport.ConnectionData.Port : (ushort)7777;

        for (int i = broadcastClients.Count - 1; i >= 0; i--)
        {
            var client = broadcastClients[i];
            try
            {
                // We send the specific IP we are broadcasting FROM so the client knows exactly which host address to use
                IPEndPoint localEp = (IPEndPoint)client.Client.LocalEndPoint;
                string message = $"{gameIdentifier}|{localEp.Address}|{gamePort}";
                byte[] data = Encoding.UTF8.GetBytes(message);

                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
                client.Send(data, data.Length, endPoint);
            }
            catch
            {
                client.Close();
                broadcastClients.RemoveAt(i);
            }
        }
        
        if (broadcastClients.Count == 0 && isBroadcasting)
        {
            isBroadcasting = false;
            Debug.LogWarning("[NetworkDiscovery] All broadcast clients failed. Stopping.");
        }
    }

    private void OnReceiveCallback(IAsyncResult result)
    {
        if (!isSearching || listenClient == null) return;

        try
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = listenClient.EndReceive(result, ref remoteEndPoint);
            string message = Encoding.UTF8.GetString(data);

            // Parse: IDENTIFIER|IP|PORT
            string[] parts = message.Split('|');
            if (parts.Length == 3 && parts[0] == gameIdentifier)
            {
                string hostIp = parts[1];
                ushort gamePort = ushort.Parse(parts[2]);

                Debug.Log($"[NetworkDiscovery] Found potential server at {hostIp}:{gamePort} (via {remoteEndPoint.Address})");

                // If the broadcasted IP is a virtual one but we received it on a different one, 
                // we might want to be careful, but the sender now filters its own IPs.
                
                _foundHostIp = hostIp;
                _foundHostPort = gamePort;
                _serverFoundPending = true;
            }
            else if (isSearching)
            {
                listenClient.BeginReceive(OnReceiveCallback, null);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkDiscovery] Receive error: {e.Message}");
        }
    }

    private volatile bool _serverFoundPending = false;
    private string _foundHostIp;
    private ushort _foundHostPort;

    void LateUpdate()
    {
        if (_serverFoundPending)
        {
            _serverFoundPending = false;
            // Note: We don't StopDiscovery() here yet so we can hear multiple if needed,
            // but for this setup we stop once the UI handles the first one.
            OnServerFound?.Invoke(_foundHostIp, _foundHostPort);
        }
    }

    void OnDestroy() => StopDiscovery();
    void OnApplicationQuit() => StopDiscovery();
}
