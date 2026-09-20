using System.Collections;
using UnityEngine;

public class GekkoFallMotion : MonoBehaviour
{
    [SerializeField] private float _fallDistance = 10f;
    [SerializeField] private float _fallDuration = 2f;

    private void OnEnable()
    {
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        Vector3 start = transform.localPosition;
        Vector3 end = start + Vector3.down * _fallDistance;
        float elapsed = 0f;

        while (elapsed < _fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _fallDuration));
            transform.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }

        transform.localPosition = end;
    }
}
