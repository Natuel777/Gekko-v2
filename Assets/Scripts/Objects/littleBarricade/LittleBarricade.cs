using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class LittleBarricade : MonoBehaviour, IDamageable, IParticleSystemTarget
{
    [SerializeField] private float _life = 1;
    [SerializeField] private MeshRenderer _head;
    [SerializeField] private GameObject _barricade;
    [SerializeField] private float _fadeDuration = 3f;
    [SerializeField] private ParticleSystem _particlePurification;
    [SerializeField] private ParticleSystem _indicator;
    private bool _interacted = false;

    [Header("Reveal Camera")]
    [SerializeField] private CinemachineCamera _revealCamera;
    [SerializeField] private float _revealSeconds = 3f;
    [SerializeField] private float _revealShakeForce = 1.5f;

    [Header("Line (on break, optional)")]
    [Tooltip("LineControllers que dibujan las líneas. Al romperse la barricada se les sacan las posiciones de abajo y se redibujan.")]
    [SerializeField] private LineController[] _lineControllers;
    [Tooltip("Posiciones de la línea que pertenecen a esta barricada, una por cada LineController (mismo orden). Al romperse se sacan de la lista de ese LineController.")]
    [SerializeField] private Transform[] _linePositionsGroups;

    [Header("Touch Hazard")]
    [Tooltip("Empuje al Gekko si choca contra la barricada mientras sigue en pie.")]
    [SerializeField] private float _touchPushForce = 3.5f;
    [Tooltip("Daño al Gekko si choca contra la barricada mientras sigue en pie.")]
    [SerializeField] private float _touchDamage = 5f;

    public ParticleSystem Indicator => _indicator;

    public bool CanBeTargeted => !_interacted;

    [Tooltip("Se dispara una sola vez al romperse (ej. para avisarle a un PurifyRegion que se purifique).")]
    public UnityEvent OnPurified = new();

    [SerializeField] private GameObject[] CorruptionParticles;
    public void Damage(float dmg)
    {
        _life -= dmg;

        if(_life <=0)
        {
            _indicator.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _indicator = null;
            _particlePurification.Play();
            GetComponent<Collider>().enabled = false;
            StartCoroutine(Disapear());
            StartCoroutine(RevealCameraRoutine());
            RemoveLinePositions();
            _interacted = true;
            OnPurified.Invoke();
        }
    }

    // Empuje + daño leve al chocar de frente contra la barricada en pie, para que no sea gratis caminar contra
    // ella en vez de romperla con la lengua. Misma convención que CarnivorousPlant.ApplyBiteHit.
    private void OnCollisionEnter(Collision collision)
    {
        if(_interacted) return;
        if(!collision.gameObject.TryGetComponent(out Player player)) return;

        if(player.TryGetComponent(out Rigidbody rb))
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            rb.AddForce(dir * _touchPushForce, ForceMode.Impulse);
        }

        EventManager.Trigger<float>("OnPlayerDamaged", _touchDamage);
    }

    private void RemoveLinePositions()
    {
        int count = Mathf.Min(_lineControllers.Length, _linePositionsGroups.Length);

        for(int i = 0; i < count; i++)
        {
            if(_lineControllers[i] != null && _linePositionsGroups[i] != null)
                _lineControllers[i].RemovePositions(_linePositionsGroups[i]);
        }
        foreach (var item in CorruptionParticles)
        {
            item.SetActive(false);
        }
    }

    // Misma convención que PurificationChallenge.CompletionBeat: sube la prioridad para que el
    // CinemachineBrain haga el blend solo, y la baja pasado el tiempo para volver a la cámara normal.
    private IEnumerator RevealCameraRoutine()
    {
        if(_revealCamera == null) yield break;

        _revealCamera.Priority = 30;
        EventManager.Trigger<float>("OnCameraShake", _revealShakeForce);
        yield return new WaitForSeconds(_revealSeconds);
        _revealCamera.Priority = 0;
    }
    private IEnumerator Disapear()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);
        MeshRenderer[] renderers = _barricade.GetComponentsInChildren<MeshRenderer>();


        Material[] materials = new Material[0];
        var materialList = new System.Collections.Generic.List<Material>();

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                materialList.Add(mat);
            }
        }
        materialList.Add(_head.material);
        materials = materialList.ToArray();

        float dissolve = 0f;

        while (dissolve < 1f)
        {
            dissolve = Mathf.MoveTowards(dissolve, 1f, _fadeDuration * Time.deltaTime);

            foreach (var mat in materials)
            {
                if (mat.HasProperty("_Dissolve"))
                {
                    mat.SetFloat("_Dissolve", dissolve);
                }
            }

            yield return null;
        }
        
        _barricade.SetActive(false);
    }
} 
