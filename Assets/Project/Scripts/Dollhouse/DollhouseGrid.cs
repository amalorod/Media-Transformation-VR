using UnityEngine;

public class DollhouseGrid : MonoBehaviour
{
    [Header("Grid Einstellungen")]
    public float cellSize = 0.1f;
    public int gridWidth = 6;
    public int gridDepth = 6;

    private DollhouseRoom[,] occupiedCells;

    private void Awake()
    {
        occupiedCells = new DollhouseRoom[gridWidth, gridDepth];
    }

    public Vector3 GetNearestGridPosition(Vector3 worldPosition)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);

        int x = Mathf.RoundToInt(localPos.x / cellSize);
        int z = Mathf.RoundToInt(localPos.z / cellSize);

        x = Mathf.Clamp(x, 0, gridWidth - 1);
        z = Mathf.Clamp(z, 0, gridDepth - 1);

        Vector3 snappedLocalPos = new Vector3(
            x * cellSize,
            0f,
            z * cellSize
        );

        return transform.TransformPoint(snappedLocalPos);
    }

    public bool CanPlaceRoom(
        DollhouseRoom room,
        int startX,
        int startZ)
    {
        for (int x = startX; x < startX + room.Width; x++)
        {
            for (int z = startZ; z < startZ + room.Depth; z++)
            {
                // Außerhalb des Boards
                if (x < 0 || x >= gridWidth ||
                    z < 0 || z >= gridDepth)
                {
                    return false;
                }

                // Zelle bereits von anderem Raum belegt
                if (occupiedCells[x, z] != null &&
                    occupiedCells[x, z] != room)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void OccupyRoom(
        DollhouseRoom room,
        int startX,
        int startZ)
    {
        for (int x = startX; x < startX + room.Width; x++)
        {
            for (int z = startZ; z < startZ + room.Depth; z++)
            {
                occupiedCells[x, z] = room;
            }
        }
    }

    public void ClearRoom(DollhouseRoom room)
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (occupiedCells[x, z] == room)
                {
                    occupiedCells[x, z] = null;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                Vector3 cellCenter = transform.TransformPoint(
                    new Vector3(
                        x * cellSize,
                        0.005f,
                        z * cellSize
                    )
                );

                Gizmos.DrawWireCube(
                    cellCenter,
                    new Vector3(
                        cellSize * 0.95f,
                        0.001f,
                        cellSize * 0.95f
                    )
                );
            }
        }
    }
}