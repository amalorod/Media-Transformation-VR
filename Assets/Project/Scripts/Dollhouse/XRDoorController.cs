using System.Collections;
using UnityEngine;

/// <summary>
/// Generische Basis-Tür. Dreht den doorPivot um seine lokale Y-Achse (weich, ca. 0,4 s).
/// Solange lockBlocker aktiv ist, ist die Tür gesperrt. Keine Physik, keine Automatik.
/// </summary>
public class XRDoorController : MonoBehaviour
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float duration = 0.4f;
    [Tooltip("Solange dieses Objekt aktiv ist, ist die Tür gesperrt (z. B. DoorBlocker).")]
    [SerializeField] private GameObject lockBlocker;

    private Quaternion closedRotation;
    private bool isOpen;
    private bool isAnimating;

    public bool IsOpen => isOpen;
    public bool IsAnimating => isAnimating;
    public bool IsLocked => lockBlocker != null && lockBlocker.activeInHierarchy;

    private void Awake()
    {
        if (doorPivot == null) doorPivot = transform;
        closedRotation = doorPivot.localRotation;
    }

    public string GetStateLine()
    {
        if (IsLocked) return "Locked";
        return isOpen ? "Close · Y" : "Open · Y";
    }

    /// <summary>Öffnet oder schließt die Tür. Gibt false zurück, wenn gesperrt oder gerade in Bewegung.</summary>
    public bool TryToggle()
    {
        if (isAnimating || IsLocked) return false;
        StartCoroutine(Animate(!isOpen));
        return true;
    }

    private IEnumerator Animate(bool open)
    {
        isAnimating = true;
        Quaternion from = doorPivot.localRotation;
        Quaternion to = open ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            doorPivot.localRotation = Quaternion.Slerp(from, to, k);
            yield return null;
        }

        doorPivot.localRotation = to;
        isOpen = open;
        isAnimating = false;
    }
}
