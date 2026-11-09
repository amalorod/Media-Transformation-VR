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

    public int Width => IsRotated ? depth : width;
    public int Depth => IsRotated ? width : depth;

    public bool IsRotated { get; private set; }

    private Vector3 lastValidPosition;
    private bool hasValidPosition;

    private void Awake()
    {
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
        TryPlaceOnGrid();
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

    [ContextMenu("Rotate 90 Degrees")]
    public void Rotate90Degrees()
    {
        IsRotated = !IsRotated;
        transform.Rotate(0f, 90f, 0f);

        // Grid-Logik nur während des laufenden Spiels
        if (Application.isPlaying)
        {
            TryPlaceOnGrid();
        }
    }


}