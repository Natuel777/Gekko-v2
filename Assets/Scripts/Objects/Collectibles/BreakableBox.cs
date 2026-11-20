using System.Collections;
using UnityEngine;

public class BreakableBox : MonoBehaviour, IDamageable
{
    private static readonly int EstaAbiertaHash = Animator.StringToHash("estaAbierta");

    [SerializeField] private float _life = 2;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Transform _spawn;
    [SerializeField] private int _minQuant;
    [SerializeField] private int _maxQuant;
    [SerializeField] private float _upForceMin = 4f;
    [SerializeField] private float _upForceMax = 7f;
    [SerializeField] private float _sideForceMin = 1.5f;
    [SerializeField] private float _sideForceMax = 3.5f;

    [Header("Apertura")]
    [Tooltip("Animator con el bool 'estaAbierta'. Vacío = se busca en los hijos (el de 'flower').")]
    [SerializeField] private Animator _animator;
    [Tooltip("Segundos desde que se activa 'estaAbierta' hasta que salen las frutas (para que salgan cuando la flor ya se abrió).")]
    [Min(0f)]
    [SerializeField] private float _fruitDelayOnOpen = 0.35f;
    [Tooltip("Segundos que la flor queda abierta antes de empezar a desaparecer.")]
    [Min(0f)]
    [SerializeField] private float _stayOpenSeconds = 2f;
    [Tooltip("Duración del encogimiento final antes de destruirse.")]
    [Min(0.01f)]
    [SerializeField] private float _shrinkDuration = 0.4f;

    [Header("Show: Squash & Wobble al golpe")]
    [Tooltip("Lo que se aplasta y tiembla. Vacío = el transform del Animator (flower).")]
    [SerializeField] private Transform _visual;
    [Tooltip("Cuánto se aplasta en Y (y se estira en XZ) en cada golpe.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float _squashAmount = 0.25f;
    [Min(0.01f)]
    [SerializeField] private float _squashDuration = 0.35f;
    [Tooltip("Grados de bamboleo en cada golpe.")]
    [SerializeField] private float _wobbleAngle = 12f;
    [SerializeField] private float _wobbleFrequency = 30f;

    [Header("Show: Frutas en cascada + partículas")]
    [Tooltip("Segundos entre fruta y fruta (pop, pop, pop). 0 = todas juntas.")]
    [Min(0f)]
    [SerializeField] private float _fruitInterval = 0.08f;
    [Tooltip("Mini 'pop' de escala de cada fruta al aparecer.")]
    [SerializeField] private bool _fruitPopScale = true;
    [Tooltip("Partículas en cada golpe (opcional). Se instancian en el spawn.")]
    [SerializeField] private ParticleSystem _hitParticles;
    [Tooltip("Partículas al abrirse (pétalos/polvo, opcional). Se instancian en el spawn.")]
    [SerializeField] private ParticleSystem _openParticles;
    [Tooltip("Partículas al desaparecer (opcional).")]
    [SerializeField] private ParticleSystem _vanishParticles;

    private bool _opened;
    private Vector3 _visualBaseScale;
    private Quaternion _visualBaseRotation;
    private Coroutine _hitFx;

    private void Awake()
    {
        if (!_animator) _animator = GetComponentInChildren<Animator>();
        if (!_visual) _visual = _animator ? _animator.transform : transform;
        _visualBaseScale = _visual.localScale;
        _visualBaseRotation = _visual.localRotation;
    }

    public void Damage(float dmg)
    {
        if (_opened) return;

        _life--;
        bool opensNow = _life <= 0;

        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlankPlaced);
        SpawnParticles(_hitParticles);

        if (_hitFx != null) StopCoroutine(_hitFx);
        _hitFx = StartCoroutine(SquashWobble());

        if (opensNow) StartCoroutine(OpenRoutine());
        else StartCoroutine(DropFruits());
    }

    private IEnumerator OpenRoutine()
    {
        _opened = true;
        if (_animator) _animator.SetBool(EstaAbiertaHash, true);
        SpawnParticles(_openParticles);

        if (_fruitDelayOnOpen > 0f) yield return new WaitForSeconds(_fruitDelayOnOpen);
        yield return DropFruits();

        yield return new WaitForSeconds(_stayOpenSeconds);
        yield return ShrinkAndDestroy();
    }

    private IEnumerator DropFruits()
    {
        yield return null;
        int quantity = Random.Range(_minQuant, _maxQuant + 1); // el máximo es exclusivo en int, por eso el +1

        for (int i = 0; i < quantity; i++)
        {
            GameObject fruit = InstantiatePrefab();
            Rigidbody rb = fruit.GetComponent<Rigidbody>();

            // Dirección horizontal aleatoria
            float angle = (360f / quantity) * i + Random.Range(-15f, 15f);
            Vector3 horizontal = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Hacia arriba + hacia un costado
            Vector3 force = Vector3.up * Random.Range(_upForceMin, _upForceMax)
                          + horizontal * Random.Range(_sideForceMin, _sideForceMax);

            if (rb)
            {
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse); // giro para que se vea natural
            }

            if (_fruitPopScale) StartCoroutine(PopScale(fruit.transform));
            if (_fruitInterval > 0f && i < quantity - 1) yield return new WaitForSeconds(_fruitInterval);
        }
    }

    // Aplasta, estira y rebota la escala mientras se bambolea, todo amortiguado.
    private IEnumerator SquashWobble()
    {
        float t = 0f;
        while (t < _squashDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / _squashDuration);
            float damper = 1f - n;

            // Oscilación amortiguada: arranca aplastado y rebota hasta volver a la escala original
            float osc = Mathf.Cos(n * Mathf.PI * 3f) * damper;
            float y = 1f - osc * _squashAmount;
            float xz = 1f + osc * _squashAmount * 0.6f;
            _visual.localScale = Vector3.Scale(_visualBaseScale, new Vector3(xz, y, xz));

            float wobble = Mathf.Sin(t * _wobbleFrequency) * _wobbleAngle * damper;
            _visual.localRotation = _visualBaseRotation * Quaternion.Euler(wobble, 0f, wobble * 0.5f);
            yield return null;
        }
        _visual.localScale = _visualBaseScale;
        _visual.localRotation = _visualBaseRotation;
        _hitFx = null;
    }

    private IEnumerator ShrinkAndDestroy()
    {
        if (_hitFx != null) { StopCoroutine(_hitFx); _hitFx = null; }
        _visual.localRotation = _visualBaseRotation;
        Vector3 start = _visualBaseScale;
        float t = 0f;
        while (t < _shrinkDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / _shrinkDuration);
            // EaseInBack: se infla un poquito y después se encoge a 0
            const float c = 1.70158f;
            float e = 1f - ((c + 1f) * n * n * n - c * n * n);
            _visual.localScale = start * e;
            yield return null;
        }
        SpawnParticles(_vanishParticles);
        Destroy(gameObject);
    }

    // Las frutas aparecen chiquitas y "popean" a su tamaño con overshoot.
    private IEnumerator PopScale(Transform target)
    {
        Vector3 baseScale = target.localScale;
        const float duration = 0.25f;
        float t = 0f;
        while (t < duration && target)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            // EaseOutBack
            const float c = 1.70158f;
            float e = 1f + (c + 1f) * Mathf.Pow(n - 1f, 3f) + c * Mathf.Pow(n - 1f, 2f);
            target.localScale = baseScale * Mathf.Max(0.01f, e);
            yield return null;
        }
        if (target) target.localScale = baseScale;
    }

    private void SpawnParticles(ParticleSystem ps)
    {
        if (!ps) return;
        Transform at = _spawn ? _spawn : transform;
        ParticleSystem instance = Instantiate(ps, at.position, at.rotation);
        instance.Play(true);
        var main = instance.main;
        Destroy(instance.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
    }

    private GameObject InstantiatePrefab()
    {
        return Instantiate(_prefab, _spawn.position, _spawn.rotation);
    }
}
