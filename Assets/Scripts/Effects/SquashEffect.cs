using UnityEngine;
using System.Collections;

public class SquashEffect
{
    [SerializeField] private float squashAmount = 0.8f;
    [SerializeField] private float squashDuration = 0.1f;

    [SerializeField] private float sideStretchAmount = 1.2f;
    [SerializeField] private float sideStretchDuration = 0.1f;

    [SerializeField] private float sideSquashAmount = 0.8f;
    [SerializeField] private float sideSquashDuration = 0.04f;

    [SerializeField] private float returnDuration = 0.15f;

    [SerializeField] private Renderer mushroomRenderer;
    [SerializeField] private float emissionIntensity = 1.5f;
    [SerializeField] private float emissionUpDuration = 0.1f;
    [SerializeField] private float emissionDownDuration = 0.15f;

    private Material material;
    private Color originalEmission;

    private Vector3 originalScale;
    private Transform _transform;
    private MonoBehaviour _mushroom;

    public SquashEffect(Renderer rend, Transform trans, MonoBehaviour mush) 
    {
        mushroomRenderer = rend;
        _transform = trans;
        _mushroom = mush;
        originalScale = _transform.localScale;
        material = mushroomRenderer.material;
        originalEmission = material.GetColor("_EmissionColor");
    }
    public void Squash()
    {
        _mushroom.StartCoroutine(SquashCoroutine());
    }

    private IEnumerator SquashCoroutine()
    {
        _mushroom.StartCoroutine(ChangeEmission(emissionIntensity, emissionUpDuration));
        Vector3 squashScale = new Vector3(originalScale.x,originalScale.y * squashAmount,originalScale.z);

        Vector3 stretchScale = new Vector3(originalScale.x * sideStretchAmount,originalScale.y * squashAmount,originalScale.z * sideStretchAmount);

        Vector3 sideSquashScale = new Vector3(originalScale.x * sideSquashAmount,originalScale.y,originalScale.z * sideSquashAmount);


        yield return ScaleTo(originalScale,squashScale,squashDuration);

        yield return ScaleTo(squashScale,stretchScale,sideStretchDuration);

        yield return ScaleTo(stretchScale,sideSquashScale,returnDuration);

        yield return ScaleTo(sideSquashScale,originalScale,sideSquashDuration);

        _transform.localScale = originalScale;
        _mushroom.StartCoroutine(ChangeEmission(0f, emissionDownDuration));
    }

    private IEnumerator ScaleTo(Vector3 startScale,Vector3 targetScale,float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;

            _transform.localScale = Vector3.Lerp(startScale,targetScale,t);

            yield return null;
        }

        _transform.localScale = targetScale;
    }

    private IEnumerator ChangeEmission(float targetIntensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;

            float currentIntensity = Mathf.Lerp(0f,targetIntensity,t);

            material.SetColor("_EmissionColor",originalEmission * Mathf.Pow(2f, currentIntensity));

            yield return null;
        }
    }
}