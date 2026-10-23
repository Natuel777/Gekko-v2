using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Barrera que se disuelve de a poco y nunca vuelve a levantarse. Hay dos formas de bajarla:
//  - Drop(): se disuelve entera de una vez (ej. la barrera del camino al purificar la planta).
//  - DissolveStep(step, steps): se disuelve por fases. El escudo de la planta usa una fase por núcleo: cada núcleo roto lo
//    disuelve un tramo más y, al llegar a la última fase, cae del todo.
// En los dos casos la disolución es progresiva: el valor del shader viaja de a poco hasta el objetivo y no salta.
public class PurificationBarrier : MonoBehaviour
{
    [Header("Dissolve")]
    [SerializeField] private Renderer[] _renderers;
    [Tooltip("Propiedad expuesta del shader (Shield.shadergraph y Swirl.shadergraph usan _DissolveAmount).")]
    [SerializeField] private string _dissolveProperty = "_DissolveAmount";
    [Tooltip("Valor al estar levantada. Ajustar según hacia dónde disuelva el shader.")]
    [SerializeField] private float _dissolveFrom = 0f;
    [Tooltip("Valor al estar caída.")]
    [SerializeField] private float _dissolveTo = 1f;
    [Tooltip("Segundos que tarda en disolverse de punta a punta cuando cae de una vez (Drop).")]
    [SerializeField] private float _dropDuration = 1f;
    [Tooltip("Segundos que tarda cada fase cuando se disuelve por fases (DissolveStep). Con 3 núcleos son 3 fases, así que el escudo entero tarda 3 veces esto.")]
    [Min(0.01f)]
    [SerializeField] private float _stepDuration = 1.5f;

    [Header("Feedback (optional)")]
    [SerializeField] private ParticleSystem _dropParticle;

    private Collider[] _colliders;
    private readonly List<Material> _materials = new();
    private int _dissolveId;
    private bool _initialized;

    // _dropped: ya se mandó a caer (no vuelve atrás). _finished: terminó de disolverse y ya se ocultó.
    private bool _dropped, _finished;

    // Avance de la disolución, de 0 (entera) a 1 (disuelta del todo). No es el valor del shader: ese sale de interpolar
    // _dissolveFrom -> _dissolveTo con este avance (ver ApplyProgress).
    private float _progress, _targetProgress;
    private float _speed;
    private Coroutine _routine;

    public bool IsUp => !_dropped;
    public event Action<PurificationBarrier> Dropped;

    private void Awake()
    {
        EnsureInit();
        ApplyProgress();
    }

    // Si se desactivó a mitad de la disolución Unity cortó la corrutina: se retoma desde donde quedó.
    private void OnEnable()
    {
        if(_initialized && !_finished && !Mathf.Approximately(_progress, _targetProgress))
            _routine = StartCoroutine(DissolveRoutine());
    }

    private void OnDisable()
    {
        if(_routine == null) return;

        StopCoroutine(_routine);
        _routine = null;
    }

    // Las instancias creadas por r.materials no las libera Unity solas hasta cambiar de escena.
    private void OnDestroy()
    {
        foreach(Material material in _materials)
            if(material != null) Destroy(material);
    }

    // Si la barrera arranca desactivada Awake no corre: se inicializa recién al bajarla.
    private void EnsureInit()
    {
        if(_initialized) return;
        _initialized = true;

        _colliders = GetComponentsInChildren<Collider>(true);
        _dissolveId = Shader.PropertyToID(_dissolveProperty);

        if(_renderers == null) return;

        // r.materials da una instancia propia de cada renderer: el material es un asset compartido, y escribir en él
        // disolvería a todos los que lo usan. No se usa MaterialPropertyBlock porque el GPU Resident Drawer (activo en
        // PC_RPAsset) no es compatible con property blocks: igual que en PurificationCore y PurificationChallenge.
        foreach(Renderer r in _renderers)
        {
            if(r == null || !HasDissolve(r)) continue;

            foreach(Material material in r.materials)
                if(material != null && material.HasProperty(_dissolveId)) _materials.Add(material);
        }

        // Sin la propiedad el SetFloat no hace nada y la barrera solo "desaparecería" de golpe al final: se avisa.
        if(_materials.Count == 0)
            Debug.LogWarning($"[{name}] Ningún material de los renderers tiene la propiedad '{_dissolveProperty}': la barrera no se va a ver disolver, solo desaparece al final.", this);
    }

    private bool HasDissolve(Renderer r)
    {
        foreach(Material material in r.sharedMaterials)
            if(material != null && material.HasProperty(_dissolveId)) return true;

        return false;
    }

    // Disuelve la barrera hasta la fase step de steps: 0 = entera, steps = disuelta del todo (en ese caso cae: ver
    // BeginDrop). Cada fase tarda _stepDuration, sin importar cuántas haya. Si todavía se está disolviendo la fase
    // anterior, sigue de corrido hacia la nueva a la misma velocidad (no se frena ni arranca de nuevo).
    // Solo avanza: repetir una fase o pedir una anterior no hace nada.
    public void DissolveStep(int step, int steps)
    {
        if(_dropped) return;

        steps = Mathf.Max(1, steps);
        float target = Mathf.Clamp01((float)step / steps);

        if(target <= _targetProgress) return;

        // Velocidad en avance (0..1) por segundo: cada fase mide 1/steps del recorrido.
        float speed = (1f / steps) / Mathf.Max(_stepDuration, 0.01f);

        if(target >= 1f) BeginDrop(speed);
        else DissolveTo(target, speed);
    }

    // Cae entera de una vez, en _dropDuration segundos de punta a punta (si ya estaba parcialmente disuelta, solo
    // recorre lo que le falta a la misma velocidad).
    public void Drop()
    {
        if(_dropped) return;

        BeginDrop(1f / Mathf.Max(_dropDuration, 0.01f));
    }

    // Arranca la caída final: desde acá la barrera ya no se puede detener ni levantar. Termina (se oculta y avisa con
    // Dropped) cuando el avance llega a 1.
    private void BeginDrop(float speed)
    {
        _dropped = true;

        EnsureInit();

        // El collider se apaga al iniciar la caída para que la lengua y Gekko puedan pasar enseguida.
        foreach(Collider c in _colliders)
            if(c != null) c.enabled = false;

        // Desactivada no puede correr la corrutina ni reproducir FX: pasa directo al estado final.
        if(!gameObject.activeInHierarchy)
        {
            _progress = _targetProgress = 1f;
            FinishDrop();
            return;
        }

        if(_dropParticle != null) _dropParticle.Play();
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlankPlaced);

        DissolveTo(1f, speed);
    }

    // Mueve el avance, de a poco, hacia target. Si ya hay una disolución en curso solo le cambia el objetivo y la velocidad.
    private void DissolveTo(float target, float speed)
    {
        EnsureInit();

        _targetProgress = target;
        _speed = speed;

        // Desactivada no puede correr la corrutina: la fase se aplica de golpe.
        if(!gameObject.activeInHierarchy)
        {
            _progress = target;
            ApplyProgress();
            return;
        }

        if(_routine == null) _routine = StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        while(!Mathf.Approximately(_progress, _targetProgress))
        {
            _progress = Mathf.MoveTowards(_progress, _targetProgress, _speed * Time.deltaTime);
            ApplyProgress();
            yield return null;
        }

        _routine = null;

        // Que el avance llegue a un tramo intermedio no termina nada: solo la caída final oculta la barrera.
        if(_dropped) FinishDrop();
    }

    private void FinishDrop()
    {
        if(_finished) return;
        _finished = true;

        ApplyProgress();

        // Solo se ocultan los renderers: el GameObject sigue activo para no cortar partículas/sonido.
        if(_renderers != null)
            foreach(Renderer r in _renderers)
                if(r != null) r.enabled = false;

        Dropped?.Invoke(this);
    }

    // Escribe el valor del shader que corresponde al avance actual (_dissolveFrom con avance 0, _dissolveTo con avance 1).
    private void ApplyProgress()
    {
        float value = Mathf.Lerp(_dissolveFrom, _dissolveTo, _progress);

        foreach(Material material in _materials)
            if(material != null) material.SetFloat(_dissolveId, value);
    }
}
