using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Steuert Tag 1 in fester Storyboard-Reihenfolge (Paket 1 und Paket 2).
/// Schritte werden entweder aus Modulzuständen und Distanzen gelesen oder über Report(id)
/// von Interactables und Pickups gemeldet. Das Overlay zeigt aktuelles Ziel, kurze Bestätigung
/// und nächstes Ziel und lässt sich über OverlayVisible abschalten.
/// </summary>
public class Tag1FlowController : MonoBehaviour
{
    [Header("Paket 1")]
    [SerializeField] private DollhouseRoom officeModule;
    [SerializeField] private DollhouseRoom childrenModule;
    [SerializeField] private Transform torvaldPuppet;
    [SerializeField] private Transform[] shoppingBags;
    [SerializeField] private Transform bagDropZone;
    [SerializeField] private float bagDropRadius = 1f;
    [SerializeField] private float puppetDistance = 0.3f;
    [SerializeField] private float prologDuration = 3f;

    [Header("Paket 2")]
    [SerializeField] private DollhouseRoom kitchenModule;
    [SerializeField] private Transform bobPuppet;
    [SerializeField] private Transform lindePuppet;
    [SerializeField] private Transform roomAMini;
    [SerializeField] private float heistDuration = 30f;
    [SerializeField] private float krogstadDelay = 2f;
    [SerializeField] private GameObject figureBob;
    [SerializeField] private GameObject figureLinde;
    [SerializeField] private GameObject figureKrogstad;
    [SerializeField] private GameObject figureRank;
    [SerializeField] private GameObject figureAnneMarie;

    [Header("Anzeige")]
    [SerializeField] private bool overlayVisible = true;
    [SerializeField] private float confirmDuration = 2.5f;

    private static readonly string[] Titles =
    {
        "Prolog: Im Flur beginnen",                                              // 0
        "Tüten ins Wohnzimmer tragen",                                           // 1
        "Büro-Modul auf dem Mini-Haus einsetzen",                                // 2
        "Torvald-Puppe ins Büro-Modul stellen",                                  // 3
        "Makronen: Entscheidung treffen (Essen / Verbrennen / Verstecken)",      // 4
        "Kinderzimmer-Modul einsetzen",                                          // 5
        "Paket 1 abgeschlossen (Zwischenstand)",                                 // 6
        "Küche freischalten: Küchen-Modul auf dem Mini-Haus einsetzen",          // 7
        "Bob-Puppe ins Kinderzimmer-Modul stellen",                              // 8
        "Beine-Teil aus dem Schaukelpferd nehmen",                               // 9
        "Zwei Hausarbeiten erledigen (Staub, Kartoffeln, Blumen, Kinderzimmer)", // 10
        "Torvald-Puppe in die Küche stellen (Büro-Heist beginnt)",               // 11
        "Büro durchsuchen: Bankunterlagen, Schlüssel, Arm-Teil, Papier",         // 12
        "Schatulle mit dem Büro-Schlüssel öffnen",                               // 13
        "Schatullen-Code eingeben (Innenbox)",                                   // 14
        "Papas Taschenuhr aus der Innenbox nehmen",                              // 15
        "Klavier-Rätsel: Kopf-Teil finden (Reihenfolge)",                        // 16
        "Torso-Teil im Schlafzimmer-Kleiderschrank finden",                      // 17
        "Linde-Puppe ins Mini-Haus stellen",                                     // 18
        "Lindes Geheimnis: Entscheidung treffen",                                // 19
        "Krogstad erscheint",                                                    // 20
        "Rank: Mantel an der Garderobe prüfen",                                  // 21
        "Kinder: drei Verstecke finden",                                         // 22
        "Paket 2 abgeschlossen – Krogstads Konfrontation steht bevor."           // 23
    };

    private static readonly string[] PianoOrder = { "Klavier_1", "Klavier_3", "Klavier_2" };
    private const string SafeCode = "2909";

    private int stepIndex;
    private float stepTimer;
    private string confirmText = "";
    private float confirmTimer;

    private string makronenChoice = "";
    private string lindeChoice = "";
    private string codeInput = "";
    private int pianoProgress;
    private bool keyFound;
    private bool codeHintShown;
    private bool ringWorn;
    private bool pianoSolved;
    private bool heistRunning;
    private bool heistCaught;
    private float heistTimer;
    private readonly HashSet<string> heistItems = new HashSet<string>();
    private readonly HashSet<string> chores = new HashSet<string>();
    private readonly HashSet<string> hideSpots = new HashSet<string>();

    public int StepIndex => stepIndex;
    public string MakronenChoice => makronenChoice;
    public string LindeChoice => lindeChoice;
    public bool KeyFound => keyFound;
    public bool CodeHintShown => codeHintShown;
    public bool RingWorn => ringWorn;
    public bool PianoSolved => pianoSolved;
    public bool HeistCaught => heistCaught;
    public bool OverlayVisible
    {
        get => overlayVisible;
        set => overlayVisible = value;
    }

    private void Update()
    {
        if (confirmTimer > 0f)
            confirmTimer -= Time.deltaTime;

        if (heistRunning)
        {
            heistTimer -= Time.deltaTime;
            if (heistTimer <= 0f)
            {
                heistRunning = false;
                if (heistItems.Count < 4)
                {
                    heistCaught = true;
                    Confirm("Torvald bemerkt die offenen Schubladen. Die Suche läuft weiter.");
                }
            }
        }

        if (stepIndex >= Titles.Length)
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
                if (Near(torvaldPuppet, officeModule != null ? officeModule.transform : null, puppetDistance))
                    Complete("Torvald im Büro. Seine Stimme aus dem Büro: \"Is my little lark chirping out there?\"");
                break;

            case 5:
                if (childrenModule != null && childrenModule.IsPlacedOnGrid)
                    Complete("Kinderzimmer-Modul eingesetzt.");
                break;

            case 6:
                stepTimer += Time.deltaTime;
                if (stepTimer >= 2f)
                    Complete("Paket 1 abgeschlossen. Paket 2 beginnt.");
                break;

            case 7:
                if (kitchenModule != null && kitchenModule.IsPlacedOnGrid)
                    Complete("Küche freigeschaltet.");
                break;

            case 8:
                if (Near(bobPuppet, childrenModule != null ? childrenModule.transform : null, puppetDistance))
                {
                    if (figureBob != null) figureBob.SetActive(true);
                    Complete("Bob im Kinderzimmer: \"Mama? Anne-Marie was holding my hand... Is it already Christmas?\"");
                }
                break;

            case 11:
                if (Near(torvaldPuppet, kitchenModule != null ? kitchenModule.transform : null, puppetDistance))
                {
                    heistRunning = true;
                    heistTimer = heistDuration;
                    Complete("Torvald geht in die Küche. Die Zeit läuft.");
                }
                break;

            case 12:
                if (heistItems.Count >= 4)
                {
                    heistRunning = false;
                    Complete("Büro durchsucht.");
                }
                break;

            case 18:
                if (Near(lindePuppet, roomAMini, puppetDistance))
                {
                    if (figureLinde != null) figureLinde.SetActive(true);
                    Complete("Linde erscheint im Flur.");
                }
                break;

            case 20:
                stepTimer += Time.deltaTime;
                if (stepTimer >= krogstadDelay)
                {
                    if (figureKrogstad != null) figureKrogstad.SetActive(true);
                    Complete("Krogstad: \"It is only dry business matters, madame.\"");
                }
                break;
        }
    }

    /// <summary>
    /// Meldung von Interactables und Pickups. Wirkt nur im Schritt, zu dem die id gehört.
    /// </summary>
    public void Report(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        // Ehering ist eine optionale Entscheidung innerhalb der Schatulle, ohne Schrittwechsel.
        if (id == "Ehering")
        {
            if (keyFound)
            {
                ringWorn = true;
                Confirm("Ehering angezogen.");
            }
            return;
        }

        switch (stepIndex)
        {
            case 4:
                if (id == "Essen" || id == "Verbrennen" || id == "Verstecken")
                {
                    makronenChoice = id;
                    Complete("Makronen: " + id + ".");
                }
                break;

            case 9:
                if (id == "Beine")
                    Complete("Beine-Teil aus dem Schaukelpferd genommen.");
                break;

            case 10:
                if (id == "Staub" || id == "Kartoffeln" || id == "Blumen" || id == "Kinderzimmer")
                {
                    chores.Add(id);
                    if (chores.Count >= 2)
                        Complete("Zwei Hausarbeiten geschafft.");
                }
                break;

            case 12:
                if (id == "Bankunterlagen" || id == "Schluessel" || id == "Arm" || id == "Papier")
                {
                    if (heistItems.Add(id))
                    {
                        if (id == "Schluessel") keyFound = true;
                        if (id == "Papier") codeHintShown = true;
                        Confirm("Gefunden: " + id + " (" + heistItems.Count + " von 4).");
                    }
                }
                break;

            case 13:
                if (id == "Schatulle" && keyFound)
                    Complete("Schatulle geöffnet. Ehering, Konto-Heft und Innenbox.");
                break;

            case 14:
                if (id.StartsWith("Ziffer_"))
                {
                    codeInput += id.Substring(7);
                    if (codeInput == SafeCode)
                    {
                        Complete("Innenbox geöffnet.");
                    }
                    else if (!SafeCode.StartsWith(codeInput))
                    {
                        codeInput = "";
                        Confirm("Falsche Eingabe. Die Zahlen werden zurückgesetzt.");
                    }
                }
                break;

            case 15:
                if (id == "Taschenuhr")
                    Complete("Papas Taschenuhr genommen. Ticken aus der Erinnerung.");
                break;

            case 16:
                if (id == PianoOrder[pianoProgress])
                {
                    pianoProgress++;
                    if (pianoProgress >= PianoOrder.Length)
                    {
                        pianoSolved = true;
                        Complete("Klavier-Rätsel gelöst. Das Kopf-Teil liegt im Resonanzraum.");
                    }
                }
                else if (id.StartsWith("Klavier_"))
                {
                    pianoProgress = 0;
                    Confirm("Falsche Taste. Die Reihenfolge beginnt neu.");
                }
                break;

            case 17:
                if (id == "Torso")
                    Complete("Torso-Teil im Kleiderschrank gefunden.");
                break;

            case 19:
                if (id == "Linde_Alles" || id == "Linde_Andeuten" || id == "Linde_Schweigen")
                {
                    lindeChoice = id;
                    Complete("Linde: " + id + ".");
                }
                break;

            case 21:
                if (id == "Rank_Mantel")
                {
                    if (figureRank != null) figureRank.SetActive(true);
                    Complete("Rank erscheint: \"I'd like the torment to last as long as possible.\"");
                }
                break;

            case 22:
                if (id == "Versteck_1" || id == "Versteck_2" || id == "Versteck_3")
                {
                    hideSpots.Add(id);
                    if (hideSpots.Count >= 3)
                    {
                        if (figureAnneMarie != null) figureAnneMarie.SetActive(true);
                        Complete("Die Kinder sind gefunden. Anne-Marie ist da.");
                    }
                }
                break;
        }
    }

    private void Complete(string text)
    {
        Debug.Log("[Tag1] " + text);
        Confirm(text);
        stepIndex++;
        stepTimer = 0f;
    }

    private void Confirm(string text)
    {
        Debug.Log("[Tag1] " + text);
        confirmText = text;
        confirmTimer = confirmDuration;
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

    private static bool Near(Transform a, Transform b, float distance)
    {
        if (a == null || b == null)
            return false;

        return Vector3.Distance(a.position, b.position) <= distance;
    }

    private void OnGUI()
    {
        if (!overlayVisible)
            return;

        string current = Titles[Mathf.Min(stepIndex, Titles.Length - 1)];
        string next = stepIndex < Titles.Length - 1 ? Titles[stepIndex + 1] : "-";

        GUILayout.BeginArea(new Rect(10f, 10f, 760f, 140f), GUI.skin.box);
        GUILayout.Label("Aktuelles Ziel: " + current);
        if (confirmTimer > 0f && confirmText.Length > 0)
            GUILayout.Label("✓ " + confirmText);
        GUILayout.Label("Nächstes Ziel: " + next);
        GUILayout.EndArea();
    }
}
