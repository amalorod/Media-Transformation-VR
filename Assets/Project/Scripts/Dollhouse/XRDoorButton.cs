using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Türknopf mit XRSimpleInteractable. Hover aktiviert die Y-Action nur für diese Tür,
/// Hover-Ende deaktiviert sie wieder. Der Knopf wird beim Hover gelb markiert.
/// Die Y-Action ist eine eigene Code-Action auf dem linken secondaryButton, sie überschreibt keine vorhandenen Actions.
/// Es kann immer nur ein Türknopf gleichzeitig auf Y reagieren.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class XRDoorButton : MonoBehaviour, ITag1ActionSource
{
    [SerializeField] private XRDoorController door;
    [SerializeField] private Renderer highlightRenderer;

    private static XRDoorButton hoveredButton;
    private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f);

    private XRSimpleInteractable interactable;
    private InputAction toggleAction;
    private Color baseColor;
    private bool hasBaseColor;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        toggleAction = new InputAction("Tag1DoorToggle", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        toggleAction.performed += OnToggle;

        if (highlightRenderer == null) highlightRenderer = GetComponent<Renderer>();
        if (highlightRenderer != null)
        {
            baseColor = highlightRenderer.material.color;
            hasBaseColor = true;
        }
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        ClearHover();
    }

    private void OnDestroy()
    {
        if (toggleAction != null)
        {
            toggleAction.performed -= OnToggle;
            toggleAction.Dispose();
        }
    }

    public string GetActionLine()
    {
        return door != null ? door.GetStateLine() : "Locked";
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        // Nur ein Türknopf darf gleichzeitig auf Y hören.
        if (hoveredButton != null && hoveredButton != this)
            hoveredButton.ClearHover();

        hoveredButton = this;
        toggleAction.Enable();
        SetHighlight(true);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        ClearHover();
    }

    private void ClearHover()
    {
        if (hoveredButton == this)
            hoveredButton = null;

        toggleAction.Disable();
        SetHighlight(false);
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        if (door != null) door.TryToggle();
    }

    private void SetHighlight(bool on)
    {
        if (!hasBaseColor) return;
        highlightRenderer.material.color = on ? HighlightColor : baseColor;
    }
}
