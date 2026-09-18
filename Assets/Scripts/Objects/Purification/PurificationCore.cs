using System;
using UnityEngine;

// Núcleo del desafío de purificación. Va en el MISMO GameObject que su collider: la lengua hace
// hit.transform.GetComponent<IDamageable>(). No se destruye al romperse (la lengua todavía puede
// tener la referencia hasta terminar de retraerse); se apaga collider y visual.
[RequireComponent(typeof(Collider))]
public class PurificationCore : MonoBehaviour, IDamageable, IHitOncePerLick
{
    [Header("Config")]
    [SerializeField] private int _hitsToBreak = 1;

    [Header("Feedback (todo opcional)")]
    [SerializeField] private ParticleSystem _hitParticle;
    [Tooltip("No debe ser hijo de 'Visual': ese objeto se desactiva al romperse.")]
    [SerializeField] private ParticleSystem _breakParticle;
    [Tooltip("No debe ser hijo de 'Visual': ese objeto se desactiva al romperse.")]
    [SerializeField] private AudioSource _breakSound;
    [Tooltip("Malla/objeto del núcleo que se oculta al romperse.")]
    [SerializeField] private GameObject _visual;

    [Header("Arte de venas / enredaderas (opcional)")]
    [SerializeField] private GameObject[] _activeWhileIntact;
    [SerializeField] private GameObject[] _activeWhenBroken;

    private Collider _collider;
    private int _hits;

    public bool IsBroken { get; private set; }
    public event Action<PurificationCore> Broken;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        SetActiveAll(_activeWhileIntact, true);
        SetActiveAll(_activeWhenBroken, false);
    }

    public void Damage(float dmg)
    {
        if(IsBroken) return;

        _hits++;

        if(_hits < _hitsToBreak)
        {
            if(_hitParticle != null) _hitParticle.Play();
            return;
        }

        Break();
    }

    private void Break()
    {
        IsBroken = true;
        if(_collider != null) _collider.enabled = false;

        if(_visual != null) _visual.SetActive(false);
        if(_breakParticle != null) _breakParticle.Play();
        if(_breakSound != null) _breakSound.Play();

        SetActiveAll(_activeWhileIntact, false);
        SetActiveAll(_activeWhenBroken, true);

        Broken?.Invoke(this);
    }

    private static void SetActiveAll(GameObject[] objects, bool active)
    {
        if(objects == null) return;

        foreach(GameObject go in objects)
            if(go != null) go.SetActive(active);
    }
}
