using System.Collections;
using UnityEngine;

public enum WorldMode
{
    Reality,
    Dream
}

public class WorldModeManager : MonoBehaviour
{
    [Header("XR Origin")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform xrHead;

    [Header("Spawnpunkte")]
    [SerializeField] private Transform realitySpawn;
    [SerializeField] private Transform dreamSpawn;

    [Header("Transition")]
    [SerializeField] private XRScreenFade screenFade;
    [Tooltip("Zum Beispiel XR-Locomotion-Provider, die während des Wechsels pausieren sollen.")]
    [SerializeField] private Behaviour[] behavioursToDisableDuringTransition = System.Array.Empty<Behaviour>();

    private bool isTransitioning;

    public WorldMode CurrentMode { get; private set; } = WorldMode.Reality;
    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (xrHead == null && Camera.main != null)
            xrHead = Camera.main.transform;
    }

    private void Start()
    {
        CurrentMode = WorldMode.Reality;
        if (ValidateReferences())
            MoveXROriginToSpawn(realitySpawn);
    }

    [ContextMenu("Test/Enter Dream")]
    public void EnterDream()
    {
        BeginTransition(WorldMode.Dream);
    }

    [ContextMenu("Test/Wake Up")]
    public void WakeUp()
    {
        BeginTransition(WorldMode.Reality);
    }

    private void BeginTransition(WorldMode targetMode)
    {
        if (isTransitioning || targetMode == CurrentMode)
            return;

        if (!ValidateReferences())
            return;

        Transform targetSpawn = targetMode == WorldMode.Dream ? dreamSpawn : realitySpawn;
        if (targetSpawn == null)
        {
            Debug.LogError($"No spawn Transform is assigned for {targetMode}.", this);
            return;
        }

        StartCoroutine(TransitionTo(targetMode, targetSpawn));
    }

    private IEnumerator TransitionTo(WorldMode targetMode, Transform targetSpawn)
    {
        isTransitioning = true;
        Behaviour[] transitionBehaviours = behavioursToDisableDuringTransition ?? System.Array.Empty<Behaviour>();
        bool[] previouslyEnabled = new bool[transitionBehaviours.Length];

        try
        {
            for (int i = 0; i < transitionBehaviours.Length; i++)
            {
                Behaviour behaviour = transitionBehaviours[i];
                if (behaviour == null)
                    continue;

                previouslyEnabled[i] = behaviour.enabled;
                behaviour.enabled = false;
            }

            yield return screenFade.FadeToBlack();
            MoveXROriginToSpawn(targetSpawn);
            CurrentMode = targetMode;
            yield return screenFade.FadeFromBlack();
        }
        finally
        {
            for (int i = 0; i < transitionBehaviours.Length; i++)
            {
                Behaviour behaviour = transitionBehaviours[i];
                if (behaviour != null)
                    behaviour.enabled = previouslyEnabled[i];
            }

            isTransitioning = false;
        }
    }

    private void MoveXROriginToSpawn(Transform spawn)
    {
        Vector3 currentHeadPosition = xrHead.position;
        float headHeightOffset = currentHeadPosition.y - xrOrigin.position.y;

        Vector3 currentForward = Vector3.ProjectOnPlane(xrHead.forward, Vector3.up);
        Vector3 desiredForward = Vector3.ProjectOnPlane(spawn.forward, Vector3.up);
        if (currentForward.sqrMagnitude > Mathf.Epsilon && desiredForward.sqrMagnitude > Mathf.Epsilon)
        {
            float yawDelta = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);
            xrOrigin.RotateAround(currentHeadPosition, Vector3.up, yawDelta);
        }

        Vector3 desiredHeadPosition = spawn.position + Vector3.up * headHeightOffset;
        xrOrigin.position += desiredHeadPosition - xrHead.position;
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        if (xrOrigin == null)
        {
            Debug.LogError("XR Origin Transform is not assigned.", this);
            valid = false;
        }

        if (xrHead == null)
        {
            Debug.LogError("XR Main Camera / Head Transform is not assigned.", this);
            valid = false;
        }

        if (realitySpawn == null)
        {
            Debug.LogError("Reality Spawn Transform is not assigned.", this);
            valid = false;
        }

        if (dreamSpawn == null)
        {
            Debug.LogError("Dream Spawn Transform is not assigned.", this);
            valid = false;
        }

        if (screenFade == null || !screenFade.IsReady)
        {
            Debug.LogError("XR Screen Fade is not assigned or its camera canvas is not ready.", this);
            valid = false;
        }

        return valid;
    }
}
