using System;
using System.Collections;
using UnityEngine;

// Barrera independiente que cae al romperse TODOS los núcleos de purificación asignados. Misma técnica de
// caída que PurificationBarrier (dissolve vía MaterialPropertyBlock, solo baja, nunca vuelve a subir) pero
// sin depender de un PurificationChallenge: cuenta sus propios núcleos. Pensada para no modificar
// PurificationBarrier/PurificationChallenge ni el prefab "Barricade Parkour" ya existente (que usa
// LittleBarricade y cae con un golpe directo de lengua, no con núcleos).
public class CoreTriggeredBarrier : MonoBehaviour
{
    [Header("Cores")]
    [Tooltip("Núcleos de purificación que deben romperse todos para que esta barrera caiga.")]
    [SerializeField] private PurificationCore[] _cores;

    [Header("Barricade")]
    [Tooltip("Raíz cuyos renderers (y los de sus hijos) se disuelven al caer. Si se deja vacío, usa los de este mismo objeto.")]
    [SerializeField] private GameObject _barricadeRoot;
    [Tooltip("Propiedad expuesta del shader (M_Enredaderas/Nucleo usan _Dissolve).")]
    [SerializeField] private string _dissolveProperty = "_Dissolve";
    [Tooltip("Valor al estar levantada.")]
    [SerializeField] private float _dissolveFrom = 0f;
    [Tooltip("Valor al estar caída.")]
    [SerializeField] private float _dissolveTo = 1f;
    [SerializeField] private float _dropDuration = 1f;

    [Header("Feedback (all optional)")]
    [SerializeField] private ParticleSystem _dropParticle;
    [SerializeField] private AudioSource _dropSound;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private MaterialPropertyBlock _block;
    private int _dissolveId;
    private bool _dropped;
    private int _remaining;

    public bool IsUp => !_dropped;
    public event Action Dropped;

    private void Awake()
    {
        EnsureInit();
        SetDissolve(_dissolveFrom);

        if(_cores != null)
            foreach(PurificationCore core in _cores)
                if(core != null && !core.IsBroken) _remaining++;
    }

    private void OnEnable()
    {
        if(_cores == null) return;

        foreach(PurificationCore core in _cores)
            if(core != null) core.Broken += OnCoreBroken;

        // Si la barrera se reactiva y los núcleos ya estaban todos rotos, cae directo.
        if(_remaining == 0 && _cores.Length > 0) Drop();
    }

    private void OnDisable()
    {
        if(_cores == null) return;

        foreach(PurificationCore core in _cores)
            if(core != null) core.Broken -= OnCoreBroken;
    }

    private void OnCoreBroken(PurificationCore core)
    {
        if(_dropped) return;

        _remaining = Mathf.Max(0, _remaining - 1);

        if(_remaining == 0) Drop();
    }

    // Si la barrera arranca desactivada Awake no corre: se inicializa recién al bajarla.
    private void EnsureInit()
    {
        if(_block != null) return;

        GameObject root = _barricadeRoot != null ? _barricadeRoot : gameObject;
        _renderers = root.GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _block = new MaterialPropertyBlock();
        _dissolveId = Shader.PropertyToID(_dissolveProperty);
    }

    public void Drop()
    {
        if(_dropped) return;
        _dropped = true;

        EnsureInit();

        // El collider se apaga al iniciar la caída para que la lengua y Gekko puedan pasar enseguida.
        foreach(Collider c in _colliders)
            if(c != null) c.enabled = false;

        // Desactivada no puede correr la corrutina ni reproducir FX: pasa directo al estado final.
        if(!gameObject.activeInHierarchy)
        {
            FinishDrop();
            return;
        }

        if(_dropParticle != null) _dropParticle.Play();
        if(_dropSound != null) _dropSound.Play();

        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        float t = 0f;

        while(t < _dropDuration)
        {
            t += Time.deltaTime;
            SetDissolve(Mathf.Lerp(_dissolveFrom, _dissolveTo, t / _dropDuration));
            yield return null;
        }

        FinishDrop();
    }

    private void FinishDrop()
    {
        SetDissolve(_dissolveTo);

        // Solo se ocultan los renderers: el GameObject sigue activo para no cortar partículas/sonido.
        if(_renderers != null)
            foreach(Renderer r in _renderers)
                if(r != null) r.enabled = false;

        Dropped?.Invoke();
    }

    private void SetDissolve(float value)
    {
        if(_renderers == null) return;

        foreach(Renderer r in _renderers)
        {
            if(r == null) continue;

            r.GetPropertyBlock(_block);
            _block.SetFloat(_dissolveId, value);
            r.SetPropertyBlock(_block);
        }
    }
}
