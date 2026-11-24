using UnityEngine;

/// <summary>
/// Steuert Paket 1 von Tag 1 in fester Storyboard-Reihenfolge.
/// Schritte werden aus bestehenden Modulzuständen gelesen. Ein Overlay zeigt aktuelles und nächstes Ziel
/// und kann über overlayVisible abgeschaltet werden.
/// </summary>
public class Tag1FlowController : MonoBehaviour
{
    [Header("Story-Objekte")]
    [SerializeField] private DollhouseRoom officeModule;
    [SerializeField] private DollhouseRoom childrenModule;
    [SerializeField] private Transform torvaldPuppet;
    [SerializeField] private Transform[] shoppingBags;
    [SerializeField] private Transform bagDropZone;
    [SerializeField] private float bagDropRadius = 1f;
    [SerializeField] private float puppetDistance = 0.3f;

    [Header("Anzeige")]
    [SerializeField] private bool overlayVisible = true;
    [SerializeField] private float confirmDuration = 2.5f;
    [SerializeField] private float prologDuration = 3f;

    private static readonly string[] Titles =
    {
        "Prolog: Im Flur beginnen",
        "Tüten ins Wohnzimmer tragen",
        "Büro-Modul auf dem Mini-Haus einsetzen",
        "Torvald-Puppe ins Büro-Modul stellen",
        "Makronen: Entscheidung treffen (Essen / Verbrennen / Verstecken)",
        "Kinderzimmer-Modul einsetzen",
        "Paket 1 abgeschlossen (Zwischenstand)"
    };

    private int stepIndex;
    private float stepTimer;
    private string confirmText = "";
    private float confirmTimer;
    private string makronenChoice = "";

    public int StepIndex => stepIndex;
    public string MakronenChoice => makronenChoice;
    public bool OverlayVisible
    {
        get => overlayVisible;
        set => overlayVisible = value;
    }

    private void Update()
    {
        if (confirmTimer > 0f)
            confirmTimer -= Time.deltaTime;

        if (stepIndex >= Titles.Length - 1)
            return;

        switch (stepIndex)
        {
            case 0:
                stepTimer += Time.deltaTime;
                if (stepTimer >= prologDuration)
                    Complete("Prolog gelesen. Die Tüten warten im Flur.");
                break;

            case 1:
                if (AllBagsInDropZone())
                    Complete("Tüten im Wohnzimmer abgestellt.");
                break;

            case 2:
                if (officeModule != null && officeModule.IsPlacedOnGrid)
                    Complete("Büro-Modul auf dem Mini-Haus eingesetzt.");
                break;

            case 3:
                if (PuppetInOffice())
                    Complete("Torvald im Büro. Seine Stimme aus dem Büro: \"Is my little lark chirping out there?\"");
                break;

            case 4:
                // Wird über Choose() abgeschlossen.
                break;

            case 5:
                if (childrenModule != null && childrenModule.IsPlacedOnGrid)
                    Complete("Kinderzimmer-Modul eingesetzt.");
                break;
        }
    }

    /// <summary>
    /// Wird von den Makronen-Auswahlpunkten aufgerufen. Wirkt nur im Makronen-Schritt.
    /// </summary>
    public void Choose(string choiceId)
    {
        if (stepIndex != 4)
            return;

        makronenChoice = choiceId;
        string storyLine = choiceId switch
        {
            "Essen" => "Krümel bleiben liegen.",
            "Verbrennen" => "Nora wird \"besser\". Der Ofen nimmt die Makronen.",
            "Verstecken" => "Die Makronen sind versteckt.",
            _ => "Entscheidung getroffen."
        };
        Complete("Makronen: " + choiceId + ". " + storyLine);
    }

    private void Complete(string text)
    {
        Debug.Log("[Tag1] " + text);
        confirmText = text;
        confirmTimer = confirmDuration;
        stepIndex++;
        stepTimer = 0f;
    }

    private bool AllBagsInDropZone()
    {
        if (bagDropZone == null || shoppingBags == null || shoppingBags.Length == 0)
            return false;

        foreach (Transform bag in shoppingBags)
        {
            if (bag == null)
                return false;

            Vector3 d = bag.position - bagDropZone.position;
            d.y = 0f;
            if (d.magnitude > bagDropRadius)
                return false;
        }

        return true;
    }

    private bool PuppetInOffice()
    {
        if (torvaldPuppet == null || officeModule == null || !officeModule.IsPlacedOnGrid)
            return false;

        return Vector3.Distance(torvaldPuppet.position, officeModule.transform.position) <= puppetDistance;
    }

    private void OnGUI()
    {
        if (!overlayVisible)
            return;

        string current = Titles[stepIndex];
        string next = stepIndex < Titles.Length - 1 ? Titles[stepIndex + 1] : "-";

        GUILayout.BeginArea(new Rect(10f, 10f, 760f, 140f), GUI.skin.box);
        GUILayout.Label("Aktuelles Ziel: " + current);
        if (confirmTimer > 0f && confirmText.Length > 0)
            GUILayout.Label("✓ " + confirmText);
        GUILayout.Label("Nächstes Ziel: " + next);
        GUILayout.EndArea();
    }
}
