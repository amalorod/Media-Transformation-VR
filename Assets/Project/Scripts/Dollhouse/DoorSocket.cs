using UnityEngine;

public class DoorSocket : MonoBehaviour
{
    [SerializeField] private float connectionDistance = 0.03f;

    public bool IsConnected { get; private set; }

    public bool IsNear(DoorSocket other)
    {
        if (other == null || other == this)
            return false;

        float distance = Vector3.Distance(
            transform.position,
            other.transform.position
        );

        return distance <= connectionDistance;
    }

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsConnected
            ? Color.green
            : Color.yellow;

        Gizmos.DrawSphere(transform.position, 0.008f);
    }
}