using UnityEngine;

public class EnergyBurstAnimation : MonoBehaviour
{
    [SerializeField] private BasicAnimation basicAnimation;

    [Header("Charge Phase")]
    [SerializeField] private float chargeDuration = 0.8f;
    [SerializeField] private float rotationMultiplier = 4f;
    [SerializeField] private float positionShake = 0.08f;
    [SerializeField] private float rotationShake = 8f;
    [SerializeField]
    private AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Release Phase")]
    [SerializeField] private float releaseDuration = 0.6f;
    [SerializeField] private float liftHeight = 0.8f;

    private float timer;
    private bool isPlaying;

    private Vector3 originalRotationSpeed;
    private bool originalRotating;
    private BurstPhase currentPhase;

    void Update()
    {
        if (!isPlaying)
            return;

        timer += Time.deltaTime;

        if (currentPhase == BurstPhase.Charge)
        {
            Charge();
        }
        else if (currentPhase == BurstPhase.Release)
        {
            Release();
        }
    }

    private void Charge()
    {
        float progress = Mathf.Clamp01(timer / chargeDuration);

        float intensity = chargeCurve.Evaluate(progress);

        basicAnimation.RotationSpeed = originalRotationSpeed * Mathf.Lerp(1f, rotationMultiplier, intensity);

        Vector3 shake = Random.insideUnitSphere * positionShake * intensity;

        Vector3 rotationShakeOffset = Random.insideUnitSphere * rotationShake * intensity;

        basicAnimation.AddPositionOffset(shake);
        basicAnimation.AddRotationOffset(rotationShakeOffset);

        if (timer >= chargeDuration)
        {
            currentPhase = BurstPhase.Release;
            timer = 0f;

            basicAnimation.RotationSpeed = originalRotationSpeed;
            basicAnimation.IsRotating = originalRotating;
        }
    }

    private void Release()
    {
        float progress = Mathf.Clamp01(timer / releaseDuration);

        float liftProgress = Mathf.Sin(progress * Mathf.PI);

        float lift = liftHeight * liftProgress;

        basicAnimation.AddPositionOffset(Vector3.up * lift);

        if (timer >= releaseDuration)
        {
            FinishBurst();
        }
    }

    public void Play()
    {
        if (basicAnimation == null)
            return;

        if (isPlaying)
            return;

        originalRotationSpeed = basicAnimation.RotationSpeed;
        originalRotating = basicAnimation.IsRotating;

        basicAnimation.IsRotating = true;
        basicAnimation.RotationSpeed =
            originalRotationSpeed * rotationMultiplier;

        timer = 0f;
        currentPhase = BurstPhase.Charge;
        isPlaying = true;
    }

    // Para que quien la dispare sepa cuándo terminó (la torreta espera a que acabe la carga para volver a disparar).
    public bool IsPlaying => isPlaying;

    // Corta la animación donde esté y deja la rotación del cristal como estaba antes de Play. La torreta la usa si sale del
    // estado de carga antes de tiempo (por ejemplo, al purificarla). Sin efecto si no está corriendo.
    public void Stop()
    {
        if (!isPlaying)
            return;

        FinishBurst();
    }

    private void FinishBurst()
    {
        basicAnimation.RotationSpeed = originalRotationSpeed;
        basicAnimation.IsRotating = originalRotating;

        isPlaying = false;
    }

    private enum BurstPhase
    {
        Charge,
        Release
    }
}
