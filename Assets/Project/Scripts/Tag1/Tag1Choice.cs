using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Einfacher Auswahlpunkt für Tag-1-Entscheidungen. Select übergibt die choiceId an den Tag1FlowController.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class Tag1Choice : MonoBehaviour
{
    [SerializeField] private Tag1FlowController flow;
    [SerializeField] private string choiceId;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (flow != null)
            flow.Report(choiceId);
    }
}
