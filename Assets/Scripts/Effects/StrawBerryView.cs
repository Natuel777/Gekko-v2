using UnityEngine;
using UnityEngine.InputSystem;

public class StrawBerryView : CollectableView
{
    private Transform _transform;
    public float amplitude = 1f;
    public float speed = 2f;
    public float shrinkSpeed = 3f;

    public bool isMoving;
    public bool isShrinking;

    float startY;

    public GameObject mainStar;
    public GameObject starParticles;

    public StrawBerryView(Transform transform, StrawBerry sb) 
    {
        _transform = transform;
        _collectible = sb;
        startY = transform.position.y;
        isMoving = true;
        isShrinking = false;
        _maxTimeCollected = 2;
    }

    public void VirtualUpdate()
    {
        if(isMoving)
        {
            float offset = Mathf.Sin(Time.time * speed) * amplitude;

            Vector3 pos = _transform.position;
            pos.y = startY + offset;
            _transform.position = pos;
        }

        if (isShrinking)
        {
            _transform.localScale = Vector3.Lerp(
                _transform.localScale,
                Vector3.zero,
                Time.deltaTime * shrinkSpeed
            );
            if (_transform.localScale.magnitude < 0.1f)
            {
                //Destroy(gameObject);
            }
        }

        

        if (_timerCollected >0)
        {
            _timerCollected -= Time.deltaTime;
            if (_timerCollected <= 0) _collectible.ReturnToFactory();
        }
    }

    public override void Collect()
    {
        _timerCollected = _maxTimeCollected;
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerSlurp);
        isMoving = false;
        isShrinking = true;
        StrawBerry a = _collectible as StrawBerry;
        a.Particles();
    }
}
