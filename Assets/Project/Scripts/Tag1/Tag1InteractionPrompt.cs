using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liefert die Aktionszeile für einen Hinweis, z. B. "Open · Y", "Locked" oder "Grab · Grip".
/// </summary>
public interface ITag1ActionSource
{
    string GetActionLine();
}

/// <summary>
/// Kopfgebundener Aktionshinweis unten mittig im Sichtfeld. Standardmäßig unsichtbar (CanvasGroup alpha 0).
/// Die Canvas-Komponente wird im Build ergänzt. Es gibt keinen GraphicRaycaster, daher blockiert er keine XR-Rays.
/// </summary>
public class Tag1InteractionPrompt : MonoBehaviour
{
    public static Tag1InteractionPrompt Instance { get; private set; }

    private CanvasGroup group;
    private TMP_Text titleText;
    private TMP_Text actionText;
    private string lastTitle = "";
    private string lastAction = "";

    private void Awake()
    {
        Instance = this;
        Build();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string title, string action)
    {
        if (title != lastTitle)
        {
            lastTitle = title;
            titleText.text = title;
        }
        if (action != lastAction)
        {
            lastAction = action;
            actionText.text = action;
        }
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }

    private void Build()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(500f, 120f);

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;
        var bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        titleText = CreateText("Title", new Vector2(0f, 28f), 40f, Color.white);
        actionText = CreateText("Action", new Vector2(0f, -26f), 34f, new Color(1f, 0.85f, 0.3f));
    }

    private TMP_Text CreateText(string name, Vector2 position, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        var r = text.rectTransform;
        r.sizeDelta = new Vector2(480f, 50f);
        r.anchoredPosition = position;
        return text;
    }
}
