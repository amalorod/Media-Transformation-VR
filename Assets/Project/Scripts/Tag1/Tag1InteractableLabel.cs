using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Gemeinsame Beschriftung für Interaktions- und Storyobjekte.
/// Das Namenslabel über dem Objekt ist nur im Informationsmodus (rechter Secondary Button) sichtbar.
/// Der kopfgebundene Aktionshinweis erscheint unabhängig davon beim Hover.
/// Das Label hat keinen GraphicRaycaster und blockiert keine XR-Rays.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class Tag1InteractableLabel : MonoBehaviour
{
    [SerializeField] private string labelText = "";
    [SerializeField] private string actionText = "Grab · Grip";
    [SerializeField] private float heightOffset = 0.15f;

    private static readonly List<Tag1InteractableLabel> Registry = new List<Tag1InteractableLabel>();

    private XRBaseInteractable interactable;
    private Transform labelRoot;
    private TMP_Text labelTmp;
    private Renderer[] targetRenderers;
    private bool infoVisible;

    private string DisplayName => string.IsNullOrEmpty(labelText) ? gameObject.name : labelText;

    /// <summary>Blendet alle registrierten Labels ein oder aus. Wird vom Tag1InformationMode gesteuert.</summary>
    public static void SetAllLabelsVisible(bool visible)
    {
        for (int i = Registry.Count - 1; i >= 0; i--)
        {
            Tag1InteractableLabel label = Registry[i];
            if (label == null)
            {
                Registry.RemoveAt(i);
                continue;
            }

            label.infoVisible = visible;
            label.ApplyVisibility();
        }
    }

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        targetRenderers = GetComponentsInChildren<Renderer>(true);
        BuildLabel();
    }

    private void OnEnable()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);

        if (interactable == null)
            return;

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        Registry.Remove(this);

        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        infoVisible = false;
        SetLabelVisible(false);
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (labelRoot != null)
            Destroy(labelRoot.gameObject);
    }

    private void LateUpdate()
    {
        if (labelRoot == null || !labelRoot.gameObject.activeSelf)
            return;

        labelRoot.position = GetTopPosition() + Vector3.up * heightOffset;

        Camera cam = Camera.main;
        if (cam != null)
            labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - cam.transform.position);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => RefreshPrompt();

    private void OnHoverExited(HoverExitEventArgs args) => HidePrompt();

    private void OnSelectEntered(SelectEnterEventArgs args) => HidePrompt();

    /// <summary>Ein Objekt wird nur beschriftet, wenn es im Informationsmodus aktiv und sichtbar ist.</summary>
    private void ApplyVisibility()
    {
        SetLabelVisible(infoVisible && isActiveAndEnabled && gameObject.activeInHierarchy);
    }

    private string GetActionLine()
    {
        var source = GetComponent<ITag1ActionSource>();
        return source != null ? source.GetActionLine() : actionText;
    }

    private void RefreshPrompt()
    {
        if (Tag1InteractionPrompt.Instance != null)
            Tag1InteractionPrompt.Instance.Show(DisplayName, GetActionLine());
    }

    private void HidePrompt()
    {
        if (Tag1InteractionPrompt.Instance != null)
            Tag1InteractionPrompt.Instance.Hide();
    }

    private void SetLabelVisible(bool visible)
    {
        if (labelRoot != null && labelRoot.gameObject.activeSelf != visible)
            labelRoot.gameObject.SetActive(visible);
    }

    private void BuildLabel()
    {
        var root = new GameObject("Label_" + gameObject.name, typeof(RectTransform));
        labelRoot = root.transform;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(400f, 70f);
        rect.localScale = Vector3.one * 0.0012f;

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;
        var bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        labelTmp = textGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = DisplayName;
        labelTmp.fontSize = 42f;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.white;
        labelTmp.outlineWidth = 0.25f;
        labelTmp.outlineColor = Color.black;
        labelTmp.raycastTarget = false;
        var textRect = labelTmp.rectTransform;
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
