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
    [SerializeField] private TMPro.TMP_InputField ipInputField; // Allow direct connection (e.g., playit.gg)
    [SerializeField] private NetworkDiscovery networkDiscovery;
    [SerializeField] private TextMeshProUGUI statusText; // Optional: shows "Searching..." feedback

    public static string LocalPlayerName = "Player";

    // ─── Cross-Scene Auto-Start ──────────────────────────────────
    public static bool   AutoStartAsHost   = false;
    public static bool   AutoStartAsClient  = false;
    public static string AutoConnectIP      = "";

    public class NetworkRunner : MonoBehaviour
    {
        public bool isHost;
        public string connectIp;

        void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "MainScene")
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
                
                if (NetworkManager.Singleton != null)
                {
                    if (isHost)
                    {
                        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                        if (transport != null) transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
                        NetworkManager.Singleton.StartHost();
                        Debug.Log("[NetworkRunner] Started Host successfully.");
                    }
                    else
                    {
                        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                        if (transport != null)
                        {
                            string ip = string.IsNullOrWhiteSpace(connectIp) ? "127.0.0.1" : connectIp;
                            transport.SetConnectionData(ip, 7777);
                        }
                        NetworkManager.Singleton.StartClient();
                        Debug.Log("[NetworkRunner] Started Client successfully.");
                    }
                }
                Destroy(gameObject);
            }
        }
    }

    private void Awake()
    {
        // Auto-find fallback for NetworkDiscovery if not assigned in Inspector
        if (networkDiscovery == null)
        {
            networkDiscovery = GetComponent<NetworkDiscovery>();
            if (networkDiscovery == null) networkDiscovery = Object.FindAnyObjectByType<NetworkDiscovery>();
        }

        hostButton.onClick.AddListener(() => {
            Debug.Log("[NetworkManagerUI] Host button clicked.");
            DoStartHost();
        });
        
        clientButton.onClick.AddListener(() => {
            Debug.Log("[NetworkManagerUI] Client button clicked.");
            UpdateLocalName();
            if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
            {
                Debug.Log($"[NetworkManagerUI] Attempting direct connection to: {ipInputField.text}");
                ConnectToDirectIP(ipInputField.text);
            }
            else
            {
                Debug.Log("[NetworkManagerUI] Starting LAN search.");
                StartLANSearch();
            }
        });

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnConnectSuccess;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnConnectFailed;
        }
    }

    private void Start()
    {
        // ── Auto-start from MainMenu scene flags ─────────────────────
        if (AutoStartAsHost)
        {
            AutoStartAsHost = false;
            Debug.Log("[NetworkManagerUI] Auto-starting as Host (from MainMenu).");
            // Directly execute the host logic (same as the host button click handler)
            DoStartHost();
            return;
        }
        
        if (AutoStartAsClient)
        {
            AutoStartAsClient = false;
            if (!string.IsNullOrWhiteSpace(AutoConnectIP))
            {
                Debug.Log($"[NetworkManagerUI] Auto-connecting to {AutoConnectIP} (from MainMenu).");
                ConnectToDirectIP(AutoConnectIP);
                AutoConnectIP = "";
            }
            else
            {
                Debug.Log("[NetworkManagerUI] Auto-starting LAN search (from MainMenu).");
                StartLANSearch();
            }
            return;
        }

        // If no auto-start flags, hide the fallback UI since the player
        // should be starting from MainMenu.unity. The UI only stays visible
        // as a fallback if testing MainScene directly in the editor.
#if !UNITY_EDITOR
        gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// Extracted host logic so it can be called from both the button and auto-start.
    /// </summary>
    private void DoStartHost()
    {
        UpdateLocalName();

        // Defensive: fully shut down any stale session that may be holding the port
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetworkManagerUI] Shutting down stale session before hosting.");
            if (networkDiscovery != null) networkDiscovery.StopDiscovery();
            NetworkManager.Singleton.Shutdown();
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", (ushort)7777, "0.0.0.0");
            Debug.Log("[NetworkManagerUI] Host transport configured to listen on 0.0.0.0:7777");
        }

        if (NetworkManager.Singleton.StartHost())
        {
            if (networkDiscovery != null) networkDiscovery.StartBroadcasting();
            gameObject.SetActive(false);
            Debug.Log("[NetworkManagerUI] Host started successfully.");
        }
        else
        {
            NetworkManager.Singleton.Shutdown();
            if (statusText != null)
                statusText.text = "Port 7777 in use. Close other instances or restart Unity.";
            Debug.LogError("[NetworkManagerUI] StartHost failed — port 7777 is likely held by another process.");
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

    private void ConnectToDirectIP(string ipAndPort)
    {
        hostButton.interactable = false;
        clientButton.interactable = false;

        string ip = ipAndPort.Trim();
        ushort port = 7777;

        if (ip.Contains(":"))
        {
            string[] parts = ip.Split(':');
            ip = parts[0];
            if (ushort.TryParse(parts[1], out ushort parsedPort))
            {
                port = parsedPort;
            }
        }

        if (statusText != null)
        {
            statusText.text = $"Connecting to {ip}:{port}...";
            statusText.gameObject.SetActive(true);
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // Set the address we want to connect to
            transport.SetConnectionData(ip, port);
        }

        if (NetworkManager.Singleton.StartClient())
        {
            // Hide menu immediately while handshaking
            hostButton.gameObject.SetActive(false);
            clientButton.gameObject.SetActive(false);
            if (nameInputField != null) nameInputField.gameObject.SetActive(false);
            if (ipInputField != null) ipInputField.gameObject.SetActive(false);
            
            // Handshake timeout
            CancelInvoke(nameof(ConnectionTimeout));
            Invoke(nameof(ConnectionTimeout), 10f);
        }
        else
        {
            ResetUI("Failed to start Client component.");
        }
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
            if (ipInputField != null) ipInputField.gameObject.SetActive(false);
            
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
        if (ipInputField != null) ipInputField.gameObject.SetActive(true);
        
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

    private void ForceFullShutdown()
    {
        if (networkDiscovery != null) networkDiscovery.StopDiscovery();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnConnectSuccess;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnConnectFailed;
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown(true);
        }
    }

    private void OnDestroy()
    {
        ForceFullShutdown();
    }

    private void OnApplicationQuit()
    {
        ForceFullShutdown();
    }

    private void OnDisable()
    {
        if (networkDiscovery != null) networkDiscovery.OnServerFound -= OnLANServerFound;
    }
}
