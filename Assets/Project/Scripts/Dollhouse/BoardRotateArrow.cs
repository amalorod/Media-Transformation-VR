using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Einfacher Pfeil zum Drehen des Puppenhaus-Boards. Hover färbt den Pfeil gelb,
/// Select löst die 45-Grad-Drehung nach links oder rechts aus.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class BoardRotateArrow : MonoBehaviour
{
    [SerializeField] private BoardRotationController controller;
    [SerializeField] private bool isLeft;

    [Tooltip("Renderer, die beim Hover eingefärbt werden. Leer = alle Kind-Renderer.")]
    [SerializeField] private Renderer[] highlightRenderers;

    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color hoverColor = Color.yellow;

    private XRSimpleInteractable interactable;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        block = new MaterialPropertyBlock();

        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();

        SetColor(normalColor);
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => SetColor(hoverColor);

    private void OnHoverExited(HoverExitEventArgs args) => SetColor(normalColor);

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (controller == null)
            return;

        if (isLeft)
            controller.RotateLeft();
        else
            controller.RotateRight();
    }

    private void SetColor(Color color)
    {
        foreach (Renderer rend in highlightRenderers)
        {
            if (rend == null)
                continue;

            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            rend.SetPropertyBlock(block);
        }
    }
}
