using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

[System.Serializable]
public class DialogueLine
{
    [TextArea(2, 4)]
    public string subtitleText;
    public AudioClip voiceLine;
    public float duration = 3f;
}

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class AudioArtefact : MonoBehaviour
{
    [Header("Audio & Dialogues")]
    public AudioSource artefactAudioSource;
    public AudioSource ambientAudioSource;
    public List<DialogueLine> dialogueSequence = new List<DialogueLine>();

    [Header("VR Subtitle UI")]
    public GameObject subtitleCanvasObject;
    public TextMeshProUGUI subtitleTextUI;
    public float subtitleDistance = 1.5f;
    public float subtitleHeightOffset = -0.2f;

    /*[Header("Effects")]
    public GameObject vignetteEffect;
    public GameObject blurEffect;
    public GameObject fogEffect;*/

    [Header("Movement Settings")]
    public float slowedMoveSpeed = 0.2f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Transform mainCameraTransform;
    private bool isTriggered = false;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
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
        if (subtitleCanvasObject != null && subtitleCanvasObject.activeSelf && mainCameraTransform != null)
        {
            Vector3 targetPosition = mainCameraTransform.position + (mainCameraTransform.forward * subtitleDistance);
            targetPosition.y += subtitleHeightOffset;
            
            subtitleCanvasObject.transform.position = Vector3.Lerp(subtitleCanvasObject.transform.position, targetPosition, Time.deltaTime * 5f);
            subtitleCanvasObject.transform.rotation = Quaternion.LookRotation(subtitleCanvasObject.transform.position - mainCameraTransform.position);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isTriggered) return;
        isTriggered = true;

        StartCoroutine(PlayFlashback());
    }

    private IEnumerator PlayFlashback()
    {
        ToggleEffects(true);
        SetMoveSpeed(slowedMoveSpeed);

        if (ambientAudioSource != null)
        {
            ambientAudioSource.Play();
        }

        foreach (DialogueLine line in dialogueSequence)
        {
            ShowSubtitle(line.subtitleText);

            if (line.voiceLine != null && artefactAudioSource != null)
            {
                artefactAudioSource.PlayOneShot(line.voiceLine);
                float waitTime = Mathf.Max(line.voiceLine.length, line.duration);
                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                yield return new WaitForSeconds(line.duration);
            }

            HideSubtitle();
        }

        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }

        ToggleEffects(false);
        SetMoveSpeed(1.0f);
    }

    private void ToggleEffects(bool state)
    {
        /*if (vignetteEffect != null) vignetteEffect.SetActive(state);
        if (blurEffect != null) blurEffect.SetActive(state);
        if (fogEffect != null) fogEffect.SetActive(state);*/
    }

    private void SetMoveSpeed(float speedMultiplier)
    {
        Debug.Log("Player Move Speed Multiplier: " + speedMultiplier);
    }

    private void ShowSubtitle(string text)
    {
        if (subtitleTextUI != null && subtitleCanvasObject != null)
        {
            subtitleTextUI.text = text;
            
            if (mainCameraTransform != null)
            {
                Vector3 startPos = mainCameraTransform.position + (mainCameraTransform.forward * subtitleDistance);
                startPos.y += subtitleHeightOffset;
                subtitleCanvasObject.transform.position = startPos;
            }

            subtitleCanvasObject.SetActive(true);
        }
    }

    private void HideSubtitle()
    {
        if (subtitleCanvasObject != null)
        {
            subtitleCanvasObject.SetActive(false);
        }
    }
}