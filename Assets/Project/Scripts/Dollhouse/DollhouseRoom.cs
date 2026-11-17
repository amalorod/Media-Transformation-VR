using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DollhouseRoom : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private DollhouseGrid grid;

    [Header("XR")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Room Size")]
    [SerializeField] private int width = 1;
    [SerializeField] private int depth = 1;

    [Header("Magnetic Snap")]
    [SerializeField] private float snapDistance = 0.04f;

    [Header("World Synchronization")]
    [SerializeField] private Transform worldRoom;
    [SerializeField] private Transform worldGridOrigin;
    [SerializeField] private float worldScaleFactor = 20f;

    [Header("Door Connections")]
    [SerializeField] private DoorSocket[] doorSockets;

    private Collider roomCollider;


    public int Width => width;
    public int Depth => depth;

    private Vector3 lastValidPosition;
    private bool hasValidPosition;

    private void Awake()
    {
        roomCollider = GetComponent<Collider>();

        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (doorSockets == null || doorSockets.Length == 0)
        {
            doorSockets = GetComponentsInChildren<DoorSocket>();
        }
    }



    private void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }


    private void CheckDoorConnections()
    {
        if (doorSockets == null || doorSockets.Length == 0)
            return;

        Debug.Log($"CHECK CONNECTIONS: {name}");

        DoorSocket[] allSockets =
            FindObjectsByType<DoorSocket>(
                FindObjectsSortMode.None
            );

        Debug.Log($"Gefundene DoorSockets: {allSockets.Length}");

        foreach (DoorSocket ownSocket in doorSockets)
        {
            ownSocket.SetConnected(false);

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == ownSocket)
                    continue;

                if (otherSocket.transform.IsChildOf(transform))
                    continue;

                if (ownSocket.IsNear(otherSocket))
                {
                    ownSocket.SetConnected(true);
                    otherSocket.SetConnected(true);

                    Debug.Log(
                        $"CONNECTED: {ownSocket.name} <-> {otherSocket.name}"
                    );

                    break;
                }
            }
        }
    }
    private void OnReleased(SelectExitEventArgs args)
    {
        SnapRotation();

        if (IsInsideGrid())
        {
            TryPlaceOnGrid();
        }

        TryMagneticSnap();
        UpdateWorldRoom();
        CheckDoorConnections();
    }

    private bool IsInsideGrid()
    {
        if (grid == null)
            return false;

        Vector3 localPosition =
            grid.transform.InverseTransformPoint(transform.position);

        bool rotatedSideways =
            Mathf.RoundToInt(transform.eulerAngles.y / 90f) % 2 != 0;

        int occupiedWidth = rotatedSideways ? depth : width;
        int occupiedDepth = rotatedSideways ? width : depth;

        float maxX = (grid.gridWidth - occupiedWidth) * grid.cellSize;
        float maxZ = (grid.gridDepth - occupiedDepth) * grid.cellSize;

        return localPosition.x >= 0f &&
            localPosition.x <= maxX &&
            localPosition.z >= 0f &&
            localPosition.z <= maxZ;
    }

    private void SnapRotation()
    {
        float snappedY = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;

        transform.rotation = Quaternion.Euler(
            0f,
            snappedY,
            0f
        );
    }


    private void TryMagneticSnap()
    {
        if (doorSockets == null || doorSockets.Length == 0)
            return;

        DoorSocket[] allSockets = FindObjectsByType<DoorSocket>(
            FindObjectsSortMode.None
        );

        DoorSocket nearestOwnSocket = null;
        DoorSocket nearestTargetSocket = null;
        float nearestDistance = float.MaxValue;

        foreach (DoorSocket ownSocket in doorSockets)
        {
            if (!IsEastOrWestSocket(ownSocket))
                continue;

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == ownSocket ||
                    otherSocket.transform.IsChildOf(transform) ||
                    !AreCompatibleSockets(ownSocket, otherSocket))
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    ownSocket.transform.position,
                    otherSocket.transform.position
                );

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestOwnSocket = ownSocket;
                    nearestTargetSocket = otherSocket;
                }
            }
        }

        if (nearestOwnSocket != null &&
            nearestTargetSocket != null &&
            nearestDistance <= snapDistance)
        {
            SnapToDoorSocket(nearestOwnSocket, nearestTargetSocket);
        }
    }

    private bool IsEastOrWestSocket(DoorSocket socket)
    {
        if (socket == null)
            return false;

        return socket.name.Contains("Door_Socket_East") ||
            socket.name.Contains("Door_Socket_West");
    }

    private bool AreCompatibleSockets(
        DoorSocket first,
        DoorSocket second)
    {
        bool firstIsEast = first.name.Contains("Door_Socket_East");
        bool secondIsEast = second.name.Contains("Door_Socket_East");

        return firstIsEast != secondIsEast;
    }

    private void SnapToDoorSocket(
        DoorSocket ownSocket,
        DoorSocket targetSocket)
    {
        Vector3 offset =
            targetSocket.transform.position -
            ownSocket.transform.position;

        transform.position += offset;
    }

    private void TryPlaceOnGrid()
    {
        if (grid == null)
        {
            Debug.LogWarning($"{name}: Kein DollhouseGrid zugewiesen.");
            return;
        }

        Vector3 snappedPosition = grid.GetNearestGridPosition(transform.position);

        Vector3 localPosition = grid.transform.InverseTransformPoint(snappedPosition);

        int startX = Mathf.RoundToInt(localPosition.x / grid.cellSize);
        int startZ = Mathf.RoundToInt(localPosition.z / grid.cellSize);

        // Alte Belegung temporär freigeben
        grid.ClearRoom(this);

        if (grid.CanPlaceRoom(this, startX, startZ))
        {
            transform.position = snappedPosition;

            grid.OccupyRoom(this, startX, startZ);

            lastValidPosition = snappedPosition;
            hasValidPosition = true;
        }
        else if (hasValidPosition)
        {
            // Zur letzten gültigen Position zurück
            transform.position = lastValidPosition;

            Vector3 previousLocal =
                grid.transform.InverseTransformPoint(lastValidPosition);

            int previousX =
                Mathf.RoundToInt(previousLocal.x / grid.cellSize);

            int previousZ =
                Mathf.RoundToInt(previousLocal.z / grid.cellSize);

            grid.OccupyRoom(this, previousX, previousZ);
        }
    }


    private void UpdateWorldRoom()
    {
        if (worldRoom == null || grid == null || worldGridOrigin == null)
            return;

        // Position relativ zum kleinen Grid
        Vector3 localMiniPosition =
            grid.transform.InverseTransformPoint(transform.position);

        // Skalierung auf die echte Welt übertragen
        Vector3 scaledPosition = new Vector3(
            localMiniPosition.x * worldScaleFactor,
            0f,
            localMiniPosition.z * worldScaleFactor
        );

        // Position relativ zum Referenzpunkt der echten Welt berechnen
        worldRoom.position =
            worldGridOrigin.TransformPoint(scaledPosition);

        // Rotation relativ zum kleinen Grid übernehmen
        Quaternion relativeRotation =
            Quaternion.Inverse(grid.transform.rotation) * transform.rotation;

        worldRoom.rotation =
            worldGridOrigin.rotation * relativeRotation;
    }

}
