using Unity.Netcode;
using UnityEngine;

public class StartNetwork : MonoBehaviour
{
    public void StartSever()
    {
        NetworkManager.Singleton.StartServer();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient ();
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }
}
