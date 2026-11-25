using UnityEngine;

public enum DollhouseRoomSlotType
{
    None = 0,
    Office = 1,
    ChildrenRoom = 2,
    Kitchen = 3,
    Bedroom = 4
}

[DisallowMultipleComponent]
public sealed class DollhouseRoomSlot : MonoBehaviour
{
    [SerializeField] private string slotId;
    [SerializeField] private DollhouseRoomSlotType slotType;
    [SerializeField] private int floorIndex;
    [SerializeField] private float snapRadius = 0.4f;
    [SerializeField] private Transform snapTransform;
    [SerializeField] private Renderer markerRenderer;
    [SerializeField] private Color idleColor = new Color(0.26f, 0.30f, 0.34f, 1f);
    [SerializeField] private Color grabbedColor = new Color(1f, 0.66f, 0.16f, 1f);
    [SerializeField] private Color validColor = new Color(0.18f, 0.82f, 0.30f, 1f);
    [SerializeField] private bool occupied;
    [SerializeField] private DollhouseRoom occupyingModule;
    [SerializeField] private DollhouseGrid grid;
    [SerializeField] private int gridX;
    [SerializeField] private int gridZ;
    [SerializeField] private float gridYRotation;

    private static DollhouseRoom heldModule;
    private MaterialPropertyBlock propertyBlock;

    public string SlotId => slotId;
    public DollhouseRoomSlotType SlotType => slotType;
    public int FloorIndex => floorIndex;
    public float SnapRadius => snapRadius;
    public Transform SnapTransform => snapTransform != null ? snapTransform : transform;
    public Renderer MarkerRenderer => markerRenderer;
    public bool Occupied => occupied;
    public DollhouseRoom OccupyingModule => occupyingModule;
    public DollhouseGrid Grid => grid;
    public int GridX => gridX;
    public int GridZ => gridZ;
    public float GridYRotation => gridYRotation;

    private void Update()
    {
        RefreshMarker();
    }

    public bool Matches(DollhouseRoom room)
    {
        return room != null &&
               slotType != DollhouseRoomSlotType.None &&
               room.RequiredSlotType == slotType &&
               room.FloorIndex == floorIndex;
    }

    public bool IsAvailableFor(DollhouseRoom room)
    {
        return !occupied || occupyingModule == room;
    }

    public bool IsWithinSnapRadius(Vector3 position)
    {
        return Vector3.Distance(position, SnapTransform.position) <= snapRadius;
    }

    public bool TryOccupy(DollhouseRoom room)
    {
        if (!Matches(room) || !IsAvailableFor(room))
            return false;

        occupied = true;
        occupyingModule = room;
        RefreshMarker();
        return true;
    }

    public void Release(DollhouseRoom room)
    {
        if (occupyingModule != room)
            return;

        occupied = false;
        occupyingModule = null;
        RefreshMarker();
    }

    public static DollhouseRoomSlot FindCompatibleSlot(DollhouseRoom room, Vector3 position)
    {
        DollhouseRoomSlot best = null;
        float bestDistance = float.MaxValue;

        foreach (DollhouseRoomSlot slot in FindObjectsByType<DollhouseRoomSlot>(FindObjectsSortMode.None))
        {
            if (slot == null || !slot.Matches(room) || !slot.IsAvailableFor(room))
                continue;

            float distance = Vector3.Distance(position, slot.SnapTransform.position);
            if (distance > slot.snapRadius || distance >= bestDistance)
                continue;

            best = slot;
            bestDistance = distance;
        }

        return best;
    }

    public static void SetHeldModule(DollhouseRoom room)
    {
        heldModule = room;
        foreach (DollhouseRoomSlot slot in FindObjectsByType<DollhouseRoomSlot>(FindObjectsSortMode.None))
            slot.RefreshMarker();
    }

    public static void ClearHeldModule(DollhouseRoom room)
    {
        if (heldModule == room)
            heldModule = null;

        foreach (DollhouseRoomSlot slot in FindObjectsByType<DollhouseRoomSlot>(FindObjectsSortMode.None))
            slot.RefreshMarker();
    }

    public static void ReleaseOccupiedSlot(DollhouseRoom room)
    {
        foreach (DollhouseRoomSlot slot in FindObjectsByType<DollhouseRoomSlot>(FindObjectsSortMode.None))
        {
            if (slot != null && slot.occupyingModule == room)
                slot.Release(room);
        }
    }

    public void RefreshMarker()
    {
        if (markerRenderer == null)
            return;

        markerRenderer.enabled = !occupied;
        if (occupied)
            return;

        Color color = idleColor;
        if (heldModule != null && Matches(heldModule))
        {
            color = IsWithinSnapRadius(heldModule.ModuleSnapAnchorPosition)
                ? validColor
                : grabbedColor;
        }

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", color);
        markerRenderer.SetPropertyBlock(propertyBlock);
    }
}
