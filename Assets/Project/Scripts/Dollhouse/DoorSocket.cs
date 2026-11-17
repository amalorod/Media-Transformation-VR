using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Markiert eine Türöffnung an einem Puppenhaus-Raum.
/// Überprüft die räumliche Ausrichtung und Nähe zu anderen Türöffnungen.
/// </summary>
public class DoorSocket : MonoBehaviour
{
    [Header("Verbindungseinstellungen")]
    [Tooltip("Maximale Distanz in Metern, um als verbunden zu gelten")]
    [SerializeField] private float connectionDistance = 0.035f;

    [Tooltip("Maximal erlaubte Winkelabweichung der gegenüberliegenden Türen")]
    [Range(0.5f, 1f)]
    [SerializeField] private float facingDotThreshold = 0.75f;

    [Header("Events")]
    public UnityEvent onConnected;
    public UnityEvent onDisconnected;

    public bool IsConnected { get; private set; }

    /// <summary>
    /// Prüft, ob ein anderer Socket nahe genug ist UND ob die Türen sich gegenüberstehen
    /// (Vorwärtsvektoren müssen aufeinander zeigen: Dot-Produkt nahe -1).
    /// </summary>
    public bool IsNearAndFacing(DoorSocket other)
    {
        if (other == null || other == this)
            return false;

        float distance = Vector3.Distance(transform.position, other.transform.position);
        if (distance > connectionDistance)
            return false;

        // Türen müssen einander zugewandt sein (transform.forward zeigt nach außen aus der Tür)
        float dot = Vector3.Dot(transform.forward, other.transform.forward);
        return dot <= -facingDotThreshold;
    }

    public void SetConnected(bool connected)
    {
        if (IsConnected == connected) return;

        IsConnected = connected;

        if (connected)
        {
            onConnected?.Invoke();
            Debug.Log($"[DoorSocket] VERBUNDEN: {name} mit gegenüberliegender Tür!");
        }
        else
        {
            onDisconnected?.Invoke();
        }
    }

    private void OnDrawGizmos()
    {
        // Kugel am Socket
        Gizmos.color = IsConnected ? Color.green : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.008f);

        // Ausrichtungs-Pfeil nach außen
        Gizmos.color = IsConnected ? Color.green : Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 0.03f);
    }
}