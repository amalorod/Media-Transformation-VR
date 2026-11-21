using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Puppenhaus-Raum (Miniatur) für die Nora-Mechanik.
/// Räume stehen plan zum Boden, richten sich in 90°-Schritten aus,
/// können außerhalb des Boards abgelegt werden und synchronisieren
/// ihre finale Position mit der lebensgroßen Welt.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class DollhouseRoom : MonoBehaviour
{
    [Header("Grid & Board")]
    [SerializeField] private DollhouseGrid grid;

    [Header("XR Interaktion")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Raumgröße (in Grid-Zellen)")]
    [SerializeField] private int width = 1;
    [SerializeField] private int depth = 1;

    [Header("Magnetic Snap")]
    [FormerlySerializedAs("snapDistance")]
    [SerializeField] private float doorSnapDistance = 0.04f;

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
    private Rigidbody roomRigidbody;
    private bool isPlacedOnGrid = false;

    private Vector3 placementPreviewPosition;
    private Quaternion placementPreviewRotation;

    public int Width => width;
    public int Depth => depth;
    public bool IsPlacedOnGrid => isPlacedOnGrid;
    public bool IsPlayerCurrentRoom => isPlayerCurrentRoom;
    public bool HasValidPlacementPreview { get; private set; }
    public Vector3 PlacementPreviewPosition => placementPreviewPosition;
    public Quaternion PlacementPreviewRotation => placementPreviewRotation;

    private void Awake()
    {
        roomCollider = GetComponent<Collider>();
        roomRigidbody = GetComponent<Rigidbody>();

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
        if (grabInteractable == null || !grabInteractable.isSelected)
        {
            HasValidPlacementPreview = false;
            return;
        }

        if (grid != null)
        {
            // Während des Greifens X/Z-Neigung entfernen; Y bleibt frei drehbar.
            Quaternion boardRotation = grid.transform.rotation;
            Quaternion boardLocalRotation =
                Quaternion.Inverse(boardRotation) * transform.rotation;

            transform.rotation = boardRotation * Quaternion.Euler(
                0f,
                boardLocalRotation.eulerAngles.y,
                0f
            );
        }

        UpdatePlacementPreview();
    }

    private void ConfigureGrabSettings()
    {
        if (grabInteractable == null)
            return;

        // Verhindert ein starkes Springen zum Controller beim Greifen.
        grabInteractable.useDynamicAttach = true;
        grabInteractable.throwOnDetach = false;

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
        // Stoppt eine vorherige Fallbewegung, bevor XR die Kontrolle übernimmt.
        roomRigidbody.isKinematic = true;
        roomRigidbody.useGravity = false;
        roomRigidbody.linearVelocity = Vector3.zero;
        roomRigidbody.angularVelocity = Vector3.zero;
        HasValidPlacementPreview = false;

        if (grid != null && isPlacedOnGrid)
        {
            grid.ClearRoom(this);
            isPlacedOnGrid = false;
        }

        CheckDoorConnections();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        SnapUprightAndQuantizeRotation();
        HasValidPlacementPreview = false;

        // Außerhalb des Grids findet keinerlei Grid- oder World-Synchronisierung statt.
        if (!IsInsideGrid())
        {
            if (grid != null)
                grid.ClearRoom(this);

            isPlacedOnGrid = false;
            SetOffGridPhysics();

            if (hideWorldRoomWhenOffGrid && worldRoom != null)
                worldRoom.gameObject.SetActive(false);

            CheckDoorConnections();
            return;
        }

        // Auf dem Board bleibt der Raum kinematisch; zuerst wird die Zielzelle belegt.
        SetGridPhysics();
        if (TryPlaceOnGrid())
        {
            TryMagneticSnap();
            if (!TryPlaceAtCurrentPosition())
            {
                Debug.LogWarning(
                    $"The snapped position for {name} could not be represented by a free grid footprint; keeping the socket-aligned position.",
                    this);
            }
        }

        UpdateWorldRoom();
        CheckDoorConnections();
    }

    private void SnapUprightAndQuantizeRotation()
    {
        GetSnappedYRotation(transform.rotation, out float snappedY);
        Quaternion boardRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;

        transform.rotation = boardRotation * Quaternion.Euler(0f, snappedY, 0f);
    }

    private void GetSnappedYRotation(Quaternion rotation, out float snappedY)
    {
        Quaternion boardRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;
        Quaternion boardLocalRotation = Quaternion.Inverse(boardRotation) * rotation;
        snappedY = Mathf.Repeat(Mathf.Round(boardLocalRotation.eulerAngles.y / 90f) * 90f, 360f);
    }

    private void TryPlaceOnGridOrTable()
    {
        if (IsInsideGrid())
        {
            SetGridPhysics();
            TryPlaceOnGrid();
            return;
        }

        isPlacedOnGrid = false;
        SetOffGridPhysics();
    }

    private bool IsInsideGrid()
    {
        if (grid == null)
            return false;

        Vector3 localPosition = grid.transform.InverseTransformPoint(transform.position);
        GetSnappedYRotation(transform.rotation, out float yRotation);
        grid.GetEffectiveDimensions(width, depth, yRotation, out int occupiedWidth, out int occupiedDepth);

        float minX = occupiedWidth * grid.cellSize * 0.5f;
        float minZ = occupiedDepth * grid.cellSize * 0.5f;
        float maxX = (grid.gridWidth - occupiedWidth * 0.5f) * grid.cellSize;
        float maxZ = (grid.gridDepth - occupiedDepth * 0.5f) * grid.cellSize;

        return localPosition.x >= minX && localPosition.x <= maxX &&
               localPosition.z >= minZ && localPosition.z <= maxZ;
    }

    private void UpdatePlacementPreview()
    {
        HasValidPlacementPreview = IsInsideGrid() && TryGetPlacementTarget(
            transform.position,
            out placementPreviewPosition,
            out placementPreviewRotation,
            out _,
            out _);
    }

    private bool TryGetPlacementTarget(
        Vector3 requestedPosition,
        out Vector3 targetPosition,
        out Quaternion targetRotation,
        out int startX,
        out int startZ)
    {
        targetPosition = default;
        targetRotation = transform.rotation;
        startX = -1;
        startZ = -1;

        if (grid == null)
            return false;

        GetSnappedYRotation(transform.rotation, out float yRotation);
        Quaternion boardRotation = grid.transform.rotation;
        targetRotation = boardRotation * Quaternion.Euler(0f, yRotation, 0f);

        if (!grid.TryGetGridCoordinates(
            requestedPosition,
            width,
            depth,
            yRotation,
            out int requestedX,
            out int requestedZ))
        {
            return false;
        }

        if (grid.CanPlaceRoom(this, requestedX, requestedZ, yRotation))
        {
            startX = requestedX;
            startZ = requestedZ;
        }
        else if (!grid.FindNearestValidPosition(
            this,
            requestedX,
            requestedZ,
            yRotation,
            out startX,
            out startZ))
        {
            return false;
        }

        float roomBottomOffset = GetRoomBottomOffset();
        targetPosition = grid.GetWorldPositionForCell(
            startX,
            startZ,
            width,
            depth,
            yRotation,
            roomBottomOffset);
        return true;
    }

    private bool TryPlaceOnGrid()
    {
        if (grid == null)
            return false;

        if (!TryGetPlacementTarget(
            transform.position,
            out Vector3 targetPosition,
            out Quaternion targetRotation,
            out int startX,
            out int startZ))
        {
            GetSnappedYRotation(transform.rotation, out float requestedYRotation);
            if (grid.TryGetGridCoordinates(
                transform.position,
                width,
                depth,
                requestedYRotation,
                out int requestedX,
                out int requestedZ))
            {
                transform.rotation = grid.transform.rotation * Quaternion.Euler(0f, requestedYRotation, 0f);
                transform.position = grid.GetWorldPositionForCell(
                    requestedX,
                    requestedZ,
                    width,
                    depth,
                    requestedYRotation,
                    GetRoomBottomOffset());
                Debug.LogWarning($"No free grid position is available for {name}.", this);
            }

            isPlacedOnGrid = false;
            return false;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        grid.ClearRoom(this);
        GetSnappedYRotation(targetRotation, out float yRotation);
        grid.OccupyRoom(this, startX, startZ, yRotation);
        SetGridPhysics();
        isPlacedOnGrid = true;
        return true;
    }

    private bool TryPlaceAtCurrentPosition()
    {
        GetSnappedYRotation(transform.rotation, out float yRotation);
        if (!IsInsideGrid() || !grid.TryGetGridCoordinates(
            transform.position,
            width,
            depth,
            yRotation,
            out int startX,
            out int startZ))
        {
            return false;
        }

        if (!grid.CanPlaceRoom(this, startX, startZ, yRotation))
            return false;

        grid.OccupyRoom(this, startX, startZ, yRotation);
        isPlacedOnGrid = true;
        return true;
    }

    private float GetRoomBottomOffset()
    {
        Bounds bounds = roomCollider.bounds;
        Vector3 up = grid.transform.up;
        Vector3 absoluteUp = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
        float supportDistance = Vector3.Dot(absoluteUp, bounds.extents);
        float lowestProjection = Vector3.Dot(bounds.center, up) - supportDistance;
        return Vector3.Dot(transform.position, up) - lowestProjection;
    }

    private void SetGridPhysics()
    {
        roomRigidbody.useGravity = false;
        roomRigidbody.isKinematic = true;
        roomRigidbody.linearVelocity = Vector3.zero;
        roomRigidbody.angularVelocity = Vector3.zero;
    }

    private void SetOffGridPhysics()
    {
        roomRigidbody.isKinematic = false;
        roomRigidbody.useGravity = true;
        roomRigidbody.linearVelocity = Vector3.zero;
        roomRigidbody.angularVelocity = Vector3.zero;
    }

    private void TryMagneticSnap()
    {
        if (doorSockets == null ||
            doorSockets.Length == 0)
        {
            return;
        }

        DoorSocket[] allSockets =
          FindObjectsByType<DoorSocket>();

        DoorSocket nearestOwnSocket = null;
        DoorSocket nearestTargetSocket = null;
        float nearestDistance = float.MaxValue;

        foreach (DoorSocket ownSocket in doorSockets)
        {
            if (ownSocket == null)
                continue;

            foreach (DoorSocket otherSocket in allSockets)
            {
                if (otherSocket == null || otherSocket == ownSocket)
                    continue;

                DollhouseRoom otherRoom = otherSocket.GetComponentInParent<DollhouseRoom>();
                if (otherRoom == null || otherRoom == this || !otherRoom.isPlacedOnGrid ||
                    !ownSocket.IsCompatibleWith(otherSocket))
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    ownSocket.transform.position,
                    otherSocket.transform.position);

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
            nearestDistance <= doorSnapDistance)
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
        Vector3 offset = targetSocket.transform.position - ownSocket.transform.position;
        offset.y = 0f;
        transform.position += offset;

        if (Debug.isDebugBuild)
        {
            float snappedDistance = Vector3.Distance(
                ownSocket.transform.position,
                targetSocket.transform.position);
            if (snappedDistance > 0.001f)
            {
                Debug.LogWarning(
                    $"DoorSocket snap for {name} left a {snappedDistance:F4} m gap (Y is intentionally unchanged).",
                    this);
            }
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

        GetSnappedYRotation(transform.rotation, out float yRotation);
        float worldYaw = Mathf.Repeat(worldGridOrigin.eulerAngles.y + yRotation, 360f);
        worldRoom.rotation = Quaternion.Euler(0f, worldYaw, 0f);
    }

    public void CheckDoorConnections()
    {
        DoorSocket[] allSockets = FindObjectsByType<DoorSocket>();
        DollhouseRoom[] rooms = FindObjectsByType<DollhouseRoom>();
        HashSet<DoorSocket> connectedSockets = new HashSet<DoorSocket>();

        foreach (DollhouseRoom room in rooms)
        {
            if (room == null || !room.isPlacedOnGrid || room.doorSockets == null)
                continue;

            foreach (DoorSocket ownSocket in room.doorSockets)
            {
                if (ownSocket == null)
                    continue;

                foreach (DoorSocket otherSocket in allSockets)
                {
                    if (otherSocket == null ||
                        otherSocket == ownSocket ||
                        otherSocket.transform.IsChildOf(room.transform))
                    {
                        continue;
                    }

                    DollhouseRoom otherRoom = otherSocket.GetComponentInParent<DollhouseRoom>();
                    if (otherRoom == null || otherRoom == room || !otherRoom.isPlacedOnGrid)
                        continue;

                    if (ownSocket.IsNearAndCompatible(otherSocket))
                    {
                        connectedSockets.Add(ownSocket);
                        connectedSockets.Add(otherSocket);
                    }
                }
            }
        }

        foreach (DoorSocket socket in allSockets)
        {
            if (socket != null)
                socket.SetConnected(connectedSockets.Contains(socket));
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