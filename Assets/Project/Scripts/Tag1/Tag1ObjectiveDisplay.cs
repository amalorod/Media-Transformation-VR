using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Reine Anzeige für das VR-Objective-Canvas. Der Tag1FlowController bestimmt Text und Sichtbarkeit.
/// Sichtbar ist das Objective nur im Informationsmodus (rechter Secondary Button gehalten),
/// damit es im normalen Spielen nicht stört.
/// Das Canvas-GameObject bleibt dauerhaft aktiv, damit dieses Skript weiterlaufen kann; umgeschaltet
/// werden die Canvas-Komponente und die sichtbaren Kindobjekte.
/// </summary>
public class Tag1ObjectiveDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;

    private Canvas canvas;
    private readonly List<GameObject> visualChildren = new List<GameObject>();

    private string lastHeader = "";
    private string lastBody = "";
    private bool requestedVisible;
    private bool appliedVisible;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        foreach (Transform child in transform)
            visualChildren.Add(child.gameObject);

        SetApplied(false);
    }

    public void SetText(string header, string body)
    {
        if (header == lastHeader && body == lastBody)
            return;

        lastHeader = header;
        lastBody = body;

        if (headerText != null) headerText.text = header;
        if (bodyText != null) bodyText.text = body;
    }

    /// <summary>Merkt sich die gewünschte Sichtbarkeit. Die tatsächliche Anzeige steuert der Informationsmodus.</summary>
    public void SetVisible(bool visible)
    {
        requestedVisible = visible;
    }

    private void LateUpdate()
    {
        SetApplied(requestedVisible && Tag1InformationMode.Active);
    }

    private void SetApplied(bool visible)
    {
        if (appliedVisible == visible && canvas != null && canvas.enabled == visible)
            return;

        appliedVisible = visible;

        if (canvas != null)
            canvas.enabled = visible;

        foreach (GameObject child in visualChildren)
        {
            if (child != null && child.activeSelf != visible)
                child.SetActive(visible);
        }
    }
}
