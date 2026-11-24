using TMPro;
using UnityEngine;

/// <summary>
/// Reine Anzeige für das VR-Objective-Canvas. Der Tag1FlowController bestimmt Text und Sichtbarkeit.
/// </summary>
public class Tag1ObjectiveDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;

    private string lastHeader = "";
    private string lastBody = "";

    public void SetText(string header, string body)
    {
        if (header == lastHeader && body == lastBody)
            return;

        lastHeader = header;
        lastBody = body;

        if (headerText != null) headerText.text = header;
        if (bodyText != null) bodyText.text = body;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}
