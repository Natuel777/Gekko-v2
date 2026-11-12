using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enredaderas decorativas que se disuelven cuando cae el escudo de un PurificationChallenge (todos sus núcleos rotos).
// Va en el objeto padre (ej. "Enredaderas" en BossLvlOne): toma los renderers de todos sus hijos que tengan la propiedad
// de dissolve, sube el valor de a poco y al terminar los oculta. Solo baja, nunca vuelve a aparecer.
public class ChallengeVinesDissolve : MonoBehaviour
{
    [Tooltip("Desafío cuyo ShieldDropped (todos los núcleos rotos) dispara la disolución.")]
    [SerializeField] private PurificationChallenge _challenge;

    [Header("Dissolve")]
    [Tooltip("Propiedad expuesta del shader (Thorns.mat disuelve con _Dissolve: 0.03 entera -> 1 disuelta).")]
    [SerializeField] private string _dissolveProperty = "_Dissolve";
    [Tooltip("Valor con las enredaderas enteras.")]
    [SerializeField] private float _dissolveFrom = 0.03f;
    [Tooltip("Valor con las enredaderas disueltas.")]
    [SerializeField] private float _dissolveTo = 1f;
    [Tooltip("Segundos de espera desde que se rompe el último núcleo hasta que empiezan a disolverse.")]
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

    private void Awake()
    {
        EnsureInit();
        ApplyProgress();
    }

    private void OnEnable()
    {
        if(_challenge == null)
        {
            Debug.LogWarning($"[{name}] ChallengeVinesDissolve sin PurificationChallenge asignado: las enredaderas no se van a disolver.", this);
            return;
        }

        _challenge.ShieldDropped += OnShieldDropped;

        // Si el escudo ya había caído (o se reactiva a mitad de la disolución), se retoma desde donde quedó.
        if(_challenge.State != PurificationChallenge.ChallengeState.Active || (_dissolving && !_finished))
            StartDissolve();
    }

    private void OnDisable()
    {
        if(_challenge != null) _challenge.ShieldDropped -= OnShieldDropped;

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

    private void OnShieldDropped() => StartDissolve();

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
    }

    private void ApplyProgress()
    {
        float value = Mathf.Lerp(_dissolveFrom, _dissolveTo, _progress);

        foreach(Material material in _materials)
            if(material != null) material.SetFloat(_dissolveId, value);
    }
}
