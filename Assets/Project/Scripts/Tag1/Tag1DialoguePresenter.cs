using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Zentrale, austauschbare Text-/Dialoganzeige für Tag 1.
///
/// Bewusste Grenzen:
/// - Es wird KEIN neuer Dialogbaum erfunden. Die Texte sind reine Inspector-Daten.
/// - Die Fortschrittslogik bleibt vollständig in <see cref="Tag1FlowController"/>.
///   Dieser Presenter liest nur den öffentlichen <c>StepIndex</c> und zeigt an.
/// - Fehlende Texte, Clips oder Referenzen erzeugen niemals Exceptions,
///   sondern höchstens eine Debug-Ausgabe.
///
/// Späteres Teammodel: nur die Inspector-Felder (Sprecher/Text/Cue-ID) füllen,
/// optional eine AudioSource am Tag1AudioCue-Objekt zuweisen.
/// </summary>
[DisallowMultipleComponent]
public class Tag1DialoguePresenter : MonoBehaviour
{
    [Serializable]
    public class StepLine
    {
        [Tooltip("Tag1-Schritt (Tag1FlowController.StepIndex), für den diese Zeile gilt.")]
        public int stepIndex;

        [Tooltip("Sprechername, z. B. Torvald, Nora, Linde.")]
        public string speaker = "";

        [Tooltip("Anzuzeigender Text. Leer = für diesen Schritt wird nichts angezeigt.")]
        [TextArea(2, 4)]
        public string line = "";

        [Tooltip("Optionale Cue-ID, z. B. tag1.torvald.lark. Leer = kein Audio.")]
        public string cueId = "";

        [Tooltip("Automatische Anzeigedauer in Sekunden. 0 = Standarddauer benutzen.")]
        public float duration;
    }

    [Header("Anzeige-Referenzen (optional)")]
    [Tooltip("Optional. Bleibt das leer, baut der Presenter eine schlichte Weltraum-Anzeige.")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text lineText;

    [Header("Verhalten")]
    [Tooltip("Tag1FlowController, dessen StepIndex beobachtet wird. Optional.")]
    [SerializeField] private Tag1FlowController flow;

    [Tooltip("Cue-Ablage für die Audioausgabe. Optional.")]
    [SerializeField] private Tag1AudioCue audioCues;

    [Tooltip("Standarddauer der Anzeige in Sekunden.")]
    [SerializeField] private float defaultDuration = 5f;

    [Tooltip("Auch dann eine Debug-Zeile schreiben, wenn kein Text hinterlegt ist.")]
    [SerializeField] private bool logWhenUnconfigured = true;

    [Header("Texte pro Schritt (austauschbar im Inspector)")]
    [SerializeField] private List<StepLine> stepLines = new List<StepLine>();

    private Coroutine hideRoutine;
    private int lastObservedStep = int.MinValue;
    private bool runtimeUiAttempted;
    private TMP_Text runtimeSpeaker;
    private TMP_Text runtimeLine;
    private CanvasGroup runtimeGroup;

    private bool HasAssignedUi => lineText != null || speakerText != null;

    private void Awake()
    {
        HideImmediate();
    }

    private void OnEnable()
    {
        // Startzustand angleichen, damit der erste Schritt nicht verschluckt wird.
        lastObservedStep = int.MinValue;
    }

    private void Update()
    {
        if (flow == null)
            return;

        int step = flow.StepIndex;
        if (step == lastObservedStep)
            return;

        lastObservedStep = step;
        ShowForStep(step);
    }

    /// <summary>
    /// Zeigt den zum Schritt hinterlegten Text und spielt dessen Cue.
    /// Nicht konfigurierte Schritte werden stillschweigend übersprungen.
    /// </summary>
    public void ShowForStep(int stepIndex)
    {
        StepLine entry = Find(stepIndex);
        if (entry == null)
        {
            if (logWhenUnconfigured)
                Debug.Log($"[Tag1DialoguePresenter] Schritt {stepIndex}: kein Dialogtext hinterlegt.", this);
            return;
        }

        Show(entry.speaker, entry.line, entry.cueId, entry.duration);
    }

    /// <summary>
    /// Zentrale Anzeige-Methode. Alle Parameter dürfen leer sein.
    /// </summary>
    public void Show(string speaker, string line, string cueId = "", float duration = 0f)
    {
        if (!string.IsNullOrEmpty(cueId) && audioCues != null)
            audioCues.Play(cueId);

        if (string.IsNullOrEmpty(line) && string.IsNullOrEmpty(speaker))
        {
            // Reiner Audio-Cue ohne Text.
            Debug.Log($"[Tag1DialoguePresenter] Cue '{cueId}' ohne Text.", this);
            return;
        }

        if (!TryDisplay(speaker, line, cueId))
            Debug.Log($"[Tag1DialoguePresenter] {speaker}: {line} (cue '{cueId}')", this);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        float effective = duration > 0f ? duration : defaultDuration;
        if (effective > 0f)
            hideRoutine = StartCoroutine(HideAfter(effective));
    }

    /// <summary>Blendet die Anzeige aus.</summary>
    public void Clear()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        HideImmediate();
    }

    /// <summary>Trägt fehlende Standard-Cue-Einträge nach, ohne vorhandene zu überschreiben.</summary>
    public void EnsureAudioCues()
    {
        if (audioCues != null)
            audioCues.EnsureDefaultEntries();
    }

    private bool TryDisplay(string speaker, string line, string cueId)
    {
        if (HasAssignedUi)
        {
            if (speakerText != null)
                speakerText.text = speaker ?? string.Empty;
            if (lineText != null)
                lineText.text = line ?? string.Empty;
            if (panel != null)
                panel.alpha = 1f;
            return true;
        }

        EnsureRuntimeUi();
        if (runtimeLine == null && runtimeSpeaker == null)
            return false;

        if (runtimeSpeaker != null)
            runtimeSpeaker.text = speaker ?? string.Empty;
        if (runtimeLine != null)
            runtimeLine.text = line ?? string.Empty;
        if (runtimeGroup != null)
            runtimeGroup.alpha = 1f;
        return true;
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        HideImmediate();
        hideRoutine = null;
    }

    private void HideImmediate()
    {
        if (panel != null)
            panel.alpha = 0f;

        if (runtimeGroup != null)
            runtimeGroup.alpha = 0f;
    }

    private StepLine Find(int stepIndex)
    {
        if (stepLines == null)
            return null;

        foreach (StepLine entry in stepLines)
        {
            if (entry != null && entry.stepIndex == stepIndex)
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Baut nur dann eine Anzeige, wenn keine Referenzen zugewiesen sind.
    /// Schlägt der Aufbau fehl, bleibt der Presenter funktionsfähig (nur Log).
    /// </summary>
    private void EnsureRuntimeUi()
    {
        if (runtimeUiAttempted || HasAssignedUi)
            return;

        runtimeUiAttempted = true;

        try
        {
            if (TMP_Settings.defaultFontAsset == null)
                return;

            GameObject canvasObject = new GameObject("Tag1DialogueCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            runtimeGroup = canvasObject.AddComponent<CanvasGroup>();
            runtimeGroup.alpha = 0f;
            runtimeGroup.interactable = false;
            runtimeGroup.blocksRaycasts = false;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(420f, 140f);
            canvasObject.transform.localScale = Vector3.one * 0.0012f;
            canvasObject.transform.localPosition = new Vector3(0f, -0.25f, 1.1f);

            runtimeSpeaker = CreateText(canvasObject.transform, "Speaker", new Vector2(0f, 34f), 30f, FontStyles.Bold);
            runtimeLine = CreateText(canvasObject.transform, "Line", new Vector2(0f, -12f), 26f, FontStyles.Normal);
        }
        catch (Exception exception)
        {
            // Niemals eine Exception nach außen tragen – nur loggen.
            runtimeSpeaker = null;
            runtimeLine = null;
            Debug.LogWarning($"[Tag1DialoguePresenter] Laufzeit-Anzeige konnte nicht erstellt werden: {exception.Message}", this);
        }
    }

    private TMP_Text CreateText(Transform parent, string name, Vector2 anchoredPosition, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.96f, 0.9f, 1f);
        text.raycastTarget = false;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400f, 60f);
        rect.anchoredPosition = anchoredPosition;
        return text;
    }
}
