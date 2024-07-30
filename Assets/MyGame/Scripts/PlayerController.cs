using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using JetBrains.Annotations;
using UnityEditor.PackageManager;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;

    [CanBeNull] public static event System.Action GameOverEvent;

    private Camera _mainCamera;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLength _playerLength;
    private bool _canCollide = true;

    private readonly ulong[] _targetClientArray = new ulong[1];

    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLength>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }

    private void Update()
    {
        if (!IsOwner || !Application.isFocused) return;
        // Movement
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
        transform.position = Vector3.MoveTowards(transform.position,
            mouseWorldCoordinates, Time.deltaTime * speed);

        // Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;
        }
    }

    [ServerRpc(RequireOwnership = false)] // false đánh dấu phương thức dc gọi từ client nhưng dc thực thi trên sever
    private void DeterminedCollisionWinnerServerRpc(PlayerData player1, PlayerData player2 ) // thêm SeverRpc vào cuối để dễ nhận biết
    {
        if (player1.Length > player2.Length)
        {
            WinInformationServerRpc(player1.Id, player2.Id);
        }
        else
        {
            WinInformationServerRpc(player2.Id, player1.Id);
        }
    }

    [ServerRpc(RequireOwnership = false)] // mặc định dc gọi từ client (Host) và dc thực thi trên server
    private void WinInformationServerRpc(ulong winner, ulong loser)
    {
        _targetClientArray[0] = winner; // mảng chứa các id của client thắng
        ClientRpcParams clientRpcParams = new ClientRpcParams // Tạo ClientRpcParams để gửi thông báo tới ID client thắng
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientArray // chỉ gửi cho client trong mảng _targetClientArray(chứa client thắng)
            }
        };
        AtePlayerClientRpc(clientRpcParams); // gửi thông báo cho client thắng

        _targetClientArray[0] = loser; // Mảng _targetClientArray được cập nhật lại với giá trị là ID của các client thua.
        clientRpcParams.Send.TargetClientIds = _targetClientArray; // chỉ gửi cho client thua trong mảng _targetClientArray
        GameOverClientRpc(clientRpcParams); // gửi thông báo cho client thua, gọi sự kiện thua, tắt mạng
    }

    [ClientRpc]
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return; // đảm bảo chỉ hiện thông báo trên client thắng, tức chủ sở hữu
        Debug.Log("You Ate a Player");
    }

    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        Debug.Log("you lose");
        GameOverEvent?.Invoke();
        NetworkManager.Singleton.Shutdown();
    }

    private IEnumerator CollisionCheckCouroutine()
    {
        _canCollide = false;
        yield return new WaitForSeconds(0.5f);
        _canCollide = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Player Collison");
        if (!collision.gameObject.CompareTag("Player")) return;
        if (!IsOwner) return;
        if (!_canCollide) return;

        StartCoroutine(CollisionCheckCouroutine());

        // Head-on Collision
        if (collision.gameObject.TryGetComponent(out PlayerLength playerLength))
        {
            Debug.Log("Head Collision");
            var player1 = new PlayerData()
            {
                Id = OwnerClientId,
                Length = GetComponent<PlayerLength>().length.Value
            };

            var player2 = new PlayerData()
            {
                Id = playerLength.OwnerClientId,
                Length = GetComponent<PlayerLength>().length.Value
            };

            DeterminedCollisionWinnerServerRpc(player1, player2);
        }
        else if (collision.gameObject.TryGetComponent(out Tail tail))
        {
            Debug.Log("Tail Collsion");
            WinInformationServerRpc(tail.networkedOwner // .networkedOwner xác định ai là chủ sở hữu của đối tượng Tail
                .GetComponent<PlayerController>().OwnerClientId, OwnerClientId);
        }
    }

    struct PlayerData : INetworkSerializable //  INetworkSerializable cần triễn khai để thực hiện truyền qua mạng
    {
        public ulong Id;
        public ushort Length;

        // BufferSerializer thực hiện các chức năng tuần tự hóa và giải tuần tự hóa.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter //IReaderWriter cho phép đọc và ghi dữ liệu
        {
            serializer.SerializeValue(ref Id); 
            serializer.SerializeValue(ref Length);
        }
    }



}
