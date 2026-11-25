using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Small, single-puppet socket for the miniature office.
/// </summary>
[DisallowMultipleComponent]
public sealed class DollhousePuppetSocket : MonoBehaviour
{
    [SerializeField] private Transform acceptedPuppet;
    [SerializeField, Range(0.05f, 0.5f)] private float targetScaleFactor = 0.25f;
    [SerializeField, Min(0.01f)] private float snapRadius = 0.25f;
    [SerializeField] private Renderer markerRenderer;
    [SerializeField] private Color idleColor = new Color(1f, 0.68f, 0.16f, 1f);
    [SerializeField] private Color validColor = new Color(0.18f, 0.82f, 0.30f, 1f);

    private XRGrabInteractable puppetGrab;
    private Rigidbody puppetRigidbody;
    private Transform originalParent;
    private Vector3 originalWorldScale;
    private bool originalIsKinematic;
    private bool originalUseGravity;
    private bool originalGrabEnabled;
    private Vector3 originalLinearVelocity;
    private Vector3 originalAngularVelocity;
    private bool hasOriginalState;
    private bool held;
    private bool occupied;
    private MaterialPropertyBlock propertyBlock;

    public Transform AcceptedPuppet => acceptedPuppet;
    public float TargetScaleFactor => targetScaleFactor;
    public float SnapRadius => snapRadius;
    public bool Occupied => occupied;

    private void Awake()
    {
        if (acceptedPuppet == null || acceptedPuppet.name != "Torvald_Puppe")
        {
            Debug.LogError($"{name} must reference Torvald_Puppe.", this);
            enabled = false;
            return;
        }

        puppetGrab = acceptedPuppet.GetComponent<XRGrabInteractable>();
        puppetRigidbody = acceptedPuppet.GetComponent<Rigidbody>();
        if (puppetGrab == null || puppetRigidbody == null)
        {
            Debug.LogError($"{name} requires Torvald_Puppe XRGrabInteractable and Rigidbody references.", this);
            enabled = false;
            return;
        }

        originalParent = acceptedPuppet.parent;
        originalWorldScale = acceptedPuppet.lossyScale;
        originalIsKinematic = puppetRigidbody.isKinematic;
        originalUseGravity = puppetRigidbody.useGravity;
        originalGrabEnabled = puppetGrab.enabled;
        originalLinearVelocity = puppetRigidbody.linearVelocity;
        originalAngularVelocity = puppetRigidbody.angularVelocity;
        hasOriginalState = true;

        puppetGrab.selectEntered.AddListener(OnPuppetGrabbed);
        puppetGrab.selectExited.AddListener(OnPuppetReleased);
        if (markerRenderer != null)
            markerRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (puppetGrab == null)
            return;

        puppetGrab.selectEntered.RemoveListener(OnPuppetGrabbed);
        puppetGrab.selectExited.RemoveListener(OnPuppetReleased);
    }

    private void Update()
    {
        if (!occupied && puppetGrab != null && puppetGrab.isSelected)
            held = true;

        RefreshMarker();
    }

    private void OnPuppetGrabbed(SelectEnterEventArgs args)
    {
        if (!IsAcceptedPuppet())
            return;

        if (occupied)
            RestoreOriginalState();

        held = true;
        occupied = false;
        RefreshMarker();
    }

    private void OnPuppetReleased(SelectExitEventArgs args)
    {
        if (!IsAcceptedPuppet())
            return;

        held = false;
        if (Vector3.Distance(acceptedPuppet.position, transform.position) <= snapRadius)
        {
            PlacePuppet();
            return;
        }

        RestorePhysicsForFreePuppet();
        RefreshMarker();
    }

    private bool IsAcceptedPuppet()
    {
        return acceptedPuppet != null &&
               acceptedPuppet.name == "Torvald_Puppe" &&
               puppetGrab != null &&
               puppetRigidbody != null;
    }

    private void PlacePuppet()
    {
        if (!hasOriginalState || !IsAcceptedPuppet())
            return;

        puppetRigidbody.linearVelocity = Vector3.zero;
        puppetRigidbody.angularVelocity = Vector3.zero;
        puppetRigidbody.useGravity = false;
        puppetRigidbody.isKinematic = true;

        acceptedPuppet.SetParent(transform, false);
        acceptedPuppet.localPosition = Vector3.zero;
        acceptedPuppet.localRotation = Quaternion.identity;
        SetWorldScale(Vector3.Scale(originalWorldScale, Vector3.one * targetScaleFactor));

        occupied = true;
        held = false;
        RefreshMarker();
    }

    private void RestoreOriginalState()
    {
        if (!hasOriginalState || acceptedPuppet == null)
            return;

        acceptedPuppet.SetParent(originalParent, true);
        SetWorldScale(originalWorldScale);

        puppetRigidbody.isKinematic = originalIsKinematic;
        puppetRigidbody.useGravity = originalUseGravity;
        if (!originalIsKinematic)
        {
            puppetRigidbody.linearVelocity = originalLinearVelocity;
            puppetRigidbody.angularVelocity = originalAngularVelocity;
        }
        else
        {
            puppetRigidbody.linearVelocity = Vector3.zero;
            puppetRigidbody.angularVelocity = Vector3.zero;
        }

        puppetGrab.enabled = originalGrabEnabled;
    }

    private void RestorePhysicsForFreePuppet()
    {
        if (puppetRigidbody == null)
            return;

        puppetRigidbody.isKinematic = originalIsKinematic;
        puppetRigidbody.useGravity = originalUseGravity;
        if (!originalIsKinematic)
        {
            puppetRigidbody.linearVelocity = Vector3.zero;
            puppetRigidbody.angularVelocity = Vector3.zero;
        }
        puppetGrab.enabled = originalGrabEnabled;
    }

    private void SetWorldScale(Vector3 worldScale)
    {
        Vector3 parentScale = acceptedPuppet.parent != null
            ? acceptedPuppet.parent.lossyScale
            : Vector3.one;

        acceptedPuppet.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private void RefreshMarker()
    {
        if (markerRenderer == null)
            return;

        bool visible = held && !occupied;
        markerRenderer.enabled = visible;
        if (!visible)
            return;

        Color color = acceptedPuppet != null &&
                      Vector3.Distance(acceptedPuppet.position, transform.position) <= snapRadius
            ? validColor
            : idleColor;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", color);
        markerRenderer.SetPropertyBlock(propertyBlock);
    }
}
