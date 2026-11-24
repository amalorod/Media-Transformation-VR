using System.Collections;
using UnityEngine;

/// <summary>
/// Dreht das gesamte Puppenhaus-Board (inklusive geneigtem Pivot, GridAnchor und Mini-Räumen)
/// in festen Schritten um die vertikale Achse durch die Boardmitte.
/// Die Neigung liegt in einem Kind-Pivot und wird hier nicht verändert.
/// </summary>
public class BoardRotationController : MonoBehaviour
{
    [Tooltip("Wurzel der horizontalen Rotation. Liegt auf der Boardmitte, ohne eigene Neigung.")]
    [SerializeField] private Transform rotationRoot;

    [SerializeField] private float stepDegrees = 45f;
    [SerializeField] private float duration = 0.3f;

    private bool isRotating;

    public bool IsRotating => isRotating;

    public void RotateLeft() => StartStep(-stepDegrees);

    public void RotateRight() => StartStep(stepDegrees);

    private void StartStep(float delta)
    {
        if (isRotating || rotationRoot == null)
            return;

        StartCoroutine(RotateRoutine(delta));
    }

    private IEnumerator RotateRoutine(float delta)
    {
        isRotating = true;

        Quaternion start = rotationRoot.rotation;
        Quaternion end = Quaternion.AngleAxis(delta, Vector3.up) * start;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rotationRoot.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        rotationRoot.rotation = end;
        isRotating = false;
    }
}
