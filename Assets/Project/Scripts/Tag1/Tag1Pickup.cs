using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Aufnehmbares Story-Objekt für Tag 1. Beim Greifen wird die pickupId an den Tag1FlowController gemeldet.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Tag1Pickup : MonoBehaviour
{
    [SerializeField] private Tag1FlowController flow;
    [SerializeField] private string pickupId;

    private XRGrabInteractable grab;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnPicked);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnPicked);
    }

    private void OnPicked(SelectEnterEventArgs args)
    {
        if (flow != null)
            flow.Report(pickupId);
    }
}
