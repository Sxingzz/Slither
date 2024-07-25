using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerLength : NetworkBehaviour
{
    [SerializeField] private GameObject tailPrefab;

    // ushort đại diện cho một số nguyên không âm có phạm vi giá trị từ 0 đến 65,535
    public NetworkVariable<ushort> length = new(1, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private List<GameObject> _tails;
    private Transform _lastTail;
    private Collider2D _collider2D;

    public override void OnNetworkSpawn() // hàm này dc gọi khi đối tượng này dc tạo
    {
        base.OnNetworkSpawn();
        _tails = new List<GameObject>(); // check null
        _lastTail = transform;
        _collider2D = GetComponent<Collider2D>(); // get collider
        if (!IsServer) length.OnValueChanged += LengthChanged; // client updates tails
    }

    // this will be called by the sever
    [ContextMenu("Add Length")]
    public void AddLength()
    {
        length.Value += 1;
        InstantiateTail();
    }

    private void LengthChanged(ushort previousValue, ushort newValue)
    {
        Debug.Log("LengthChanged callback");
        InstantiateTail();


    }

    private void InstantiateTail()
    {
        GameObject tailGameObject = Instantiate(tailPrefab, transform.position, Quaternion.identity);
        tailGameObject.GetComponent<SpriteRenderer>().sortingOrder = -length.Value;
        // out chỉ định rằng một tham số của phương thức sẽ được gán một giá trị trong phương thức đó 
        if (tailGameObject.TryGetComponent(out Tail tail))
        {
            tail.networkedOwner = transform;
            tail.followTransform = _lastTail;
            _lastTail = tailGameObject.transform;
            Physics2D.IgnoreCollision(tailGameObject.GetComponent<Collider2D>(), _collider2D);   
        }
        _tails.Add(tailGameObject);
    }
}
