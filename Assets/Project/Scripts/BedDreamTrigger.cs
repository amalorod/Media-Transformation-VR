using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class BedDreamTrigger : MonoBehaviour
{
    [SerializeField] private WorldModeManager worldModeManager;
    [SerializeField] private InputActionReference sleepAction;
    [SerializeField] private GameObject interactionVisual;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private Transform playerRoot;
    private bool enabledSleepActionHere;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (worldModeManager == null)
            worldModeManager = FindAnyObjectByType<WorldModeManager>();
        if (worldModeManager == null)
            Debug.LogError("No active WorldModeManager was found in the scene for BedDreamTrigger.", this);

        CachePlayerRoot();
        if (interactionVisual != null && !interactionVisual.activeSelf)
            interactionVisual.SetActive(true);
    }

    private void OnEnable()
    {
        if (sleepAction == null || sleepAction.action == null)
        {
            Debug.LogError("Sleep Input Action is not assigned on the bed trigger.", this);
            return;
        }

        enabledSleepActionHere = !sleepAction.action.enabled;
        if (enabledSleepActionHere)
            sleepAction.action.Enable();

        sleepAction.action.performed += OnSleepPerformed;
    }

    private void OnDisable()
    {
        if (sleepAction != null && sleepAction.action != null)
        {
            sleepAction.action.performed -= OnSleepPerformed;
            if (enabledSleepActionHere)
                sleepAction.action.Disable();
        }

        enabledSleepActionHere = false;
        playerColliders.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[BedDreamTrigger] ENTER: {other.name}", this);
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

    private void OnSleepPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("[BedDreamTrigger] ACTION PERFORMED", this);
        if (playerColliders.Count == 0)
        {
            Debug.Log("[BedDreamTrigger] ACTION IGNORED - PLAYER NOT INSIDE", this);
            return;
        }

        if (worldModeManager == null)
            return;

        Debug.Log("[BedDreamTrigger] ENTER DREAM", this);
        worldModeManager.EnterDream();
    }

    private void UpdatePlayerPresence(Collider other, bool isInside)
    {
        if (!IsPlayerCollider(other))
            return;

        if (isInside)
        {
            if (playerColliders.Add(other))
                Debug.Log("[BedDreamTrigger] PLAYER INSIDE", this);
        }
        else if (playerColliders.Remove(other))
        {
            Debug.Log("[BedDreamTrigger] PLAYER EXIT", this);
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
