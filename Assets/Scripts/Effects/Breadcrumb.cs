using UnityEngine;
using UnityEngine.InputSystem;

public class Breadcrumb : CollectableView
{
    public Renderer rend;
    public float boostedRotationSpeed;
    public float stretchAmount = 1.3f;
    public float stretchSpeed = 8f;
    bool isStretching = false;
    Vector3 targetScale;
    public float stretchDuration = 0.1f;
    public float squashAmount = 1.3f;
    float timer = 0f;
    bool isSquashing = false;
    Vector3 squashTarget;
    Vector3 originalScale;
    bool isCollapsing = false;
    public float collapseSpeed = 6f;
    float whiteT = 0f;
    bool isLighting = false;
    public float maxWhite = 0.8f;

    public float lightSpeed = 5f;
    float squashTimer = 0f;
    public float squashDuration = 0.15f;


    private void Awake()
    {
        rend = GetComponent<Renderer>();
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (_timerCollected > 0)
        {
            _timerCollected -= Time.deltaTime;
            if (_timerCollected <= 0) _collectible.ReturnToFactory();
        }

        if (isStretching)
        {
            timer += Time.deltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale,targetScale,Time.deltaTime * stretchSpeed);

            if (timer >= stretchDuration)
            {
                isStretching = false;
                isSquashing = true;

                squashTimer = 0f;

                Vector3 currentScale = transform.localScale;

                squashTarget = new Vector3(originalScale.x * squashAmount, originalScale.y * 0.2f, originalScale.z * squashAmount);
            }
        }

        if (isSquashing)
        {
            squashTimer += Time.deltaTime;

            float t = squashTimer / squashDuration;
            t = Mathf.Clamp01(t);

            transform.localScale = Vector3.Lerp(transform.localScale,squashTarget,t);

            if (squashTimer >= squashDuration)
            {
                isSquashing = false;
                isCollapsing = true;
            }
        }


        if (isCollapsing)
        {
            Vector3 collapseTarget = new Vector3(0f,originalScale.y * 0.2f,0f);
            transform.localScale = Vector3.Lerp(transform.localScale, collapseTarget,Time.deltaTime * collapseSpeed);

            if (transform.localScale.magnitude < 0.05f)
            {
                Destroy(gameObject);
            }
        }

        if (isLighting)
        {
            whiteT += Time.deltaTime * lightSpeed;
            float t = Mathf.Clamp01(whiteT);
            float finalWhite = t * maxWhite;

            rend.material.SetFloat("_WhiteAmount", finalWhite);
        }
    }
    public override void Collect()
    {
        _timerCollected = _maxTimeCollected;
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerSlurp);
        float current = rend.material.GetFloat("_RotationSpeed");
        rend.material.SetFloat("_RotationSpeed", current * boostedRotationSpeed);
        isStretching = true;

        isLighting = true;
        whiteT = 0f;

        Vector3 currentScale = transform.localScale;
        targetScale = new Vector3(originalScale.x * 0.8f, originalScale.y * stretchAmount, originalScale.z * 0.8f);
        timer = 0f;
    }
}
