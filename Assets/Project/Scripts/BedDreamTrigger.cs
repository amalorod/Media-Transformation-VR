using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bett-Trigger für den Traumwechsel. Der frühere Y-Eingabepfad (InputAction "Sleep") wurde entfernt,
/// damit die Y-Taste ausschließlich die Türsteuerung bedient. Die Dream-Architektur (WorldModeManager.EnterDream)
/// bleibt erhalten und wird später über Tag1_EndInteractable beziehungsweise Trigger/Select ausgelöst.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BedDreamTrigger : MonoBehaviour
{
    [SerializeField] private WorldModeManager worldModeManager;
    [SerializeField] private GameObject interactionVisual;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    private Transform playerRoot;

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

    private void OnDisable()
    {
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

    /// <summary>Löst den Traumwechsel aus. Wird nicht mehr per Y aufgerufen, sondern später über Interact/Trigger.</summary>
    public void EnterDream()
    {
        if (playerColliders.Count == 0 || worldModeManager == null)
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
