using UnityEngine;
using UnityEngine.InputSystem;

public class HandController : MonoBehaviour
{
    public InputActionReference gripInput;
    public InputActionReference triggerInput;
    public InputActionReference middleFingerInput;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (gripInput != null) gripInput.action.Enable();
        if (triggerInput != null) triggerInput.action.Enable();
        if (middleFingerInput != null) middleFingerInput.action.Enable();
    }

    private void Update()
    {
        if (!animator) return;

        if (gripInput != null)
            animator.SetFloat("Grip", gripInput.action.ReadValue<float>());

        if (triggerInput != null)
            animator.SetFloat("Trigger", triggerInput.action.ReadValue<float>());

        if (middleFingerInput != null)
            animator.SetBool("MiddleFinger", middleFingerInput.action.IsPressed());
    }
}