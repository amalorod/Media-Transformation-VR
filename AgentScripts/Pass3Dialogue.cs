using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pass 3: austauschbare Dialog-/Audio-Grundlage in der Szene verankern.
///
/// Es werden bewusst KEINE Dialogtexte und KEINE Voice Lines erzeugt.
/// Angelegt wird nur die Struktur, die das Team im Inspector fuellen kann:
///   - Tag1AudioCue     (Cue-ID -> AudioClip, Clip darf null sein)
///   - Tag1DialoguePresenter (beobachtet Tag1FlowController.StepIndex, zeigt Text/Cue)
/// Die Fortschrittslogik und die Auswahlpunkte bleiben unveraendert.
/// </summary>
public static class Pass3Dialogue
{
    public static string Main()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();

        var systems = GameObject.Find("GameSystems");
        if (systems == null)
            return "FEHLER: GameSystems nicht gefunden.";

        var flow = UnityEngine.Object.FindObjectsByType<Tag1FlowController>().FirstOrDefault();

        // ---- Audio-Cue-Ablage -------------------------------------------------
        var audioCue = systems.GetComponent<Tag1AudioCue>();
        if (audioCue == null)
        {
            audioCue = Undo.AddComponent<Tag1AudioCue>(systems);
            sb.AppendLine("[NEU] Tag1AudioCue auf _SYSTEMS/GameSystems angelegt.");
        }
        else
        {
            sb.AppendLine("[VORHANDEN] Tag1AudioCue wiederverwendet (nicht dupliziert).");
        }

        audioCue.EnsureDefaultEntries();

        // ---- Dialog-Presenter -------------------------------------------------
        var presenter = systems.GetComponent<Tag1DialoguePresenter>();
        if (presenter == null)
        {
            presenter = Undo.AddComponent<Tag1DialoguePresenter>(systems);
            sb.AppendLine("[NEU] Tag1DialoguePresenter auf _SYSTEMS/GameSystems angelegt.");
        }
        else
        {
            sb.AppendLine("[VORHANDEN] Tag1DialoguePresenter wiederverwendet (nicht dupliziert).");
        }

        // ---- Verdrahtung ueber Inspector-Felder -------------------------------
        var presenterSo = new SerializedObject(presenter);
        SetRef(presenterSo, "flow", flow);
        SetRef(presenterSo, "audioCues", audioCue);
        presenterSo.ApplyModifiedPropertiesWithoutUndo();

        var cueSo = new SerializedObject(audioCue);
        cueSo.Update(); // frisch ergaenzte Cue-Eintraege einlesen

        sb.AppendLine(flow != null
            ? $"[VERDRAHTET] flow = {flow.gameObject.name} (StepIndex wird beobachtet, Logik unveraendert)."
            : "[WARNUNG] Kein Tag1FlowController gefunden - Presenter bleibt passiv.");

        // ---- Cue-Tabelle pruefen ---------------------------------------------
        var cueList = cueSo.FindProperty("cues");
        sb.AppendLine($"[CUES] {cueList.arraySize} Cue-IDs hinterlegt, alle AudioClips bewusst null:");
        for (int i = 0; i < cueList.arraySize; i++)
        {
            var entry = cueList.GetArrayElementAtIndex(i);
            var id = entry.FindPropertyRelative("cueId").stringValue;
            var clip = entry.FindPropertyRelative("clip").objectReferenceValue;
            sb.AppendLine($"      - {id}  clip={(clip == null ? "null (nur Debug-Ausgabe)" : clip.name)}");
        }

        // ---- Schritt-Tabelle (absichtlich leer) -------------------------------
        var steps = presenterSo.FindProperty("stepLines");
        sb.AppendLine($"[TEXTE] stepLines = {steps.arraySize} Eintraege (bewusst leer: das Team fuellt Sprecher/Text/Cue-ID im Inspector).");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return sb.ToString();
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[Pass3] Feld '{field}' nicht gefunden.");
            return;
        }

        property.objectReferenceValue = value;
    }
}
