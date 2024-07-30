using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    private const int MaxPlayers = 3;

    private void Start()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request // chứa thông tin về yêu cầu kết nối từ client.
                              , NetworkManager.ConnectionApprovalResponse response) // chứa thông tin phản hồi về việc chấp nhận hoặc từ chối kết nối.
    {
        Debug.Log("Connect Approval");
        response.Approved = true; // Mặc định chấp nhận kết nối.
        response.CreatePlayerObject = true; // Yêu cầu tạo một đối tượng player cho client.
        response.PlayerPrefabHash = null; // Sử dụng prefab mặc định được cấu hình trong NetworkManager.

        if (NetworkManager.Singleton.ConnectedClients.Count >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is Full";
        }
        response.Pending = false; //Kết thúc quá trình xử lý kết nối.
    }
}
