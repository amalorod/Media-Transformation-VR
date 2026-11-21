using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class XRScreenFade : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField, Min(0.05f)] private float overlayDistance = 0.3f;

    private CanvasGroup canvasGroup;
    private RectTransform canvasRect;

    public bool IsReady => targetCamera != null && canvasGroup != null;

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogError("XR Screen Fade requires the XR Main Camera.", this);
            return;
        }

        CreateCameraFadeCanvas();
    }

    private void LateUpdate()
    {
        if (targetCamera == null || canvasRect == null)
            return;

        float height = 2f * overlayDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
        float width = height * Mathf.Max(targetCamera.aspect, 2f);
        canvasRect.sizeDelta = new Vector2(width, height);
    }

    public IEnumerator FadeToBlack()
    {
        yield return FadeTo(1f);
    }

    public IEnumerator FadeFromBlack()
    {
        yield return FadeTo(0f);
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("XR Screen Fade canvas was not initialized.", this);
            yield break;
        }

        float startingAlpha = canvasGroup.alpha;
        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startingAlpha, targetAlpha, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void CreateCameraFadeCanvas()
    {
        GameObject canvasObject = new GameObject("XR Screen Fade Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(targetCamera.transform, false);
        canvasObject.transform.localPosition = Vector3.forward * overlayDistance;
        canvasObject.transform.localRotation = Quaternion.identity;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = targetCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;

        GameObject imageObject = new GameObject("Black Fade", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }
}
