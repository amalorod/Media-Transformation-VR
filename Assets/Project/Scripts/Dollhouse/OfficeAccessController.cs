using UnityEngine;

/// <summary>
/// Hält den realen Tür-Blocker zum Büro geschlossen, solange das Office-Modul nicht auf dem Grid liegt.
/// Sobald das Modul platziert ist, wird der Blocker deaktiviert.
/// Der Blocker-Zustand folgt ausschließlich DollhouseRoom.IsPlacedOnGrid.
/// </summary>
public class OfficeAccessController : MonoBehaviour
{
    [SerializeField] private DollhouseRoom officeModule;
    [SerializeField] private GameObject doorBlocker;

    private void Update()
    {
        if (officeModule == null || doorBlocker == null)
            return;

        bool shouldBlock = !officeModule.IsPlacedOnGrid;
        if (doorBlocker.activeSelf != shouldBlock)
            doorBlocker.SetActive(shouldBlock);
    }
}
