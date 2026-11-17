using UnityEngine;

public class EnergyBurstAnimation : MonoBehaviour
{
    [SerializeField] private BasicAnimation basicAnimation;

    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float rotationMultiplier = 4f;

    [SerializeField] private float positionShake = 0.08f;
    [SerializeField] private float rotationShake = 8f;

    [SerializeField] private float liftHeight = 0.5f;

    private float timer;
    private bool isPlaying;

    private Vector3 originalRotationSpeed;
    private bool originalRotating;


    void Update()
    {
        if (!isPlaying)
            return;

        timer += Time.deltaTime;

        float progress = Mathf.Clamp01(timer / duration);

        float intensity = Mathf.Sin(progress * Mathf.PI);

        Vector3 shake = Random.insideUnitSphere * positionShake * intensity;

        Vector3 lift = Vector3.up * liftHeight * intensity;

        basicAnimation.AddPositionOffset(shake + lift);

        Vector3 rotationShakeOffset = Random.insideUnitSphere * rotationShake * intensity;

        basicAnimation.AddRotationOffset(rotationShakeOffset);

        if (timer >= duration)
        {
            FinishBurst();
        }
    }

    public void Play()
    {
        if (basicAnimation == null)
            return;


        if (!isPlaying)
        {
            originalRotationSpeed = basicAnimation.RotationSpeed;
            originalRotating = basicAnimation.IsRotating;
        }

        timer = 0f;
        isPlaying = true;

        basicAnimation.IsRotating = true;
        basicAnimation.RotationSpeed = originalRotationSpeed * rotationMultiplier;
    }

    private void FinishBurst()
    {
        basicAnimation.RotationSpeed = originalRotationSpeed;
        basicAnimation.IsRotating = originalRotating;

        isPlaying = false;
    }
}
