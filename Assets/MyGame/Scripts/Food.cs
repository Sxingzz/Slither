using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.BossRoom.Infrastructure;

public class Food : NetworkBehaviour
{
    public GameObject prefab;

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

        NetworkObjectPool.Singleton.ReturnNetworkObject(NetworkObject, prefab);
        NetworkObject.Despawn(); // remove khỏi mạng
    }
}
