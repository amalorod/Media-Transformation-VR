using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Reflection;

/// <summary>
/// Ein Untertitel, der zu einem bestimmten Zeitpunkt der EINEN Audio-Datei ein- und ausgeblendet wird.
/// </summary>
[System.Serializable]
public class SubtitleCue
{
    [TextArea(2, 4)]
    public string text;

    [Tooltip("Sekunde in der Audio-Datei, ab der der Text eingeblendet wird (Fade in).")]
    public float startTime;

    [Tooltip("Sekunde in der Audio-Datei, bis der Text sichtbar bleibt (danach Fade out).")]
    public float endTime;
}

[RequireComponent(typeof(XRGrabInteractable))]
public class AudioArtefact : MonoBehaviour
{
    [Header("Voice (EINE Audio-Datei, Echo etc. ist schon eingebaut)")]
    [Tooltip("Länge dieser Datei = Länge der Rückblende.")]
    public AudioClip flashbackAudio;
    public AudioSource artefactAudioSource;

    [Header("Ambient-Musik (wiederverwendbar, loopt)")]
    public AudioSource ambientAudioSource;
    [Tooltip("Optional: ersetzt den Clip der Ambient-Source nur für dieses Artefakt.")]
    public AudioClip ambientClipOverride;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;

    [Header("Hintergrundgeräusche (optional, pro Artefakt, loopt)")]
    [Tooltip("z. B. Krankenhaus-Töne bei Torvalds Medizinflasche. Leer lassen = keine.")]
    public AudioSource backgroundSfxSource;
    public AudioClip backgroundSfxClip;
    [Range(0f, 1f)] public float backgroundSfxVolume = 0.4f;

    [Header("Untertitel (Zeiten in Sekunden der Audio-Datei)")]
    public List<SubtitleCue> subtitleCues = new List<SubtitleCue>();
    public GameObject subtitleCanvasObject;
    public TextMeshProUGUI subtitleTextUI;
    [Tooltip("Gewünschter Abstand zum Kopf. Der Text rückt näher (und wird kleiner), wenn dahinter eine Wand ist.")]
    public float subtitleDistance = 1.5f;
    [Tooltip("Höhe relativ zu den Augen. Positiv = über der Blickmitte. Der Text ignoriert Hoch-/Runterschauen.")]
    public float subtitleHeightOffset = 0.2f;
    [Tooltip("Dauer von Fade in und Fade out je Text.")]
    public float subtitleFadeTime = 0.5f;

    [Header("Untertitel: Folgen (nur seitlich) und Wände")]
    [Tooltip("Der Text bleibt stehen, bis man sich um mehr als diesen Winkel wegdreht, und schwenkt dann nach.")]
    public float followAngleThreshold = 30f;
    [Tooltip("Wie weich der Text nachschwenkt (Sekunden, kleiner = schneller).")]
    public float followSmoothTime = 0.5f;
    [Tooltip("Nur Wände/Umgebung auswählen, nicht Hände, Spieler oder das Artefakt.")]
    public LayerMask subtitleObstacleMask = ~0;
    [Tooltip("Mindestplatz um den Text herum, damit er nicht in Wände ragt.")]
    public float subtitleCollisionRadius = 0.3f;
    [Tooltip("So nah darf der Text dem Kopf höchstens kommen.")]
    public float subtitleMinDistance = 0.5f;

    [Header("Ablauf")]
    [Tooltip("Atmosphäre und Effekte blenden so lange ein.")]
    public float fadeInTime = 3f;
    [Tooltip("So lange läuft die Atmosphäre allein, bevor die Stimme startet.")]
    public float introDelay = 2f;
    [Tooltip("So lange läuft die Atmosphäre nach der Stimme weiter.")]
    public float outroDelay = 2f;
    [Tooltip("Atmosphäre und Effekte blenden so lange aus.")]
    public float fadeOutTime = 3f;

    [Header("Effekte (Global Volume, Weight wird geblendet)")]
    [Tooltip("Ein Global Volume mit Vignette, Color Adjustments und Depth of Field. Weight im Editor auf 0.")]
    public Volume flashbackVolume;

    [Header("Bewegung")]
    [Tooltip("Faktor auf die normale Geschwindigkeit, z. B. 0.3 = 30 %.")]
    [Range(0.05f, 1f)] public float slowedSpeedMultiplier = 0.3f;
    [Tooltip("Das Objekt 'Move' aus dem Rig (Locomotion > Move). Wird automatisch gefunden, falls leer.")]
    public GameObject moveProviderObject;

    // Move Speed wird per Reflection gelesen/gesetzt, damit es mit jeder XRI-Version
    // und jedem Move Provider (z. B. Dynamic Move Provider) funktioniert.
    private Component moveComponent;
    private PropertyInfo moveSpeedProperty;
    private FieldInfo moveSpeedField;

    private XRGrabInteractable grabInteractable;
    private Transform mainCameraTransform;
    private CanvasGroup subtitleGroup;
    private bool isTriggered = false;
    private float originalMoveSpeed = 1f;
    private string currentSubtitleText = null;

    private Vector3 subtitleBaseScale = Vector3.one;
    private float subtitleYaw;
    private float subtitleYawVelocity;
    private bool isFollowingYaw = false;
    private float subtitleDistanceRatio = 1f;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        FindMoveProvider();

        if (!HasMoveSpeed)
        {
            string components = "";
            if (moveProviderObject != null)
            {
                foreach (Component c in moveProviderObject.GetComponents<Component>())
                {
                    components += (c != null ? c.GetType().Name : "(fehlendes Script)") + ", ";
                }
            }
            Debug.LogWarning("AudioArtefact: Keinen Move Speed gefunden. Die Bewegung wird nicht verlangsamt. Zugewiesenes Objekt: "
                + (moveProviderObject != null ? moveProviderObject.name : "keins")
                + (components != "" ? " | Komponenten: " + components : ""), this);
        }
        else
        {
            Debug.Log("AudioArtefact: Move Speed gefunden an " + moveComponent.name + " (" + moveComponent.GetType().Name + "), Wert = " + GetMoveSpeed(), moveComponent);
        }

        if (subtitleCanvasObject != null)
        {
            subtitleBaseScale = subtitleCanvasObject.transform.localScale;
            subtitleGroup = subtitleCanvasObject.GetComponent<CanvasGroup>();
            if (subtitleGroup == null)
            {
                subtitleGroup = subtitleCanvasObject.AddComponent<CanvasGroup>();
            }
            subtitleGroup.alpha = 0f;
            subtitleCanvasObject.SetActive(false);
        }

        if (flashbackVolume != null)
        {
            flashbackVolume.weight = 0f;
        }
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void Update()
    {
        if (subtitleCanvasObject != null && subtitleCanvasObject.activeSelf)
        {
            UpdateSubtitlePosition(false);
        }
    }

    /// <summary>
    /// Text hängt auf fester Höhe (relativ zu den Augen) vor dem Spieler, folgt nur seitlich
    /// (erst nach einer Drehung über den Schwellwinkel) und rückt vor Wänden näher heran.
    /// </summary>
    private void UpdateSubtitlePosition(bool snap)
    {
        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        if (mainCameraTransform == null) return;

        Vector3 camPos = mainCameraTransform.position;

        // 1. Seitliche Richtung (Yaw), Hoch-/Runterschauen wird ignoriert
        Vector3 flatForward = Vector3.ProjectOnPlane(mainCameraTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f)
        {
            float camYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;

            if (snap)
            {
                subtitleYaw = camYaw;
                subtitleYawVelocity = 0f;
                isFollowingYaw = false;
            }
            else
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(subtitleYaw, camYaw));
                if (delta > followAngleThreshold) isFollowingYaw = true;
                else if (delta < 3f) isFollowingYaw = false;

                if (isFollowingYaw)
                {
                    subtitleYaw = Mathf.SmoothDampAngle(subtitleYaw, camYaw, ref subtitleYawVelocity, followSmoothTime);
                }
            }
        }

        Quaternion yawRotation = Quaternion.Euler(0f, subtitleYaw, 0f);

        // 2. Wunschposition und Wandabstand (Text rückt näher und wird kleiner, wirkt aber gleich groß)
        Vector3 toTarget = yawRotation * Vector3.forward * subtitleDistance + Vector3.up * subtitleHeightOffset;
        float maxDistance = toTarget.magnitude;
        Vector3 castDirection = toTarget / maxDistance;

        float targetRatio = 1f;
        if (Physics.SphereCast(camPos, subtitleCollisionRadius, castDirection, out RaycastHit hit, maxDistance,
                subtitleObstacleMask, QueryTriggerInteraction.Ignore))
        {
            float minRatio = Mathf.Clamp01(subtitleMinDistance / maxDistance);
            targetRatio = Mathf.Clamp(hit.distance / maxDistance, minRatio, 1f);
        }

        subtitleDistanceRatio = snap ? targetRatio : Mathf.Lerp(subtitleDistanceRatio, targetRatio, Time.deltaTime * 8f);

        // 3. Anwenden: aufrecht, dem Spieler zugewandt
        Transform canvasTransform = subtitleCanvasObject.transform;
        canvasTransform.position = camPos + castDirection * (maxDistance * subtitleDistanceRatio);
        canvasTransform.rotation = yawRotation;
        canvasTransform.localScale = subtitleBaseScale * subtitleDistanceRatio;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isTriggered) return;
        isTriggered = true;

        StartCoroutine(PlayFlashback());
    }

    private IEnumerator PlayFlashback()
    {
        // 1. Atmosphäre einblenden: Musik, Geräusche, Effekte, langsamere Bewegung
        if (HasMoveSpeed)
        {
            originalMoveSpeed = GetMoveSpeed();
            Debug.Log("AudioArtefact: Move Speed " + originalMoveSpeed + " -> " + (originalMoveSpeed * slowedSpeedMultiplier));
        }

        StartLoop(ambientAudioSource, ambientClipOverride, ambientVolume);
        StartLoop(backgroundSfxSource, backgroundSfxClip, backgroundSfxVolume);
        StartCoroutine(FadeVolumeWeight(1f, fadeInTime));
        StartCoroutine(FadeMoveSpeed(originalMoveSpeed * slowedSpeedMultiplier, fadeInTime));

        PrepareSubtitle();
        yield return new WaitForSeconds(introDelay);

        // 2. Stimme (eine Datei) und Untertitel nach Zeitmarken
        float length = GetFlashbackLength();

        if (flashbackAudio != null && artefactAudioSource != null)
        {
            artefactAudioSource.clip = flashbackAudio;
            artefactAudioSource.loop = false;
            artefactAudioSource.Play();
        }

        float t = 0f;
        while (t < length)
        {
            UpdateSubtitle(t);
            t += Time.deltaTime;
            yield return null;
        }
        SetSubtitle(null, 0f);

        // 3. Ausklingen: Atmosphäre läuft kurz weiter, dann alles ausblenden
        yield return new WaitForSeconds(outroDelay);

        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, 0f, fadeOutTime));
        if (backgroundSfxSource != null) StartCoroutine(FadeAudio(backgroundSfxSource, 0f, fadeOutTime));
        StartCoroutine(FadeVolumeWeight(0f, fadeOutTime));
        StartCoroutine(FadeMoveSpeed(originalMoveSpeed, fadeOutTime));

        yield return new WaitForSeconds(fadeOutTime);

        if (ambientAudioSource != null) ambientAudioSource.Stop();
        if (backgroundSfxSource != null) backgroundSfxSource.Stop();
        if (subtitleCanvasObject != null) subtitleCanvasObject.SetActive(false);
    }

    private float GetFlashbackLength()
    {
        if (flashbackAudio != null) return flashbackAudio.length;

        // Fallback ohne Audio: Ende des letzten Untertitels
        float last = 0f;
        foreach (SubtitleCue cue in subtitleCues)
        {
            if (cue.endTime > last) last = cue.endTime;
        }
        return last;
    }

    // ---------- Audio ----------

    private void StartLoop(AudioSource source, AudioClip clipOverride, float targetVolume)
    {
        if (source == null) return;
        if (clipOverride != null) source.clip = clipOverride;
        if (source.clip == null) return;

        source.loop = true;
        source.volume = 0f;
        source.Play();
        StartCoroutine(FadeAudio(source, targetVolume, fadeInTime));
    }

    private IEnumerator FadeAudio(AudioSource source, float target, float time)
    {
        float start = source.volume;
        float duration = Mathf.Max(time, 0.01f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        source.volume = target;
    }

    // ---------- Effekte ----------

    private IEnumerator FadeVolumeWeight(float target, float time)
    {
        if (flashbackVolume == null) yield break;

        float start = flashbackVolume.weight;
        float duration = Mathf.Max(time, 0.01f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            flashbackVolume.weight = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        flashbackVolume.weight = target;
    }

    // ---------- Bewegung ----------

    private IEnumerator FadeMoveSpeed(float target, float time)
    {
        if (!HasMoveSpeed) yield break;

        float start = GetMoveSpeed();
        float duration = Mathf.Max(time, 0.01f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetMoveSpeed(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetMoveSpeed(target);
    }

    private bool HasMoveSpeed
    {
        get { return moveComponent != null && (moveSpeedProperty != null || moveSpeedField != null); }
    }

    private float GetMoveSpeed()
    {
        return moveSpeedProperty != null
            ? (float)moveSpeedProperty.GetValue(moveComponent)
            : (float)moveSpeedField.GetValue(moveComponent);
    }

    private void SetMoveSpeed(float value)
    {
        if (moveSpeedProperty != null) moveSpeedProperty.SetValue(moveComponent, value);
        else moveSpeedField.SetValue(moveComponent, value);
    }

    private void FindMoveProvider()
    {
        // 1. Zugewiesenes Objekt: erste Komponente mit "Move Speed" nehmen
        if (moveProviderObject != null)
        {
            foreach (Component c in moveProviderObject.GetComponents<MonoBehaviour>())
            {
                if (BindMoveSpeed(c)) return;
            }
        }

        // 2. Sonst in der Szene suchen (auch deaktivierte Objekte)
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null) continue;
            if (!behaviour.GetType().Name.Contains("MoveProvider")) continue;
            if (BindMoveSpeed(behaviour)) return;
        }
    }

    private bool BindMoveSpeed(Component component)
    {
        if (component == null) return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (System.Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty("moveSpeed", flags);
            if (property != null && property.PropertyType == typeof(float) && property.CanRead && property.CanWrite)
            {
                moveComponent = component;
                moveSpeedProperty = property;
                moveSpeedField = null;
                return true;
            }

            FieldInfo field = type.GetField("m_MoveSpeed", flags) ?? type.GetField("moveSpeed", flags);
            if (field != null && field.FieldType == typeof(float))
            {
                moveComponent = component;
                moveSpeedProperty = null;
                moveSpeedField = field;
                return true;
            }
        }

        return false;
    }

    // ---------- Untertitel ----------

    private void PrepareSubtitle()
    {
        if (subtitleCanvasObject == null) return;

        UpdateSubtitlePosition(true);

        subtitleGroup.alpha = 0f;
        subtitleCanvasObject.SetActive(true);
    }

    private void UpdateSubtitle(float t)
    {
        float fade = Mathf.Max(subtitleFadeTime, 0.01f);
        float bestAlpha = 0f;
        string bestText = null;

        foreach (SubtitleCue cue in subtitleCues)
        {
            if (t < cue.startTime || t > cue.endTime) continue;

            float alpha = Mathf.Min(
                Mathf.Clamp01((t - cue.startTime) / fade),
                Mathf.Clamp01((cue.endTime - t) / fade));

            if (bestText == null || alpha >= bestAlpha)
            {
                bestAlpha = alpha;
                bestText = cue.text;
            }
        }

        SetSubtitle(bestText, bestAlpha);
    }

    private void SetSubtitle(string text, float alpha)
    {
        if (subtitleGroup == null || subtitleTextUI == null) return;

        if (text != null && text != currentSubtitleText)
        {
            subtitleTextUI.text = text;
        }
        currentSubtitleText = text;
        subtitleGroup.alpha = alpha;
    }
}