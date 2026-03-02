using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMPro.TMP_InputField nameInputField;
    [SerializeField] private TMPro.TMP_InputField ipInputField;

    public static string LocalPlayerName = "Player";

    private void Awake()
    {
        hostButton.onClick.AddListener(() => {
            UpdateLocalName();
            NetworkManager.Singleton.StartHost();
            gameObject.SetActive(false);
        });
        
        clientButton.onClick.AddListener(() => {
            UpdateLocalName();
            SetTransportAddress();
            NetworkManager.Singleton.StartClient();
            gameObject.SetActive(false);
        });
    }

    private void SetTransportAddress()
    {
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null && ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            transport.ConnectionData.Address = ipInputField.text.Trim();
        }
        else if (transport != null)
        {
            transport.ConnectionData.Address = "127.0.0.1"; // Default to localhost
        }
    }

    private void UpdateLocalName()
    {
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
        {
            LocalPlayerName = nameInputField.text;
        }
    }
}
