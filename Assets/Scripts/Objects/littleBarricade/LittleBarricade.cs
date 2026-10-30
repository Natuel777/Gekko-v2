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

    [Header("Core Focus (primero el núcleo, después la enredadera)")]
    [Tooltip("Al romperse, primero se enfoca el núcleo purificándose y recién después la Reveal Camera muestra la enredadera disolviéndose. Apagado = como antes: una sola cámara y todo se disuelve junto.")]
    [SerializeField] private bool _focusCoreFirst;
    [Tooltip("Cámara que mira al núcleo. Vacío = se crea una por código, del lado del player, mirando al núcleo.")]
    [SerializeField] private CinemachineCamera _coreCamera;
    [Min(0f)]
    [SerializeField] private float _coreCamSeconds = 2f;
    [Tooltip("Solo para la cámara creada por código: distancia horizontal al núcleo, hacia el lado del player.")]
    [SerializeField] private float _coreCamDistance = 4f;
    [Tooltip("Solo para la cámara creada por código: altura sobre el núcleo.")]
    [SerializeField] private float _coreCamHeight = 1.5f;
    [Tooltip("Segundos desde que arranca la Reveal Camera hasta que la enredadera empieza a disolverse (dar tiempo al blend).")]
    [Min(0f)]
    [SerializeField] private float _vineDissolveDelay = 0.8f;

    private CinemachineCamera _runtimeCoreCam;
    private int _coreCamBasePriority;

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
            if(_focusCoreFirst)
            {
                StartCoroutine(CoreThenVineRoutine());
            }
            else
            {
                StartCoroutine(Disapear());
                StartCoroutine(RevealCameraRoutine());
            }
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
    // Primero la cámara del núcleo mientras se disuelve, después la Reveal Camera y recién ahí se disuelve la
    // enredadera, para que se vea qué se abrió. Sin Reveal Camera la enredadera igual se disuelve, sin cámara.
    private IEnumerator CoreThenVineRoutine()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);
        EventManager.Trigger<float>("OnCameraShake", _revealShakeForce);

        StartCoroutine(DissolveRoutine(new[] { _head.material }));
        BeginCoreCam();
        yield return new WaitForSeconds(_coreCamSeconds);

        if(_revealCamera != null) _revealCamera.Priority = 30;
        EndCoreCam();
        yield return new WaitForSeconds(_vineDissolveDelay);

        StartCoroutine(DissolveRoutine(GetVineMaterials(), () => _barricade.SetActive(false)));
        yield return new WaitForSeconds(Mathf.Max(0f, _revealSeconds - _vineDissolveDelay));

        if(_revealCamera != null) _revealCamera.Priority = 0;
        if(_runtimeCoreCam != null) Destroy(_runtimeCoreCam.gameObject);
    }

    private void BeginCoreCam()
    {
        CinemachineCamera cam = _coreCamera;

        if(cam == null)
        {
            // Se arma apagada para que no le gane la cámara de juego ni un frame antes de tener prioridad.
            GameObject go = new GameObject($"{name}_CoreCam");
            go.SetActive(false);
            cam = _runtimeCoreCam = go.AddComponent<CinemachineCamera>();
            if(_revealCamera != null) cam.Lens = _revealCamera.Lens;
            FrameCore(cam.transform);
            go.SetActive(true);
        }

        _coreCamBasePriority = cam.Priority.Value;
        cam.Priority = 30;
    }

    // La creada por código no se destruye acá: el CinemachineBrain todavía está haciendo el blend desde ella.
    private void EndCoreCam()
    {
        if(_coreCamera != null) _coreCamera.Priority = _coreCamBasePriority;
        if(_runtimeCoreCam != null) _runtimeCoreCam.Priority = -1;
    }

    // Del lado del player (para que se entienda qué núcleo se rompió), un poco más arriba, mirando al núcleo.
    // Misma idea que BossPlantPhases.FrameCore.
    private void FrameCore(Transform camTransform)
    {
        Vector3 core = _head.bounds.center;
        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;

        Vector3 toPlayer = pj != null ? pj.transform.position - core : -transform.forward;
        toPlayer.y = 0f;
        if(toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward;

        Vector3 camPos = core + toPlayer.normalized * _coreCamDistance + Vector3.up * _coreCamHeight;

        camTransform.position = camPos;
        camTransform.rotation = Quaternion.LookRotation(core - camPos, Vector3.up);
    }

    // Materiales de la enredadera sin el núcleo, que ya se está disolviendo por su cuenta.
    private Material[] GetVineMaterials()
    {
        var materialList = new System.Collections.Generic.List<Material>();

        foreach (var rend in _barricade.GetComponentsInChildren<MeshRenderer>())
        {
            if (rend == _head) continue;
            materialList.AddRange(rend.materials);
        }

        return materialList.ToArray();
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

        yield return DissolveRoutine(materials, () => _barricade.SetActive(false));
    }

    private IEnumerator DissolveRoutine(Material[] materials, System.Action onFinished = null)
    {
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

        onFinished?.Invoke();
    }
} 
