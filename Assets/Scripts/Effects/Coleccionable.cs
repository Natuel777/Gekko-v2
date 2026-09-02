using UnityEngine;
using UnityEngine.InputSystem;

public class Coleccionable : CollectableView
{
    public float amplitude = 0.5f;
    public float speed = 2f;
    public float shrinkSpeed = 3f;

    public bool isMoving;
    public bool isShrinking;

    float startY;

    public GameObject mainStar;
    public GameObject starParticles;

    private void Awake()
    {
        startY = transform.position.y;
        isMoving = true;
        isShrinking = false;
    }

    void Update()
    {
        if(isMoving)
        {
            float offset = Mathf.Sin(Time.time * speed) * amplitude;

            Vector3 pos = transform.position;
            pos.y = startY + offset;
            transform.position = pos;
        }

        if (isShrinking)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                Vector3.zero,
                Time.deltaTime * shrinkSpeed
            );
            if (transform.localScale.magnitude < 0.1f)
            {
                Destroy(gameObject);
            }
        }

        

        if (_timerCollected >0)
        {
            _timerCollected -= Time.deltaTime;
            if (_timerCollected <= 0) _collectible.ReturnToFactory();
        }
    }

    public void Collected()
    {
        
    }

    public override void Collect()
    {
        _timerCollected = _maxTimeCollected;
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerSlurp);
        isMoving = false;
        isShrinking = true;

        GameObject mainStar_ps = Instantiate(mainStar, transform.position, Quaternion.identity);
        GameObject starParticles_ps = Instantiate(starParticles, transform.position, Quaternion.identity);

        Destroy(mainStar_ps, 2f);
        Destroy(starParticles_ps, 2f);
    }
}
