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

    [Header("Emission")]
    private Renderer targetRenderer;
    private Renderer baseRenderer;
    private int materialIndex;
    [SerializeField] private float maxEmissionMultiplier = 5f;
    [SerializeField]
    private AnimationCurve emissionReleaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float timer;
    private bool isPlaying;

    private Vector3 originalRotationSpeed;
    private bool originalRotating;
    private BurstPhase currentPhase;


    private Color originalEmissionColor;
    private Color originalBaseEmissionColor;
    private MaterialPropertyBlock propertyBlock;
    private bool hasEmission;
    private bool hasBaseEmission;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
        Transform baseTransform = transform.parent.Find("Base_lp");

        if (baseTransform != null)
            baseRenderer = baseTransform.GetComponent<Renderer>();
        materialIndex = 0;
    }

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

        UpdateEmission(Mathf.Lerp(1f, maxEmissionMultiplier, intensity));

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

        float emissionProgress = emissionReleaseCurve.Evaluate(progress);

        float emissionMultiplier = Mathf.Lerp(maxEmissionMultiplier, 1f, emissionProgress);

        UpdateEmission(emissionMultiplier);

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

        CacheEmission();

        basicAnimation.IsRotating = true;
        basicAnimation.RotationSpeed = originalRotationSpeed * rotationMultiplier;

        timer = 0f;
        currentPhase = BurstPhase.Charge;
        isPlaying = true;
    }

    public bool IsPlaying => isPlaying;

    private void CacheEmission()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer != null)
        {
            Material material = targetRenderer.sharedMaterials[materialIndex];

            if (material.HasProperty(EmissionColorID))
            {
                originalEmissionColor = material.GetColor(EmissionColorID);
                hasEmission = true;
            }
        }

        if (baseRenderer != null)
        {
            Material material = baseRenderer.sharedMaterials[materialIndex];

            if (material.HasProperty(EmissionColorID))
            {
                originalBaseEmissionColor = material.GetColor(EmissionColorID);
                hasBaseEmission = true;
            }
        }
    }

    private void UpdateEmission(float multiplier)
    {
        if (hasEmission)
        {
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

            propertyBlock.SetColor(
                EmissionColorID,
                originalEmissionColor * multiplier
            );

            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }

        if (hasBaseEmission)
        {
            baseRenderer.GetPropertyBlock(propertyBlock, materialIndex);

            propertyBlock.SetColor(
                EmissionColorID,
                originalBaseEmissionColor * multiplier
            );

            baseRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }

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
