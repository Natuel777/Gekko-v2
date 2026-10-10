using System.Collections;
using Unity.Cinemachine;
using UnityEngine;


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

    public ParticleSystem Indicator => _indicator;

    public bool CanBeTargeted => _interacted;

    public void Damage(float dmg)
    {
        _life -= dmg;

        if(_life <=0)
        {
            _particlePurification.Play();
            GetComponent<Collider>().enabled = false;
            _head.enabled = false;
            StartCoroutine(Disapear());
            StartCoroutine(RevealCameraRoutine());
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
        _interacted = true;
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);
        MeshRenderer[] renderers = _barricade.GetComponentsInChildren<MeshRenderer>();

        Material[] materials = new Material[0];
        var materialList = new System.Collections.Generic.List<Material>();

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials) // .materials (plural) instancia autom�ticamente
            {
                materialList.Add(mat);
            }
        }
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
