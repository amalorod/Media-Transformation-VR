using UnityEngine;

/// <summary>
/// Verwaltet das Puppenhaus-Raster (Grid).
/// Behebt Überlappungen, unterstützt 90°-Rotations-Footprints
/// und ermöglicht das freie Ablegen von Räumen außerhalb des Grids.
/// </summary>
public class DollhouseGrid : MonoBehaviour
{
    [Header("Grid Einstellungen")]
    [Tooltip("Größe einer Rasterzelle in Metern (z. B. 0.1m = 10cm)")]
    public float cellSize = 0.1f;
    [Tooltip("Anzahl der Zellen in X-Richtung")]
    public int gridWidth = 6;
    [Tooltip("Anzahl der Zellen in Z-Richtung")]
    public int gridDepth = 6;

    [Header("Tisch / Ablagefläche (Außerhalb des Grids)")]
    [Tooltip("Höhe der Ablagefläche relativ zum Grid-Pivot")]
    public float surfaceHeightOffset = 0.0f;
    [Tooltip("Maximaler Radius um das Grid, in dem Räume auf dem Tisch abgelegt werden dürfen")]
    public float tableRadius = 1.2f;

    // 2D-Array zur Speicherung der Raum-Referenzen pro Zelle
    private DollhouseRoom[,] occupiedCells;

    private void Awake()
    {
        occupiedCells = new DollhouseRoom[gridWidth, gridDepth];
    }

    /// <summary>
    /// Berechnet die effektive Breite und Tiefe eines Raumes basierend auf der 90°-Y-Rotation.
    /// Bei 90° und 270° tauschen Breite und Tiefe!
    /// </summary>
    public void GetEffectiveDimensions(int width, int depth, float yRotation, out int effWidth, out int effDepth)
    {
        int snapAngle = Mathf.RoundToInt(yRotation / 90f) * 90;
        snapAngle = ((snapAngle % 360) + 360) % 360;

        if (snapAngle == 90 || snapAngle == 270)
        {
            effWidth = depth;
            effDepth = width;
        }
        else
        {
            effWidth = width;
            effDepth = depth;
        }
    }

    /// <summary>
    /// Prüft, ob sich eine Weltkoordinate innerhalb der physischen Grenzen des Grids befindet.
    /// Ermöglicht das Ablegen von Räumen AUßERHALB des Boards auf dem Tisch!
    /// </summary>
    public bool IsWithinGridBounds(Vector3 worldPosition, float tolerancePadding = 0.02f)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float totalWidth = gridWidth * cellSize;
        float totalDepth = gridDepth * cellSize;

        return (local.x >= -tolerancePadding && local.x <= totalWidth + tolerancePadding &&
                local.z >= -tolerancePadding && local.z <= totalDepth + tolerancePadding);
    }

    /// <summary>
    /// Ermittelt die nächstgelegenen Rasterkoordinaten (Start-X, Start-Z) für einen Raum.
    /// </summary>
    public bool TryGetGridCoordinates(Vector3 worldPosition, int width, int depth, float yRotation, out int startX, out int startZ)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);

        GetEffectiveDimensions(width, depth, yRotation, out int effW, out int effD);

        // Das Zentrum des Raums auf die linke untere Ecke der belegten Zellen zurückrechnen
        float originX = localPos.x - (effW * cellSize * 0.5f);
        float originZ = localPos.z - (effD * cellSize * 0.5f);

        startX = Mathf.RoundToInt(originX / cellSize);
        startZ = Mathf.RoundToInt(originZ / cellSize);

        // Prüfen, ob der Raum innerhalb der Zellumrandung liegt
        return (startX >= 0 && startX + effW <= gridWidth &&
                startZ >= 0 && startZ + effD <= gridDepth);
    }

    /// <summary>
    /// Berechnet die exakte Weltposition für einen platzierten Raum.
    /// Der Raum sitzt immer exakt plan auf der Ebene (Y = 0f bzw. surfaceHeightOffset).
    /// </summary>
    public Vector3 GetWorldPositionForCell(int startX, int startZ, int width, int depth, float yRotation)
    {
        GetEffectiveDimensions(width, depth, yRotation, out int effW, out int effD);

        // Zentrum des Raumes berechnen
        float centerX = (startX + (effW * 0.5f)) * cellSize;
        float centerZ = (startZ + (effD * 0.5f)) * cellSize;

        Vector3 localCenter = new Vector3(centerX, surfaceHeightOffset, centerZ);
        return transform.TransformPoint(localCenter);
    }

    /// <summary>
    /// Prüft, ob alle benötigten Zellen frei sind (unter Beachtung der Rotation).
    /// </summary>
    public bool CanPlaceRoom(DollhouseRoom room, int startX, int startZ, float yRotation)
    {
        GetEffectiveDimensions(room.Width, room.Depth, yRotation, out int effW, out int effD);

        // Außerhalb des Boards?
        if (startX < 0 || startX + effW > gridWidth ||
            startZ < 0 || startZ + effD > gridDepth)
        {
            return false;
        }

        // Kollision mit bereits belegten Zellen?
        for (int x = startX; x < startX + effW; x++)
        {
            for (int z = startZ; z < startZ + effD; z++)
            {
                if (occupiedCells[x, z] != null && occupiedCells[x, z] != room)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Belegt die Zellen im Raster mit diesem Raum.
    /// </summary>
    public void OccupyRoom(DollhouseRoom room, int startX, int startZ, float yRotation)
    {
        // Vorherige Zellen des Raums immer zuerst bereinigen
        ClearRoom(room);

        GetEffectiveDimensions(room.Width, room.Depth, yRotation, out int effW, out int effD);

        for (int x = startX; x < startX + effW; x++)
        {
            for (int z = startZ; z < startZ + effD; z++)
            {
                if (x >= 0 && x < gridWidth && z >= 0 && z < gridDepth)
                {
                    occupiedCells[x, z] = room;
                }
            }
        }
    }

    /// <summary>
    /// Gibt alle vom Raum belegten Zellen wieder frei (z.B. beim Hochheben oder Ablegen auf dem Tisch).
    /// </summary>
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

    /// <summary>
    /// Sucht die nächste freie Nachbarposition, falls der direkte Slot belegt ist.
    /// </summary>
    public bool FindNearestValidPosition(
        DollhouseRoom room,
        int requestedX,
        int requestedZ,
        float yRotation,
        out int validX,
        out int validZ)
    {
        float bestDistance = float.MaxValue;
        validX = -1;
        validZ = -1;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (!CanPlaceRoom(room, x, z, yRotation))
                    continue;

                float distance = Vector2.SqrMagnitude(new Vector2(x - requestedX, z - requestedZ));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    validX = x;
                    validZ = z;
                }
            }
        }

        return validX >= 0 && validZ >= 0;
    }

    private void OnDrawGizmos()
    {
        // 1. Grid-Zellen zeichnen
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                Vector3 cellCenter = transform.TransformPoint(
                    new Vector3((x + 0.5f) * cellSize, surfaceHeightOffset + 0.001f, (z + 0.5f) * cellSize)
                );

                bool isOccupied = Application.isPlaying && occupiedCells != null && occupiedCells[x, z] != null;
                Gizmos.color = isOccupied ? new Color(1f, 0.4f, 0.2f, 0.7f) : new Color(0f, 0.8f, 1f, 0.4f);

                Gizmos.DrawWireCube(
                    cellCenter,
                    new Vector3(cellSize * 0.96f, 0.001f, cellSize * 0.96f)
                );
            }
        }

        // 2. Äußere Umrandung des Grids
        Gizmos.color = Color.white;
        Vector3 gridCenter = transform.TransformPoint(
            new Vector3(gridWidth * cellSize * 0.5f, surfaceHeightOffset, gridDepth * cellSize * 0.5f)
        );
        Gizmos.DrawWireCube(gridCenter, new Vector3(gridWidth * cellSize, 0.002f, gridDepth * cellSize));
    }
}