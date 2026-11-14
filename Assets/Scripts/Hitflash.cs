using System.Collections;
using UnityEngine;

/// Attach this to the target object. Whenever anything touches it, the target turns red

public class HitFlash : MonoBehaviour
{
    [Header("Hit Settings")]
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitDuration = 1f;

    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Flash();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Flash();
    }

    private void Flash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SetColors(hitColor);
        yield return new WaitForSeconds(hitDuration);
        RestoreColors();
        flashRoutine = null;
    }

    private void SetColors(Color color)
    {
        foreach (Renderer r in renderers)
        {
            r.material.color = color;
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = originalColors[i];
        }
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            RestoreColors();
        }
    }
}
