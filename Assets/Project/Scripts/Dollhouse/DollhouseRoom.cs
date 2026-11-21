using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puppenhaus-Raum (Miniatur) für die Nora-Mechanik.
/// Räume stehen plan zum Boden, richten sich in 90°-Schritten aus,
/// können außerhalb des Boards abgelegt werden und synchronisieren
/// ihre finale Position mit der lebensgroßen Welt.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DollhouseRoom : MonoBehaviour
{
    [Header("Grid & Board")]
    [SerializeField] private DollhouseGrid grid;

    [Header("XR Interaktion")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Tooltip("Nur physisches Greifen erlauben.")]
    [SerializeField] private bool requireDirectHandGrab = true;

    [Header("Raumgröße (in Grid-Zellen)")]
    [SerializeField] private int width = 1;
    [SerializeField] private int depth = 1;

    [Header("Magnetic Snap")]
    [SerializeField] private float snapDistance = 0.04f;

    [Header("Nora / Spieler-Standort")]
    [Tooltip("Ist dies der Raum, in dem Nora sich aktuell physisch befindet?")]
    [SerializeField] private bool isPlayerCurrentRoom = false;

    [Tooltip("Wenn aktiv, kann der Raum, in dem Nora steht, nicht angehoben werden.")]
    [SerializeField] private bool lockWhenPlayerInside = true;

    [Header("Welt-Synchronisation")]
    [SerializeField] private Transform worldRoom;
    [SerializeField] private Transform worldGridOrigin;
    [SerializeField] private float worldScaleFactor = 20f;

    [Tooltip("World-Raum deaktivieren, wenn die Miniatur außerhalb des Grids liegt.")]
    [SerializeField] private bool hideWorldRoomWhenOffGrid = false;

    [Header("Tür-Verbindungen")]
    [SerializeField] private DoorSocket[] doorSockets;

    private Collider roomCollider;
    private Vector3 lastValidPosition;
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
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (doorSockets == null || doorSockets.Length == 0)
        {
            doorSockets = GetComponentsInChildren<DoorSocket>();
        }

        ConfigureGrabSettings();
    }

    private void Start()
    {
        SnapUprightAndQuantizeRotation();
        TryPlaceOnGridOrTable();
        UpdateWorldRoom();
        CheckDoorConnections();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void LateUpdate()
    {
        if (grabInteractable == null ||
            !grabInteractable.isSelected ||
            grid == null)
        {
            return;
        }

        // Während des Greifens X/Z-Neigung entfernen.
        // Nur die Rotation um die Board-Y-Achse bleibt erhalten.
        Quaternion boardRotation = grid.transform.rotation;

        Quaternion boardLocalRotation =
            Quaternion.Inverse(boardRotation) * transform.rotation;

        Vector3 localEuler = boardLocalRotation.eulerAngles;

        transform.rotation = boardRotation * Quaternion.Euler(
            0f,
            localEuler.y,
            0f
        );
    }

    private void ConfigureGrabSettings()
    {
        if (grabInteractable == null)
            return;

        // Verhindert ein starkes Springen zum Controller beim Greifen.
        grabInteractable.useDynamicAttach = true;

        if (isPlayerCurrentRoom && lockWhenPlayerInside)
        {
            grabInteractable.enabled = false;
        }

        // requireDirectHandGrab wird derzeit bewusst nicht automatisch
        // auf Interaction Layers angewendet.
        // Die Layer-Konfiguration erfolgt im Unity-Inspector.
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (grid != null && isPlacedOnGrid)
        {
            grid.ClearRoom(this);
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Zuerst gerade stellen und auf 0/90/180/270 Grad runden.
        SnapUprightAndQuantizeRotation();

        // Außerhalb des Boards keine automatische Grid-/World-Platzierung.
        if (!IsInsideGrid())
        {
            isPlacedOnGrid = false;

            if (hideWorldRoomWhenOffGrid && worldRoom != null)
            {
                worldRoom.gameObject.SetActive(false);
            }

            CheckDoorConnections();
            return;
        }

        // Innerhalb des Boards:
        // Grid -> Door-Socket-Feinkorrektur -> Belegung -> World.
        TryPlaceOnGrid();
        TryMagneticSnap();
        RefreshGridOccupancy();
        UpdateWorldRoom();
        CheckDoorConnections();
    }

    private void SnapUprightAndQuantizeRotation()
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

    private void TryPlaceOnGridOrTable()
    {
        if (IsInsideGrid())
        {
            TryPlaceOnGrid();
            RefreshGridOccupancy();
            isPlacedOnGrid = true;
        }
        else
        {
            isPlacedOnGrid = false;
        }
    }

    private bool IsInsideGrid()
    {
        if (grid == null)
            return false;

        Vector3 localPosition =
            grid.transform.InverseTransformPoint(transform.position);

        bool rotatedSideways =
            Mathf.RoundToInt(
                transform.eulerAngles.y / 90f
            ) % 2 != 0;

        int occupiedWidth =
            rotatedSideways ? depth : width;

        int occupiedDepth =
            rotatedSideways ? width : depth;

        float maxX =
            (grid.gridWidth - occupiedWidth) * grid.cellSize;

        float maxZ =
            (grid.gridDepth - occupiedDepth) * grid.cellSize;

        return
            localPosition.x >= 0f &&
            localPosition.x <= maxX &&
            localPosition.z >= 0f &&
            localPosition.z <= maxZ;
    }

    private void TryPlaceOnGrid()
    {
        if (grid == null)
            return;

        float yRotation = transform.eulerAngles.y;

        if (!grid.TryGetGridCoordinates(
            transform.position,
            width,
            depth,
            yRotation,
            out int startX,
            out int startZ))
        {
            return;
        }

        grid.ClearRoom(this);

        if (grid.CanPlaceRoom(this, startX, startZ, yRotation))
        {
            Vector3 snappedPosition =
                grid.GetWorldPositionForCell(
                    startX,
                    startZ,
                    width,
                    depth,
                    yRotation
                );

            transform.position = snappedPosition;

            grid.OccupyRoom(
                this,
                startX,
                startZ,
                yRotation
            );

            lastValidPosition = snappedPosition;
            hasValidGridPosition = true;
            isPlacedOnGrid = true;

            return;
        }

        if (hasValidGridPosition)
        {
            transform.position = lastValidPosition;

            float previousRotation =
                transform.eulerAngles.y;

            if (grid.TryGetGridCoordinates(
                lastValidPosition,
                width,
                depth,
                previousRotation,
                out int previousX,
                out int previousZ))
            {
                grid.OccupyRoom(
                    this,
                    previousX,
                    previousZ,
                    previousRotation
                );
            }

            isPlacedOnGrid = true;
        }
    }

    private void TryMagneticSnap()
    {
        if (doorSockets == null ||
            doorSockets.Length == 0)
        {
            return;
        }

        DoorSocket[] allSockets =
            FindObjectsByType<DoorSocket>(
                FindObjectsSortMode.None
            );

        DoorSocket nearestOwnSocket = null;
        DoorSocket nearestTargetSocket = null;
        float nearestDistance = float.MaxValue;

        foreach (DoorSocket ownSocket in doorSockets)
        {
            if (ownSocket == null)
                continue;

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == null ||
                    otherSocket == ownSocket ||
                    otherSocket.transform.IsChildOf(transform))
                {
                    continue;
                }

                float distance =
                    Vector3.Distance(
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
            SnapToDoorSocket(
                nearestOwnSocket,
                nearestTargetSocket
            );
        }
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

        // Offset in Koordinaten des Boards umwandeln,
        // damit nur die horizontale Bewegung übernommen wird.
        Vector3 localOffset =
            Quaternion.Inverse(boardRotation) * worldOffset;

        localOffset.y = 0f;

        transform.position +=
            boardRotation * localOffset;
    }

    private void RefreshGridOccupancy()
    {
        if (grid == null || !IsInsideGrid())
        {
            isPlacedOnGrid = false;
            return;
        }

        float yRotation = transform.eulerAngles.y;

        if (!grid.TryGetGridCoordinates(
            transform.position,
            width,
            depth,
            yRotation,
            out int startX,
            out int startZ))
        {
            isPlacedOnGrid = false;
            return;
        }

        grid.ClearRoom(this);

        if (grid.CanPlaceRoom(
            this,
            startX,
            startZ,
            yRotation))
        {
            grid.OccupyRoom(
                this,
                startX,
                startZ,
                yRotation
            );

            lastValidPosition = transform.position;
            hasValidGridPosition = true;
            isPlacedOnGrid = true;
        }
    }

    private void UpdateWorldRoom()
    {
        if (worldRoom == null ||
            grid == null ||
            worldGridOrigin == null)
        {
            return;
        }

        if (!isPlacedOnGrid)
        {
            if (hideWorldRoomWhenOffGrid)
            {
                worldRoom.gameObject.SetActive(false);
            }

            return;
        }

        worldRoom.gameObject.SetActive(true);

        Vector3 localMiniPosition =
            grid.transform.InverseTransformPoint(
                transform.position
            );

        Vector3 scaledPosition = new Vector3(
            localMiniPosition.x * worldScaleFactor,
            0f,
            localMiniPosition.z * worldScaleFactor
        );

        worldRoom.position =
            worldGridOrigin.TransformPoint(
                scaledPosition
            );

        Quaternion relativeRotation =
            Quaternion.Inverse(grid.transform.rotation) *
            transform.rotation;

        worldRoom.rotation =
            worldGridOrigin.rotation *
            relativeRotation;
    }

    public void CheckDoorConnections()
    {
        if (doorSockets == null ||
            doorSockets.Length == 0)
        {
            return;
        }

        DoorSocket[] allSockets =
            FindObjectsByType<DoorSocket>(
                FindObjectsSortMode.None
            );

        foreach (DoorSocket ownSocket in doorSockets)
        {
            if (ownSocket == null)
                continue;

            ownSocket.SetConnected(false);

            if (!isPlacedOnGrid)
                continue;

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == null ||
                    otherSocket == ownSocket)
                {
                    continue;
                }

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

    public void SetPlayerInside(bool inside)
    {
        isPlayerCurrentRoom = inside;

        if (grabInteractable != null &&
            lockWhenPlayerInside)
        {
            grabInteractable.enabled = !inside;
        }
    }
}