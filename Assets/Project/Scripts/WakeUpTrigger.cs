using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class WakeUpTrigger : MonoBehaviour
{
    [SerializeField] private WorldModeManager worldModeManager;
    [SerializeField] private InputActionReference wakeUpAction;
    [SerializeField] private GameObject interactionVisual;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private Transform playerRoot;
    private bool enabledWakeUpActionHere;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (worldModeManager == null)
            worldModeManager = FindAnyObjectByType<WorldModeManager>();
        if (worldModeManager == null)
            Debug.LogError("No active WorldModeManager was found in the scene for WakeUpTrigger.", this);

        CachePlayerRoot();
        if (interactionVisual != null && !interactionVisual.activeSelf)
            interactionVisual.SetActive(true);
    }

    private void OnEnable()
    {
        if (wakeUpAction == null || wakeUpAction.action == null)
        {
            Debug.LogError("Wake Up Input Action is not assigned on the wake-up trigger.", this);
            return;
        }

        enabledWakeUpActionHere = !wakeUpAction.action.enabled;
        if (enabledWakeUpActionHere)
            wakeUpAction.action.Enable();

        wakeUpAction.action.performed += OnWakeUpPerformed;
    }

    private void OnDisable()
    {
        if (wakeUpAction != null && wakeUpAction.action != null)
        {
            wakeUpAction.action.performed -= OnWakeUpPerformed;
            if (enabledWakeUpActionHere)
                wakeUpAction.action.Disable();
        }

        enabledWakeUpActionHere = false;
        playerColliders.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[WakeUpTrigger] ENTER: {other.name}", this);
        UpdatePlayerPresence(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        UpdatePlayerPresence(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        UpdatePlayerPresence(other, false);
    }

    private void OnWakeUpPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("[WakeUpTrigger] ACTION PERFORMED", this);
        if (playerColliders.Count == 0)
        {
            Debug.Log("[WakeUpTrigger] ACTION IGNORED - PLAYER NOT INSIDE", this);
            return;
        }

        if (worldModeManager == null)
            return;

        Debug.Log("[WakeUpTrigger] WAKE UP", this);
        worldModeManager.WakeUp();
    }

    private void UpdatePlayerPresence(Collider other, bool isInside)
    {
        if (!IsPlayerCollider(other))
            return;

        if (isInside)
        {
            if (playerColliders.Add(other))
                Debug.Log("[WakeUpTrigger] PLAYER INSIDE", this);
        }
        else if (playerColliders.Remove(other))
        {
            Debug.Log("[WakeUpTrigger] PLAYER EXIT", this);
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (playerRoot == null)
            CachePlayerRoot();

        return playerRoot != null && (other.transform == playerRoot || other.transform.IsChildOf(playerRoot));
    }

    private void CachePlayerRoot()
    {
        Camera playerCamera = Camera.main;
        if (playerCamera != null)
            playerRoot = playerCamera.transform.root;
    }

}
