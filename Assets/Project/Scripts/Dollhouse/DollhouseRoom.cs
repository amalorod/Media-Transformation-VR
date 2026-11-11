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

    /// <summary>
    /// Konfiguriert den XRGrabInteractable so, dass kein unabsichtliches Weit-Greifen passiert
    /// und dynamische Hand-Positionierung aktiv ist.
    /// </summary>
    private void ConfigureGrabSettings()
    {
        if (grabInteractable == null) return;

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
        // 1. Zuerst Neigung korrigieren: Pitch=0, Roll=0, Yaw auf 0/90/180/270 runden
        SnapUprightAndQuantizeRotation();

        // 2. Prüfen: Auf Grid platzieren oder als Ablage auf dem Tisch parken
        TryPlaceOnGridOrTable();

        // 3. Echte Nora-Welt (1:20) spiegeln
        UpdateWorldRoom();

        // 4. Tür-Sockets überprüfen
        CheckDoorConnections();
    }

    /// <summary>
    /// Erzwingt, dass der Raum absolut waagerecht steht (X=0, Z=0)
    /// und richtet die Y-Rotation exakt an 0°, 90°, 180° oder 270° relativ zum Grid aus.
    /// </summary>
    private void SnapUprightAndQuantizeRotation()
    {
        Transform reference = (grid != null) ? grid.transform : transform.parent;
        Quaternion refRotation = (reference != null) ? reference.rotation : Quaternion.identity;

        // Relative Rotation zum Grid berechnen
        Quaternion localRot = Quaternion.Inverse(refRotation) * transform.rotation;
        Vector3 euler = localRot.eulerAngles;

        // Strikte 90°-Schritte auf der Y-Achse
        float snappedYaw = Mathf.Round(euler.y / 90f) * 90f;
        snappedYaw = ((snappedYaw % 360) + 360) % 360;

        // Pitch (X) und Roll (Z) sind IMMER 0!
        Quaternion cleanLocalRot = Quaternion.Euler(0f, snappedYaw, 0f);

        // Zurück in Welt-Rotation
        transform.rotation = refRotation * cleanLocalRot;
    }

    /// <summary>
    /// Platziert den Raum entweder im Grid ODER lässt ihn frei außerhalb auf dem Tisch liegen!
    /// </summary>
    private void TryPlaceOnGridOrTable()
    {
        if (grid == null) return;

        float currentYaw = GetLocalYaw();

        // Liegt der Raum innerhalb der Grid-Grenzen?
        if (grid.IsWithinGridBounds(transform.position))
        {
            if (grid.TryGetGridCoordinates(transform.position, width, depth, currentYaw, out int cellX, out int cellZ))
            {
                if (grid.CanPlaceRoom(this, cellX, cellZ, currentYaw))
                {
                    // Erfolgreich auf dem Grid platziert!
                    Vector3 snappedWorldPos = grid.GetWorldPositionForCell(cellX, cellZ, width, depth, currentYaw);
                    transform.position = snappedWorldPos;
                    grid.OccupyRoom(this, cellX, cellZ, currentYaw);

                    lastValidPosition = transform.position;
                    lastValidRotation = transform.rotation;
                    hasValidGridPosition = true;
                    isPlacedOnGrid = true;
                    return;
                }
            }

            // Falls innerhalb des Grids, aber Slot belegt: Zurück zur letzten gültigen Position springen
            if (hasValidGridPosition)
            {
                transform.position = lastValidPosition;
                transform.rotation = lastValidRotation;

                if (grid.TryGetGridCoordinates(lastValidPosition, width, depth, GetLocalYaw(), out int prevX, out int prevZ))
                {
                    grid.OccupyRoom(this, prevX, prevZ, GetLocalYaw());
                    isPlacedOnGrid = true;
                }
                return;
            }
        }

        // AUßERHALB DES GRIDS (AUF DEM TISCH ABLEGEN):
        // Der Spieler hat den Raum außerhalb des Grids losgelassen -> Er bleibt auf dem Tisch liegen!
        isPlacedOnGrid = false;
        grid.ClearRoom(this);

        // Höhe auf Tisch-Ebene anpassen (damit er nicht in der Luft schwebt)
        Vector3 pos = transform.position;
        pos.y = grid.transform.position.y + grid.surfaceHeightOffset;
        transform.position = pos;

        // Letzte Ablage merken
        lastValidPosition = transform.position;
        lastValidRotation = transform.rotation;
    }

    private float GetLocalYaw()
    {
        Transform reference = (grid != null) ? grid.transform : transform.parent;
        Quaternion refRotation = (reference != null) ? reference.rotation : Quaternion.identity;
        Quaternion localRot = Quaternion.Inverse(refRotation) * transform.rotation;
        return localRot.eulerAngles.y;
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