using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puppenhaus-Raum (Miniatur) für die Nora-Mechanik (A Fisherman's Tale).
/// Stellt sicher, dass Räume immer plan zum Boden stehen, sich in 90°-Schritten ausrichten,
/// auch außerhalb des Boards abgelegt werden können und Noras Standraum geschützt ist.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DollhouseRoom : MonoBehaviour
{
    [Header("Grid & Board")]
    [SerializeField] private DollhouseGrid grid;

    [Header("XR Interaktion")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [Tooltip("Nur physisches Greifen erlauben (deaktiviert Ray-Grab aus weiter Entfernung)")]
    [SerializeField] private bool requireDirectHandGrab = true;

    [Header("Raumgröße (in Grid-Zellen)")]
    [SerializeField] private int width = 1;
    [SerializeField] private int depth = 1;

    [Header("Nora / Spieler-Standort (Wohnzimmer-Schutz)")]
    [Tooltip("Ist dies der Raum, in dem Nora sich aktuell physisch befindet?")]
    [SerializeField] private bool isPlayerCurrentRoom = false;
    [Tooltip("Wenn aktiv, kann der Raum, in dem Nora steht, nicht angehoben werden (kein Herunterfallen)")]
    [SerializeField] private bool lockWhenPlayerInside = true;

    [Header("Welt-Synchronisation (1:20 Maßstab)")]
    [SerializeField] private Transform worldRoom;
    [SerializeField] private Transform worldGridOrigin;
    [SerializeField] private float worldScaleFactor = 20f;
    [Tooltip("Soll der World-Raum ausgeblendet/inaktiviert werden, wenn die Miniatur auf dem Tisch liegt?")]
    [SerializeField] private bool hideWorldRoomWhenOffGrid = false;

    [Header("Tür-Verbindungen (Sockets)")]
    [SerializeField] private DoorSocket[] doorSockets;

    // Status-Variablen
    private Collider roomCollider;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;
    private bool hasValidGridPosition = false;
    private bool isPlacedOnGrid = false;

    public int Width => width;
    public int Depth => depth;
    public bool IsPlacedOnGrid => isPlacedOnGrid;
    public bool IsPlayerCurrentRoom => isPlayerCurrentRoom;

    private void Awake()
    {
        roomCollider = GetComponent<Collider>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (doorSockets == null || doorSockets.Length == 0)
        {
            doorSockets = GetComponentsInChildren<DoorSocket>();
        }
    }

        ConfigureGrabSettings();
    }

    private void Start()
    {
        // Initial-Zustand: Auf Grid oder Tisch einrasten
        SnapUprightAndQuantizeRotation();
        TryPlaceOnGridOrTable();
        UpdateWorldRoom();
        CheckDoorConnections();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void LateUpdate()
    {
        if (grabInteractable == null || !grabInteractable.isSelected)
            return;

        Quaternion boardRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;

        Quaternion boardLocalRotation =
            Quaternion.Inverse(boardRotation) * transform.rotation;

        Vector3 localEuler = boardLocalRotation.eulerAngles;

        transform.rotation = boardRotation * Quaternion.Euler(
            0f,
            localEuler.y,
            0f
        );
    }


    private void CheckDoorConnections()
    {
        if (doorSockets == null || doorSockets.Length == 0)
            return;

        Debug.Log($"CHECK CONNECTIONS: {name}");

        // Dynamischer Attach-Punkt verhindert ruckartiges Springen zur Hand
        grabInteractable.useDynamicAttach = true;

        // Nora-Wohnzimmer sperren, falls gewünscht
        if (isPlayerCurrentRoom && lockWhenPlayerInside)
        {
            grabInteractable.enabled = false;
        }

        // Um Ray-Grabs aus 100m zu unterbinden:
        // Setze im Unity-Inspector den Interaction Layer z. B. auf "DollhouseDirect"
        // und erlaube diesen Layer NUR dem XR Direct Interactor (nicht dem XR Ray Interactor)!
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Wenn hochgehoben, belegte Grid-Zellen vorübergehend freigeben
        if (grid != null && isPlacedOnGrid)
        {
            grid.ClearRoom(this);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        SnapRotation();

        bool insideGrid = IsInsideGrid();

        if (!insideGrid)
        {
            TryMagneticSnap();
            CheckDoorConnections();
            return;
        }

        TryPlaceOnGrid();
        TryMagneticSnap();
        RefreshGridOccupancy();
        UpdateWorldRoom();

        // 4. Tür-Sockets überprüfen
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
        Quaternion boardRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;

        Quaternion boardLocalRotation =
            Quaternion.Inverse(boardRotation) * transform.rotation;

        float snappedY =
            Mathf.Round(boardLocalRotation.eulerAngles.y / 90f) * 90f;

        transform.rotation = boardRotation * Quaternion.Euler(
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
        Vector3 worldOffset =
            targetSocket.transform.position -
            ownSocket.transform.position;

        Quaternion boardRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;

        Vector3 localOffset =
            Quaternion.Inverse(boardRotation) * worldOffset;

        localOffset.y = 0f;

        transform.position += boardRotation * localOffset;
    }

    private void RefreshGridOccupancy()
    {
        if (grid == null)
            return;

        Vector3 localPosition =
            grid.transform.InverseTransformPoint(transform.position);

        int startX = Mathf.RoundToInt(localPosition.x / grid.cellSize);
        int startZ = Mathf.RoundToInt(localPosition.z / grid.cellSize);

        grid.ClearRoom(this);

        if (grid.CanPlaceRoom(this, startX, startZ))
        {
            grid.OccupyRoom(this, startX, startZ);
            lastValidPosition = transform.position;
            hasValidPosition = true;
        }
    }

    /// <summary>
    /// Synchronisiert den lebensgroßen Raum (WorldRoot) mit der Puppenhaus-Miniatur.
    /// </summary>
    private void UpdateWorldRoom()
    {
        if (worldRoom == null || grid == null || worldGridOrigin == null)
            return;

        if (!isPlacedOnGrid && hideWorldRoomWhenOffGrid)
        {
            worldRoom.gameObject.SetActive(false);
            return;
        }

        worldRoom.gameObject.SetActive(true);

        // Position relativ zum kleinen Grid berechnen
        Vector3 localMiniPosition = grid.transform.InverseTransformPoint(transform.position);

        // 1:20 Skalierung auf die echte Welt übertragen
        Vector3 scaledPosition = new Vector3(
            localMiniPosition.x * worldScaleFactor,
            0f, // Bleibt immer eben zum Boden
            localMiniPosition.z * worldScaleFactor
        );

        worldRoom.position = worldGridOrigin.TransformPoint(scaledPosition);

        // Rotation synchronisieren
        Quaternion relativeRotation = Quaternion.Inverse(grid.transform.rotation) * transform.rotation;
        worldRoom.rotation = worldGridOrigin.rotation * relativeRotation;
    }

    /// <summary>
    /// Prüft, ob Sockets dieses Raumes benachbarten Sockets anderer Räume gegenüberstehen.
    /// </summary>
    public void CheckDoorConnections()
    {
        DoorSocket[] allSockets = FindObjectsByType<DoorSocket>(FindObjectsSortMode.None);

        foreach (DoorSocket ownSocket in doorSockets)
        {
            if (ownSocket == null) continue;
            ownSocket.SetConnected(false);

            if (!isPlacedOnGrid) continue; // Außerhalb des Grids keine Türverbindungen

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == null || otherSocket == ownSocket)
                    continue;

                if (otherSocket.transform.IsChildOf(transform))
                    continue;

                if (ownSocket.IsNearAndFacing(otherSocket))
                {
                    ownSocket.SetConnected(true);
                    otherSocket.SetConnected(true);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Kann extern aufgerufen werden, wenn Nora den Raum wechselt.
    /// </summary>
    public void SetPlayerInside(bool inside)
    {
        isPlayerCurrentRoom = inside;
        if (grabInteractable != null && lockWhenPlayerInside)
        {
            grabInteractable.enabled = !inside;
        }
    }
}