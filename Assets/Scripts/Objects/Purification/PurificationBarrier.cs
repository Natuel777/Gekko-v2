using System;
using System.Collections;
using UnityEngine;

// Barrera del desafío de purificación (escudo de la planta o bloqueo del camino). Solo se puede bajar,
// nunca vuelve a subir. Los colliders deben ser sólidos (no trigger) y SIN IDamageable: así la lengua
// rebota contra la barrera y los escarabajos la esquivan/se aturden (capa Obstacle).
public class PurificationBarrier : MonoBehaviour
{
    [Header("Dissolve")]
    [SerializeField] private Renderer[] _renderers;
    [Tooltip("Propiedad expuesta del shader (Swirl.shadergraph usa _DissolveAmount).")]
    [SerializeField] private string _dissolveProperty = "_DissolveAmount";
    [Tooltip("Valor al estar levantada. Ajustar según hacia dónde disuelva el shader.")]
    [SerializeField] private float _dissolveFrom = 0f;
    [Tooltip("Valor al estar caída.")]
    [SerializeField] private float _dissolveTo = 1f;
    [SerializeField] private float _dropDuration = 1f;

    [Header("Feedback (optional)")]
    [SerializeField] private ParticleSystem _dropParticle;
    [SerializeField] private AudioSource _dropSound;

    private Collider[] _colliders;
    private MaterialPropertyBlock _block;
    private int _dissolveId;
    private bool _dropped;

    public bool IsUp => !_dropped;
    public event Action<PurificationBarrier> Dropped;

    private void Awake()
    {
        EnsureInit();
        SetDissolve(_dissolveFrom);
    }

    // Si la barrera arranca desactivada Awake no corre: se inicializa recién al bajarla.
    private void EnsureInit()
    {
        if(_block != null) return;

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

        Dropped?.Invoke(this);
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
