using UnityEngine;

public class Tag1_BagDropZone : MonoBehaviour
{
    [SerializeField] private Transform shoppingBagA;
    [SerializeField] private Transform shoppingBagB;

    private bool bagAInZone;
    private bool bagBInZone;

    public bool AreAllBagsInZone => bagAInZone && bagBInZone;

    private void OnTriggerEnter(Collider other)
    {
        UpdateBagStatus(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        UpdateBagStatus(other, false);
    }

    private void OnTriggerStay(Collider other)
    {
        // Ensure bags are recognized even if they were already inside or if trigger events were missed
        UpdateBagStatus(other, true);
    }

    private void UpdateBagStatus(Collider other, bool inZone)
    {
        // Resolve to root object via attachedRigidbody or parent
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        
        // If not found yet, try GetComponentInParent as fallback for complex hierarchies
        if (root == other.transform && other.GetComponentInParent<Rigidbody>())
        {
            root = other.GetComponentInParent<Rigidbody>().transform;
        }

        if (root == shoppingBagA)
        {
            bagAInZone = inZone;
        }
        else if (root == shoppingBagB)
        {
            bagBInZone = inZone;
        }
    }

    // Public method to manually assign bags if needed, or for the controller to initialize
    public void Setup(Transform bagA, Transform bagB)
    {
        shoppingBagA = bagA;
        shoppingBagB = bagB;
    }
}
