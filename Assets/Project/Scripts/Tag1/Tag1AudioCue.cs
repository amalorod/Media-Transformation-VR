using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bekannte Audio-Cue-IDs für Tag 1.
/// Diese IDs sind der einzige Vertrag zwischen Story-Logik und Audio.
/// Es sind reine Bezeichner – es existiert (noch) keine Audiodatei dazu.
/// </summary>
public static class Tag1CueIds
{
    public const string TorvaldLark = "tag1.torvald.lark";
    public const string KrogstadConfrontation = "tag1.krogstad.confrontation";
    public const string ChoiceConfirm = "tag1.choice.confirm";
    public const string ObjectiveComplete = "tag1.objective.complete";
    public const string EchoMajor = "tag1.echo.major";
    public const string EchoMinor = "tag1.echo.minor";
    public const string DoorLock = "tag1.door.lock";
    public const string RoomSnap = "tag1.room.snap";
    public const string PuppetPlace = "tag1.puppet.place";

    /// <summary>Alle bekannten IDs – praktisch für die Inspector-Vorbelegung.</summary>
    public static readonly string[] All =
    {
        TorvaldLark,
        KrogstadConfrontation,
        ChoiceConfirm,
        ObjectiveComplete,
        EchoMajor,
        EchoMinor,
        DoorLock,
        RoomSnap,
        PuppetPlace
    };
}

/// <summary>
/// Zentrale, austauschbare Audio-Cue-Ablage für Tag 1.
///
/// Wichtig: Ein Cue darf ohne AudioClip existieren. Fehlt der Clip, wird nur
/// eine Debug-Ausgabe mit der eindeutigen Cue-ID erzeugt – es wird niemals
/// eine Exception geworfen. Sobald das Team echte Audiodateien liefert, müssen
/// nur die Clips im Inspector zugewiesen werden; kein Code ändert sich.
/// </summary>
[DisallowMultipleComponent]
public class Tag1AudioCue : MonoBehaviour
{
    [Serializable]
    public class CueEntry
    {
        [Tooltip("Eindeutige Cue-ID, z. B. tag1.torvald.lark")]
        public string cueId = "";

        [Tooltip("Darf leer (null) sein. Solange kein Clip gesetzt ist, wird nur geloggt.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Optionaler Ersatztext, falls später Untertitel statt Audio genutzt werden.")]
        [TextArea(2, 3)]
        public string subtitle = "";
    }

    public static Tag1AudioCue Instance { get; private set; }

    [Header("Wiedergabe")]
    [Tooltip("Optional. Fehlt die Quelle, wird eine eigene AudioSource am selben Objekt genutzt.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Bei fehlendem Clip eine Debug-Ausgabe mit der Cue-ID schreiben.")]
    [SerializeField] private bool logWhenClipMissing = true;

    [Header("Cue-Tabelle (austauschbar im Inspector)")]
    [SerializeField] private List<CueEntry> cues = new List<CueEntry>();

    private bool hasWarnedMissingSource;

    private void Awake()
    {
        // Erste Instanz gewinnt; verhindert NullReference bei mehrfacher Platzierung.
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Spielt einen Cue. Wirft nie eine Exception – auch nicht bei unbekannter ID
    /// oder fehlendem Clip.
    /// </summary>
    public void Play(string cueId)
    {
        if (string.IsNullOrEmpty(cueId))
        {
            Debug.LogWarning("[Tag1AudioCue] Play() ohne Cue-ID aufgerufen.", this);
            return;
        }

        CueEntry entry = Find(cueId);
        if (entry == null)
        {
            // Unbekannte ID ist kein Fehler – die Story darf weiterlaufen.
            Debug.Log($"[Tag1AudioCue] '{cueId}' (kein Eintrag in der Cue-Tabelle)", this);
            return;
        }

        if (entry.clip == null)
        {
            if (logWhenClipMissing)
                Debug.Log($"[Tag1AudioCue] '{cueId}' (kein AudioClip zugewiesen)", this);
            return;
        }

        AudioSource source = ResolveSource();
        if (source == null)
        {
            // Ohne Quelle bleibt der Cue folgenlos – aber ohne Exception.
            if (!hasWarnedMissingSource)
            {
                hasWarnedMissingSource = true;
                Debug.LogWarning(
                    $"[Tag1AudioCue] '{cueId}' kann nicht abgespielt werden: keine AudioSource verfügbar.",
                    this);
            }
            return;
        }

        source.PlayOneShot(entry.clip, Mathf.Clamp01(entry.volume));
    }

    /// <summary>Statische Abkürzung; funktioniert auch ohne vorhandene Instanz.</summary>
    public static void PlayCue(string cueId)
    {
        if (Instance == null)
        {
            Debug.Log($"[Tag1AudioCue] '{cueId}' (kein Tag1AudioCue in der Szene)", null);
            return;
        }

        Instance.Play(cueId);
    }

    /// <summary>Liefert den hinterlegten Untertiteltext, sonst einen leeren String.</summary>
    public string GetSubtitle(string cueId)
    {
        CueEntry entry = Find(cueId);
        return entry != null ? entry.subtitle : string.Empty;
    }

    /// <summary>Wird beim ersten Start einmalig mit den bekannten IDs vorbelegt.</summary>
    public void EnsureDefaultEntries()
    {
        if (cues == null)
            cues = new List<CueEntry>();

        foreach (string id in Tag1CueIds.All)
        {
            if (Find(id) != null)
                continue;

            cues.Add(new CueEntry { cueId = id, clip = null, volume = 1f, subtitle = "" });
        }
    }

    private CueEntry Find(string cueId)
    {
        if (cues == null)
            return null;

        foreach (CueEntry entry in cues)
        {
            if (entry != null && string.Equals(entry.cueId, cueId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private AudioSource ResolveSource()
    {
        if (audioSource != null)
            return audioSource;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        return audioSource;
    }
}
