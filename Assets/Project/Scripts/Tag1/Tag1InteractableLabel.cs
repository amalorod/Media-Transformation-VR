using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Kleines World-Space-Label für ein Interaktionsobjekt. Es ist standardmäßig verborgen und wird bei Hover
/// sichtbar. Select blendet es aus. Das Label blockiert keine Raycasts und schaut zur Kamera.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class Tag1InteractableLabel : MonoBehaviour
{
    [SerializeField] private string labelText = "";
    [SerializeField] private float heightOffset = 0.2f;

    private XRBaseInteractable interactable;
    private Transform labelRoot;
    private Renderer[] targetRenderers;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        targetRenderers = GetComponentsInChildren<Renderer>();
        BuildLabel();
    }

    private void OnEnable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnSelected);
        SetLabelVisible(false);
    }

    private void OnDestroy()
    {
        if (labelRoot != null) Destroy(labelRoot.gameObject);
    }

    private void LateUpdate()
    {
        if (labelRoot == null || !labelRoot.gameObject.activeSelf)
            return;

        Vector3 top = GetTopPosition();
        labelRoot.position = top + Vector3.up * heightOffset;

        Camera cam = Camera.main;
        if (cam != null)
            labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - cam.transform.position);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => SetLabelVisible(true);

    private void OnHoverExited(HoverExitEventArgs args) => SetLabelVisible(false);

    private void OnSelected(SelectEnterEventArgs args) => SetLabelVisible(false);

    private void SetLabelVisible(bool visible)
    {
        if (labelRoot != null && labelRoot.gameObject.activeSelf != visible)
            labelRoot.gameObject.SetActive(visible);
    }

    private void BuildLabel()
    {
        var root = new GameObject("Label_" + gameObject.name);
        labelRoot = root.transform;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400f, 70f);
        rect.localScale = Vector3.one * 0.0012f;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = string.IsNullOrEmpty(labelText) ? gameObject.name : labelText;
        text.fontSize = 42f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.sizeDelta = new Vector2(400f, 70f);

        root.SetActive(false);
    }

    private Vector3 GetTopPosition()
    {
        float top = float.MinValue;
        bool found = false;
        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;
            top = found ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            found = true;
        }

        Vector3 p = transform.position;
        if (found) p.y = top;
        return p;
    }
}
