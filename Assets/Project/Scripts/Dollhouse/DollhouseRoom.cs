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
    [SerializeField] private float worldScaleFactor = 20f;

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
    }



    private void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (!TryMagneticSnap())
        {
            TryPlaceOnGrid();
        }

        UpdateWorldRoom();
    }

    private bool TryMagneticSnap()
    {
        DollhouseRoom[] rooms = FindObjectsByType<DollhouseRoom>(
            FindObjectsSortMode.None
        );

        DollhouseRoom nearestRoom = null;
        float nearestDistance = float.MaxValue;

        foreach (DollhouseRoom other in rooms)
        {
            if (other == this)
                continue;

            Collider otherCollider = other.GetComponent<Collider>();

            if (otherCollider == null)
                continue;

            Vector3 closestPoint =
                otherCollider.ClosestPoint(transform.position);

            float distance = Vector3.Distance(
                transform.position,
                closestPoint
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestRoom = other;
            }
        }

        if (nearestRoom != null && nearestDistance <= snapDistance)
        {
            SnapToRoom(nearestRoom);
            return true;
        }

        return false;
    }

    private void SnapToRoom(DollhouseRoom target)
    {
        Collider targetCollider = target.GetComponent<Collider>();

        if (roomCollider == null || targetCollider == null)
            return;

        Bounds myBounds = roomCollider.bounds;
        Bounds targetBounds = targetCollider.bounds;

        Vector3 direction =
            transform.position - target.transform.position;

        Vector3 newPosition = transform.position;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            if (direction.x > 0f)
            {
                newPosition.x =
                    targetBounds.max.x +
                    myBounds.extents.x;
            }
            else
            {
                newPosition.x =
                    targetBounds.min.x -
                    myBounds.extents.x;
            }
        }
        else
        {
            if (direction.z > 0f)
            {
                newPosition.z =
                    targetBounds.max.z +
                    myBounds.extents.z;
            }
            else
            {
                newPosition.z =
                    targetBounds.min.z -
                    myBounds.extents.z;
            }
        }

        transform.position = newPosition;
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
        if (worldRoom == null || grid == null)
            return;

        Vector3 localMiniPosition =
            grid.transform.InverseTransformPoint(transform.position);

        Vector3 worldPosition = new Vector3(
            localMiniPosition.x * worldScaleFactor,
            0f,
            localMiniPosition.z * worldScaleFactor
        );

        worldRoom.localPosition = worldPosition;

        // Rotation der Miniatur übernehmen
        Vector3 miniRotation = transform.localEulerAngles;

        worldRoom.localRotation = Quaternion.Euler(
            0f,
            miniRotation.y,
            0f
        );
    }

}
