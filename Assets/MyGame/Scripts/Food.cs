using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Food : NetworkBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (!NetworkManager.Singleton.IsServer) return;

        if (collision.TryGetComponent(out PlayerLength playerLength))
        {
            playerLength.AddLength();
        }
        else if (collision.TryGetComponent(out Tail tail))
        {
            tail.networkedOwner.GetComponent<PlayerLength>().AddLength();
        }

        NetworkObject.Despawn(); // remove khỏi mạng
    }
}
