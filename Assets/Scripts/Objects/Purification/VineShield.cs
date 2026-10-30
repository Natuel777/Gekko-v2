using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Escudo de enredaderas de la planta (prefab Escudo_Enredadera). Misma idea que PurificationBarrier.DissolveStep, pero
// en vez de disolver un shader las enredaderas se retraen: cada núcleo roto baja el Range.y de cada SplineExtrudeTapered
// un tramo (con 3 núcleos: 1 -> 2/3 -> 1/3 -> 0). En la última fase se apagan los colliders enseguida y, cuando terminan
// de retraerse, se oculta el escudo. Solo avanza: nunca vuelve a crecer.
public class VineShield : MonoBehaviour
{
    [Tooltip("Enredaderas que se retraen. Si se deja vacío, usa todas las de los hijos.")]
    [SerializeField] private SplineExtrudeTapered[] _vines;
    [Tooltip("Segundos que tarda cada fase en retraerse.")]
    [Min(0.01f)]
    [SerializeField] private float _stepDuration = 1.5f;

    [Header("Feedback (optional)")]
    [SerializeField] private ParticleSystem _dropParticle;

    private readonly List<float> _startY = new();
    private Collider[] _colliders;
    private bool _initialized, _dropped, _finished;

    // Retenido (ver Hold): las fases que llegan se guardan y recién se aplican en Release.
    private bool _held;
    private int _pendingStep, _pendingSteps;

    // Avance de 0 (enredaderas enteras) a 1 (retraídas del todo).
    private float _progress, _targetProgress;
    private float _speed;
    private Coroutine _routine;

    public bool IsUp => !_dropped;
    public event Action<VineShield> Dropped;

    private void Awake() => EnsureInit();

    // Si se desactivó a mitad de la retracción Unity cortó la corrutina: se retoma desde donde quedó.
    private void OnEnable()
    {
        if(_initialized && !_finished && !Mathf.Approximately(_progress, _targetProgress))
            _routine = StartCoroutine(RetractRoutine());
    }

    private void OnDisable()
    {
        if(_routine == null) return;

        StopCoroutine(_routine);
        _routine = null;
    }

    // Si el escudo arranca desactivado Awake no corre: se inicializa recién al bajarlo.
    private void EnsureInit()
    {
        if(_initialized) return;
        _initialized = true;

        if(_vines == null || _vines.Length == 0) _vines = GetComponentsInChildren<SplineExtrudeTapered>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        foreach(SplineExtrudeTapered vine in _vines)
            _startY.Add(vine != null ? vine.Range.y : 0f);
    }

    // Retrae el escudo hasta la fase step de steps: 0 = entero, steps = retraído del todo (en ese caso cae). Mismo
    // contrato que PurificationBarrier.DissolveStep: si la fase anterior sigue en curso, sigue de corrido a la misma
    // velocidad, y repetir una fase o pedir una anterior no hace nada.
    public void DissolveStep(int step, int steps)
    {
        if(_dropped) return;

        EnsureInit();

        steps = Mathf.Max(1, steps);
        float target = Mathf.Clamp01((float)step / steps);

        if(_held)
        {
            if(_pendingSteps == 0 || target > (float)_pendingStep / _pendingSteps)
            {
                _pendingStep = step;
                _pendingSteps = steps;
            }
            return;
        }

        if(target <= _targetProgress) return;

        _targetProgress = target;
        _speed = (1f / steps) / _stepDuration;

        if(target >= 1f) BeginDrop();

        // Desactivado no puede correr la corrutina: la fase se aplica de golpe.
        if(!gameObject.activeInHierarchy)
        {
            _progress = _targetProgress;
            ApplyProgress();
            if(_dropped) FinishDrop();
            return;
        }

        if(_routine == null) _routine = StartCoroutine(RetractRoutine());
    }

    // Retiene las fases que lleguen (DissolveStep) hasta Release, sin cortar la que ya se está retrayendo. Lo usa
    // BossPlantPhases para que las enredaderas bajen recién cuando la cámara ya está mostrando la planta.
    public void Hold() => _held = true;

    public void Release()
    {
        if(!_held) return;
        _held = false;

        if(_pendingSteps == 0) return;

        int step = _pendingStep, steps = _pendingSteps;
        _pendingSteps = 0;
        DissolveStep(step, steps);
    }

    // El collider se apaga al iniciar la caída para que la lengua y Gekko puedan pasar enseguida.
    private void BeginDrop()
    {
        _dropped = true;

        foreach(Collider c in _colliders)
            if(c != null) c.enabled = false;

        if(_dropParticle != null && gameObject.activeInHierarchy) _dropParticle.Play();
    }

    private IEnumerator RetractRoutine()
    {
        while(!Mathf.Approximately(_progress, _targetProgress))
        {
            _progress = Mathf.MoveTowards(_progress, _targetProgress, _speed * Time.deltaTime);
            ApplyProgress();
            yield return null;
        }

        _routine = null;

        if(_dropped) FinishDrop();
    }

    // Solo se apagan las enredaderas: el GameObject sigue activo para no cortar partículas.
    private void FinishDrop()
    {
        if(_finished) return;
        _finished = true;

        foreach(SplineExtrudeTapered vine in _vines)
            if(vine != null) vine.gameObject.SetActive(false);

        Dropped?.Invoke(this);
    }

    private void ApplyProgress()
    {
        for(int i = 0; i < _vines.Length; i++)
        {
            SplineExtrudeTapered vine = _vines[i];
            if(vine == null) continue;

            // Nunca por debajo del inicio del rango: con x == y la malla queda vacía.
            Vector2 range = vine.Range;
            range.y = Mathf.Max(range.x, _startY[i] * (1f - _progress));
            vine.Range = range;
            vine.Rebuild();
        }
    }
}
