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
        CachePlayerRoot();
        SetInteractionVisible(false);
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
        SetInteractionVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
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
        if (playerColliders.Count == 0)
            return;

        if (worldModeManager == null)
        {
            Debug.LogError("WorldModeManager is not assigned on the bed trigger.", this);
            return;
        }

        worldModeManager.EnterDream();
    }

    private void UpdatePlayerPresence(Collider other, bool isInside)
    {
        if (!IsPlayerCollider(other))
            return;

        if (isInside)
            playerColliders.Add(other);
        else
            playerColliders.Remove(other);

        SetInteractionVisible(playerColliders.Count > 0);
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

    private void SetInteractionVisible(bool visible)
    {
        if (interactionVisual != null && interactionVisual.activeSelf != visible)
            interactionVisual.SetActive(visible);
    }
}
