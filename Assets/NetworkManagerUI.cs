using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMPro.TMP_InputField nameInputField;
    [SerializeField] private NetworkDiscovery networkDiscovery;
    [SerializeField] private TextMeshProUGUI statusText; // Optional: shows "Searching..." feedback

    public static string LocalPlayerName = "Player";

    private void Awake()
    {
        // Auto-find fallback for NetworkDiscovery if not assigned in Inspector
        if (networkDiscovery == null)
        {
            networkDiscovery = GetComponent<NetworkDiscovery>();
            if (networkDiscovery == null) networkDiscovery = Object.FindAnyObjectByType<NetworkDiscovery>();
        }

        hostButton.onClick.AddListener(() => {
            UpdateLocalName();
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                // Force host to listen on all interfaces
                transport.SetConnectionData("127.0.0.1", (ushort)7777, "0.0.0.0");
                Debug.Log($"[NetworkManagerUI] Host transport configured to listen on 0.0.0.0:7777");
            }

            if (NetworkManager.Singleton.StartHost())
            {
                if (networkDiscovery != null) networkDiscovery.StartBroadcasting();
                gameObject.SetActive(false);
                Debug.Log("[NetworkManagerUI] Host started successfully.");
            }
            else
            {
                if (statusText != null) statusText.text = "Failed to start Host.";
                Debug.LogError("[NetworkManagerUI] NetworkManager.StartHost() returned false.");
            }
        });
        
        clientButton.onClick.AddListener(() => {
            UpdateLocalName();
            StartLANSearch();
        });

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnConnectSuccess;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnConnectFailed;
        }
    }

    private void StartLANSearch()
    {
        if (networkDiscovery == null) { Debug.LogError("NetworkDiscovery reference missing!"); return; }

        hostButton.interactable = false;
        clientButton.interactable = false;

        if (statusText != null)
        {
            statusText.text = "Searching for LAN game...";
            statusText.gameObject.SetActive(true);
        }

        CancelInvoke(nameof(DiscoveryTimeout));
        Invoke(nameof(DiscoveryTimeout), 10f);

        networkDiscovery.OnServerFound += OnLANServerFound;
        networkDiscovery.StartSearch();
    }

    private void DiscoveryTimeout()
    {
        if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening) return;
        ResetUI("No games found on your LAN.");
        if (networkDiscovery != null) networkDiscovery.StopDiscovery();
    }

    private void OnLANServerFound(string hostIp, ushort hostPort)
    {
        CancelInvoke(nameof(DiscoveryTimeout));
        Debug.Log($"[NetworkManagerUI] Found host at {hostIp}:{hostPort}, attempting connection...");

        if (statusText != null) statusText.text = $"Connecting to {hostIp}...";

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // Set the address we want to connect to
            transport.SetConnectionData(hostIp, hostPort);
        }

        networkDiscovery.OnServerFound -= OnLANServerFound;
        
        if (NetworkManager.Singleton.StartClient())
        {
            // Hide menu immediately while handshaking
            hostButton.gameObject.SetActive(false);
            clientButton.gameObject.SetActive(false);
            if (nameInputField != null) nameInputField.gameObject.SetActive(false);
            
            // Handshake timeout
            CancelInvoke(nameof(ConnectionTimeout));
            Invoke(nameof(ConnectionTimeout), 10f);
        }
        else
        {
            ResetUI("Failed to start Client component.");
        }
    }

    private void ConnectionTimeout()
    {
        if (!NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkManagerUI] Connection handshake timed out.");
            NetworkManager.Singleton.Shutdown();
            ResetUI("Handshake Timeout (Check Firewalls!)");
        }
    }

    private void OnConnectSuccess(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            CancelInvoke(nameof(ConnectionTimeout));
            gameObject.SetActive(false);
            Debug.Log("[NetworkManagerUI] Successfully connected to server.");
        }
    }

    private void OnConnectFailed(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId || !NetworkManager.Singleton.IsConnectedClient)
        {
            CancelInvoke(nameof(ConnectionTimeout));
            ResetUI(NetworkManager.Singleton.IsServer ? "Server Stopped" : "Disconnect / Failure");
        }
    }

    private void ResetUI(string message)
    {
        hostButton.interactable = true;
        clientButton.interactable = true;
        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);
        if (nameInputField != null) nameInputField.gameObject.SetActive(true);
        
        gameObject.SetActive(true);

        if (statusText != null)
        {
            statusText.text = message;
            statusText.gameObject.SetActive(true);
        }
    }

    private void UpdateLocalName()
    {
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
        {
            LocalPlayerName = nameInputField.text;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnConnectSuccess;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnConnectFailed;
        }
    }

    private void OnDisable()
    {
        if (networkDiscovery != null) networkDiscovery.OnServerFound -= OnLANServerFound;
    }
}
