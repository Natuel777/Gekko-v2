using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enredaderas decorativas que se disuelven con un desafío de la planta (PurificationChallenge, MirrorChallenge...): cuando
// cae el escudo (ej. "Enredaderas" en BossLvlOne) o cuando se purifica la planta (ej. las del árbol de la Second Zone en
// LvlTwo). Va en el objeto padre: toma los renderers de todos sus hijos que tengan la propiedad de dissolve, sube el valor
// de a poco y al terminar los oculta y avisa con Dissolved. Solo baja, nunca vuelve a aparecer.
public class ChallengeVinesDissolve : MonoBehaviour
{
    public enum DissolveTrigger
    {
        ShieldDropped,  // Cae el escudo de la planta (todos los núcleos rotos / todas las torretas purificadas).
        PlantPurified,  // Se purifica la planta: el desafío se completa.
        AfterPurifiedCinematic // Termina la cinemática de la planta purificada (para mostrarlas con su propia cámara).
    }

    [Tooltip("Desafío que dispara la disolución.")]
    [SerializeField] private Challenge _challenge;
    [Tooltip("Cuándo se disuelven: al caer el escudo de la planta o al purificarla.")]
    [SerializeField] private DissolveTrigger _trigger = DissolveTrigger.ShieldDropped;

    [Header("Dissolve")]
    [Tooltip("Propiedad expuesta del shader (Thorns.mat disuelve con _Dissolve: 0.03 entera -> 1 disuelta).")]
    [SerializeField] private string _dissolveProperty = "_Dissolve";
    [Tooltip("Valor con las enredaderas enteras.")]
    [SerializeField] private float _dissolveFrom = 0.03f;
    [Tooltip("Valor con las enredaderas disueltas.")]
    [SerializeField] private float _dissolveTo = 1f;
    [Tooltip("Segundos de espera desde que se dispara (cae el escudo o se purifica la planta) hasta que empiezan a disolverse.")]
    [Min(0f)]
    [SerializeField] private float _delay = 0f;
    [Tooltip("Segundos que tarda en disolverse de punta a punta.")]
    [Min(0.01f)]
    [SerializeField] private float _duration = 2f;

    private readonly List<Renderer> _renderers = new();
    private readonly List<Material> _materials = new();
    private Collider[] _colliders;
    private int _dissolveId;
    private bool _initialized, _dissolving, _finished;
    private float _progress;
    private Coroutine _routine;

    // Ya terminaron de disolverse y están ocultas.
    public bool IsDissolved => _finished;
    public event Action Dissolved;

    private void Awake()
    {
        EnsureInit();
        ApplyProgress();
    }

    private void OnEnable()
    {
        if(_challenge == null)
        {
            Debug.LogWarning($"[{name}] ChallengeVinesDissolve sin Challenge asignado: las enredaderas no se van a disolver.", this);
            return;
        }

        switch(_trigger)
        {
            case DissolveTrigger.ShieldDropped: _challenge.ShieldDropped += OnTriggered; break;
            case DissolveTrigger.PlantPurified: _challenge.Completed += OnTriggered; break;
            default: _challenge.CompletionBeatFinished += OnTriggered; break;
        }

        // Si ya había pasado (o se reactiva a mitad de la disolución), se retoma desde donde quedó.
        if(AlreadyTriggered() || (_dissolving && !_finished))
            StartDissolve();
    }

    private void OnDisable()
    {
        if(_challenge != null)
        {
            _challenge.ShieldDropped -= OnTriggered;
            _challenge.Completed -= OnTriggered;
            _challenge.CompletionBeatFinished -= OnTriggered;
        }

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

    private void EnsureInit()
    {
        if(_initialized) return;
        _initialized = true;

        _dissolveId = Shader.PropertyToID(_dissolveProperty);
        _colliders = GetComponentsInChildren<Collider>(true);

        // r.materials da una instancia propia de cada renderer: Thorns.mat es un asset compartido y escribir en él
        // disolvería todas las enredaderas del juego. No se usa MaterialPropertyBlock porque el GPU Resident Drawer
        // (activo en PC_RPAsset) no es compatible con property blocks, igual que en PurificationBarrier.
        foreach(Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if(!HasDissolve(r)) continue;

            _renderers.Add(r);

            foreach(Material material in r.materials)
                if(material != null && material.HasProperty(_dissolveId)) _materials.Add(material);
        }

        if(_materials.Count == 0)
            Debug.LogWarning($"[{name}] Ningún renderer hijo tiene la propiedad '{_dissolveProperty}': las enredaderas no se van a disolver.", this);
    }

    private bool HasDissolve(Renderer r)
    {
        foreach(Material material in r.sharedMaterials)
            if(material != null && material.HasProperty(_dissolveId)) return true;

        return false;
    }

    // El escudo cae antes de purificar la planta: con ShieldDropped alcanza con que el desafío ya no esté activo.
    private bool AlreadyTriggered() => _trigger switch
    {
        DissolveTrigger.ShieldDropped => _challenge.State != Challenge.ChallengeState.Active,
        DissolveTrigger.PlantPurified => _challenge.State == Challenge.ChallengeState.Completed,
        _ => _challenge.CompletionBeatDone
    };

    private void OnTriggered() => StartDissolve();

    private void StartDissolve()
    {
        if(_finished || _routine != null) return;

        EnsureInit();

        // Si ya había arrancado, el delay ya se cumplió: no se vuelve a esperar al retomar.
        float delay = _dissolving ? 0f : _delay;
        _dissolving = true;

        // Las enredaderas ya no bloquean nada mientras se disuelven.
        foreach(Collider c in _colliders)
            if(c != null) c.enabled = false;

        if(!gameObject.activeInHierarchy)
        {
            _progress = 1f;
            Finish();
            return;
        }

        _routine = StartCoroutine(DissolveRoutine(delay));
    }

    private IEnumerator DissolveRoutine(float delay)
    {
        if(delay > 0f) yield return new WaitForSeconds(delay);

        float speed = 1f / Mathf.Max(_duration, 0.01f);

        while(_progress < 1f)
        {
            _progress = Mathf.MoveTowards(_progress, 1f, speed * Time.deltaTime);
            ApplyProgress();
            yield return null;
        }

        _routine = null;
        Finish();
    }

    private void Finish()
    {
        if(_finished) return;
        _finished = true;

        ApplyProgress();

        // Solo se ocultan los renderers: los GameObjects siguen activos.
        foreach(Renderer r in _renderers)
            if(r != null) r.enabled = false;

        Dissolved?.Invoke();
    }

    private void ApplyProgress()
    {
        float value = Mathf.Lerp(_dissolveFrom, _dissolveTo, _progress);

        foreach(Material material in _materials)
            if(material != null) material.SetFloat(_dissolveId, value);
    }
}
