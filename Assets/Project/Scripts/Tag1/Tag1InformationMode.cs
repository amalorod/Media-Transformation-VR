using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Temporärer Informationsmodus auf dem rechten Secondary Button (Quest: B).
/// Nur solange der Button gehalten wird, sind Objective-Canvas und Objektlabels sichtbar.
/// Kein Toggle. Die Y-Taste bleibt ausschließlich der Türsteuerung vorbehalten.
/// </summary>
public class Tag1InformationMode : MonoBehaviour
{
    /// <summary>True, solange der Informationsmodus gehalten wird.</summary>
    public static bool Active { get; private set; }

    [Tooltip("Optional: vorhandene InputActionReference auf den rechten Secondary Button. " +
             "Bleibt das Feld leer, wird zur Laufzeit eine eigene Action auf <XRController>{RightHand}/secondaryButton erzeugt.")]
    [SerializeField] private InputActionReference infoAction;

    private InputActionAsset runtimeAsset;
    private InputAction runtimeAction;

    private bool ActionReady => infoAction != null && infoAction.action != null;

    private void Awake()
    {
        if (ActionReady)
            return;

        // InputActionReference.Create verlangt eine Action, die zu einem InputActionAsset gehört.
        // Deshalb wird hier ein kleines Asset nur im Speicher aufgebaut. Vorhandene Projekt-Assets bleiben unberührt.
        runtimeAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        InputActionMap map = runtimeAsset.AddActionMap("Tag1");
        runtimeAction = map.AddAction("Information", InputActionType.Button);
        runtimeAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        infoAction = InputActionReference.Create(runtimeAction);
    }

    private void OnEnable()
    {
        if (ActionReady)
            infoAction.action.Enable();
    }

    private void OnDisable()
    {
        SetActive(false);
        if (ActionReady)
            infoAction.action.Disable();
    }

    private void OnDestroy()
    {
        if (runtimeAsset != null)
        {
            runtimeAsset.Disable();
            Destroy(runtimeAsset);
            runtimeAsset = null;
            runtimeAction = null;
        }
        else if (runtimeAction != null)
        {
            runtimeAction.Dispose();
            runtimeAction = null;
        }
    }

    private void Update()
    {
        SetActive(ActionReady && infoAction.action.IsPressed());
    }

    private static void SetActive(bool value)
    {
        if (Active == value)
            return;

        Active = value;
        Tag1InteractableLabel.SetAllLabelsVisible(value);
    }
}
