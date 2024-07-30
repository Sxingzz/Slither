using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace Unity.BossRoom.Infrastructure
{
    /// <summary>
    /// Object Pool for networked objects, used for controlling how objects are spawned by Netcode. Netcode by default
    /// will allocate new memory when spawning new objects. With this Networked Pool, we're using the ObjectPool to
    /// reuse objects.
    /// Boss Room uses this for projectiles. In theory it should use this for imps too, but we wanted to show vanilla spawning vs pooled spawning.
    /// Hooks to NetworkManager's prefab handler to intercept object spawning and do custom actions.
    /// </summary>
    public class NetworkObjectPool : NetworkBehaviour
    {
        public static NetworkObjectPool Singleton { get; private set; }

        [SerializeField]
        List<PoolConfigObject> PooledPrefabsList;

        HashSet<GameObject> m_Prefabs = new HashSet<GameObject>();

        Dictionary<GameObject, ObjectPool<NetworkObject>> m_PooledObjects = new Dictionary<GameObject, ObjectPool<NetworkObject>>();

        Dictionary<GameObject, int> nonPooledObjects = new Dictionary<GameObject, int>();

        public void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject); // đảm bảo rằng chỉ có 1 singleton trong game
            }
            else
            {
                Singleton = this;
            }
        }

        public override void OnNetworkSpawn()
        {
            // Registers all objects in PooledPrefabsList to the cache.
            foreach (var configObject in PooledPrefabsList)
            {
                nonPooledObjects[configObject.Prefab] = 0;
                RegisterPrefabInternal(configObject.Prefab, configObject.PrewarmCount);
                nonPooledObjects[configObject.Prefab] = 0; // theo dõi số lượng prefabs dc lấy ra, ban đầu chưa lấy ra nên set là 0
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unregisters all objects in PooledPrefabsList from the cache.
            foreach (var prefab in m_Prefabs)
            {
                // Unregister Netcode Spawn handlers
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
                m_PooledObjects[prefab].Clear();
            }
            m_PooledObjects.Clear();
            m_Prefabs.Clear();
        }

        public void OnValidate() // hàm check null
        {
            for (var i = 0; i < PooledPrefabsList.Count; i++)
            {
                var prefab = PooledPrefabsList[i].Prefab;
                if (prefab != null)
                {
                    Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), $"{nameof(NetworkObjectPool)}: Pooled prefab \"{prefab.name}\" at index {i.ToString()} has no {nameof(NetworkObject)} component.");
                    // kiểm tra xem prefab có thành phần NetworkObject hay k, nếu k một lỗi assert sẽ dc ném ra.
                }
            }
        }

        /// <summary>
        /// Gets an instance of the given prefab from the pool. The prefab must be registered to the pool.
        /// </summary>
        /// <remarks>
        /// To spawn a NetworkObject from one of the pools, this must be called on the server, then the instance
        /// returned from it must be spawned on the server. This method will then also be called on the client by the
        /// PooledPrefabInstanceHandler when the client receives a spawn message for a prefab that has been registered
        /// here.
        /// </remarks>
        /// <param name="prefab"></param>
        /// <param name="position">The position to spawn the object at.</param>
        /// <param name="rotation">The rotation to spawn the object with.</param>
        /// <returns></returns>
        public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var networkObject = m_PooledObjects[prefab].Get();

            var noTransform = networkObject.transform;
            noTransform.position = position;
            noTransform.rotation = rotation;

            nonPooledObjects[prefab]++;

            return networkObject;
        }

        /// <summary>
        /// Return an object to the pool (reset objects before returning).
        /// </summary>
        public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
        {
            m_PooledObjects[prefab].Release(networkObject);
            nonPooledObjects[prefab]--;
        }

        public int GetCurrentPrefabCount(GameObject prefab) // số lượng prefab đang dc sử dụng tức nằm ngoài pool
        {
            return nonPooledObjects[prefab];
        }

        /// <summary>
        /// Builds up the cache for a prefab.
        /// </summary>
        void RegisterPrefabInternal(GameObject prefab, int prewarmCount)
        {
            NetworkObject CreateFunc() //tạo ra một bản sao của prefab và trả về thành phần NetworkObject của nó.
            {
                return Instantiate(prefab).GetComponent<NetworkObject>();
            }

            void ActionOnGet(NetworkObject networkObject) // được gọi khi một đối tượng được lấy ra từ pool.
            {
                networkObject.gameObject.SetActive(true);
            }

            void ActionOnRelease(NetworkObject networkObject) // được gọi khi một đối tượng được trả lại vào pool.
            {
                networkObject.gameObject.SetActive(false);
            }

            void ActionOnDestroy(NetworkObject networkObject) // được gọi khi một đối tượng trong pool bị phá hủy
            {
                Destroy(networkObject.gameObject);
            }

            m_Prefabs.Add(prefab); //Tập hợp này lưu trữ tất cả các prefab đã được đăng ký.

            // Create the pool
            m_PooledObjects[prefab] = new ObjectPool<NetworkObject>(CreateFunc, ActionOnGet, ActionOnRelease, ActionOnDestroy, defaultCapacity: prewarmCount);

            // Populate the pool
            var prewarmNetworkObjects = new List<NetworkObject>(); // lưu trữ các đối tượng sẽ lấy từ pool.
            for (var i = 0; i < prewarmCount; i++)
            {
                prewarmNetworkObjects.Add(m_PooledObjects[prefab].Get());// dùng để lấy một đối tượng từ m_PooledObjects và thêm vào list prewarmNetworkObjects 
            }
            foreach (var networkObject in prewarmNetworkObjects)  
            {
                m_PooledObjects[prefab].Release(networkObject);// trả các đối tượng trong prewarmNetworkObjects về lại m_PooledObjects
            }

            // Register Netcode Spawn handlers
            NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new PooledPrefabInstanceHandler(prefab, this));
            // PrefabHandler quản lý các hành động liên quan đến việc spawn và despawn các prefab mạng
        }
    }

    [Serializable]
    struct PoolConfigObject
    {
        public GameObject Prefab;
        public int PrewarmCount;
    }

    class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        GameObject m_Prefab;
        NetworkObjectPool m_Pool;

        public PooledPrefabInstanceHandler(GameObject prefab, NetworkObjectPool pool)
        {
            m_Prefab = prefab;
            m_Pool = pool;
        }

        NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return m_Pool.GetNetworkObject(m_Prefab, position, rotation);
        }

        void INetworkPrefabInstanceHandler.Destroy(NetworkObject networkObject)
        {
            m_Pool.ReturnNetworkObject(networkObject, m_Prefab);
        }
    }

}