using UnityEngine;

/// <summary>
/// Definiert die Tischoberfläche um das Puppenhaus-Grid.
/// Bietet optionale Staging-Slots für Räume, die gerade nicht auf dem Board stehen.
/// </summary>
public class DollhouseStagingTable : MonoBehaviour
{
    [Header("Tischgrenzen")]
    public float tableWidth = 1.2f;
    public float tableDepth = 1.0f;
    public float surfaceY = 0.8f;

    [Header("Feste Ablage-Slots (Optional)")]
    public Transform[] shelfSlots;

    public Vector3 ClampToTableBounds(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);

        float halfW = tableWidth * 0.5f;
        float halfD = tableDepth * 0.5f;

        local.x = Mathf.Clamp(local.x, -halfW, halfW);
        local.z = Mathf.Clamp(local.z, -halfD, halfD);
        local.y = surfaceY;

        return transform.TransformPoint(local);
    }
}