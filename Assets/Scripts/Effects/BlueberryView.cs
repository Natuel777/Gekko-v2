using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BlueberryView : CollectableView
{
    private Renderer rend;
    private float boostedRotationSpeed = 5;
    private float stretchAmount = 1.3f;
    private float stretchSpeed = 8f;
    private bool isStretching = false;
    private Vector3 targetScale;
    private float stretchDuration = 0.1f;
    private float squashAmount = 1.3f;
    private float timer = 0f;
    private bool isSquashing = false;
    private Vector3 squashTarget;
    private Vector3 originalScale;
    private Transform actualScale;
    private bool isCollapsing = false;
    private float collapseSpeed = 6f;
    private float whiteT = 0f;
    private bool isLighting = false;
    private float maxWhite = 0.8f;

    private float lightSpeed = 5f;
    private float squashTimer = 0f;
    private float squashDuration = 0.15f;

    public BlueberryView(Renderer render, Transform transformLocal, Collectible col)
    {
        rend = render;
        actualScale = transformLocal;
        originalScale = transformLocal.localScale;
        _collectible = col;
        _maxTimeCollected = 0.5f;
    }

    private IEnumerator Activate()
    {
        while (true)
        {
            if (_timerCollected > 0)
            {
                _timerCollected -= Time.deltaTime;
                if (_timerCollected <= 0)
                {
                    _collectible.ReturnToFactory();
                    _collectible.StopAllCoroutines();
                }
            }
            if (isStretching)
            {
                timer += Time.deltaTime;
                actualScale.localScale = Vector3.Lerp(actualScale.localScale, targetScale, Time.deltaTime * stretchSpeed);

                if (timer >= stretchDuration)
                {
                    isStretching = false;
                    isSquashing = true;

                    squashTimer = 0f;

                    Vector3 currentScale = actualScale.localScale;

                    squashTarget = new Vector3(originalScale.x * squashAmount, originalScale.y * 0.2f, originalScale.z * squashAmount);
                }
            }

            if (isSquashing)
            {
                squashTimer += Time.deltaTime;

                float t = squashTimer / squashDuration;
                t = Mathf.Clamp01(t);

                actualScale.localScale = Vector3.Lerp(actualScale.localScale, squashTarget, t);

                if (squashTimer >= squashDuration)
                {
                    isSquashing = false;
                    isCollapsing = true;
                }
            }


            if (isCollapsing)
            {
                Vector3 collapseTarget = new Vector3(0f, originalScale.y * 0.2f, 0f);
                actualScale.localScale = Vector3.Lerp(actualScale.localScale, collapseTarget, Time.deltaTime * collapseSpeed);

                if (actualScale.localScale.magnitude < 0.05f)
                {
                    //Destroy(gameObject);
                }
            }

            if (isLighting)
            {
                whiteT += Time.deltaTime * lightSpeed;
                float t = Mathf.Clamp01(whiteT);
                float finalWhite = t * maxWhite;

                rend.material.SetFloat("_WhiteAmount", finalWhite);
            }
            yield return new WaitForEndOfFrame();
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

        Vector3 currentScale = actualScale.localScale;
        targetScale = new Vector3(originalScale.x * 0.8f, originalScale.y * stretchAmount, originalScale.z * 0.8f);
        timer = 0f;
        _collectible.StartCoroutine(Activate());
    }
}
