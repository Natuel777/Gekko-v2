using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Núcleo del desafío de purificación. Va en el MISMO GameObject que su collider: la lengua hace
// hit.transform.GetComponent<IDamageable>(). No se destruye al romperse (la lengua todavía puede
// tener la referencia hasta terminar de retraerse); se apaga el collider y el visual, o el visual pasa
// al material purificado si hay uno asignado.
// Al romperse, los renderers del núcleo cuyo material tenga la propiedad _Dissolve (M_Enredaderas, Nucleo) se
// disuelven de 0 a 1 a _dissolveSpeed por segundo. El visual recién pasa a su estado final (purificado / oculto)
// cuando el disolve termina; si ningún material tiene _Dissolve, cambia al instante como siempre.
[RequireComponent(typeof(Collider))]
public class PurificationCore : MonoBehaviour, IDamageable, IHitOncePerLick, IParticleSystemTarget
{
    // Propiedad del shader S_Enredaderas (la usan M_Enredaderas y Nucleo): 0 = entero, 1 = disuelto.
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    [Header("Config")]
    [SerializeField] private int _hitsToBreak = 1;

    [Header("Feedback (all optional)")]
    [SerializeField] private ParticleSystem _hitParticle;
    [Tooltip("No debe ser hijo de 'Visual': ese objeto se desactiva al romperse.")]
    [SerializeField] private ParticleSystem _breakParticle;
    [Tooltip("No debe ser hijo de 'Visual': ese objeto se desactiva al romperse.")]
    [SerializeField] private AudioSource _breakSound;
    [Tooltip("Malla/objeto del núcleo. Se oculta al romperse, salvo que haya un Purified Material asignado.")]
    [SerializeField] private GameObject _visual;
    [Tooltip("Material que recibe la malla de 'Visual' al romperse. Si está asignado, la malla queda visible con este material en vez de ocultarse.")]
    [SerializeField] private Material _purifiedMaterial;
    [SerializeField] private ParticleSystem _indicator;
    public bool CanBeTargeted => !IsBroken;
    public ParticleSystem Indicator => _indicator;

    [Header("Dissolve (on break)")]
    [Tooltip("Velocidad del disolve en unidades de _Dissolve por segundo: 0.5 = tarda 2 s en ir de 0 a 1, 2 = tarda 0.5 s. Solo corre cuando el núcleo se rompe.")]
    [Min(0.01f)]
    [SerializeField] private float _dissolveSpeed = 0.5f;

    [Header("Line (on break, optional)")]
    [Tooltip("LineController que dibuja la línea. Al romperse el núcleo se le sacan las posiciones de abajo y se redibuja.")]
    [SerializeField] private LineController _lineController;
    [Tooltip("Posiciones de la línea que pertenecen a este núcleo (las mismas que están en la lista del LineController). Al romperse se sacan de esa lista y se DESTRUYEN sus GameObjects.")]
    [SerializeField] private Transform[] _linePositions;

    [Header("Veins / Vines Art (optional)")]
    [SerializeField] private GameObject[] _activeWhileIntact;
    [SerializeField] private GameObject[] _activeWhenBroken;

    private Collider _collider;
    private readonly List<Material> _dissolveMaterials = new();
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
        _indicator.gameObject.SetActive(false);
        if(_collider != null) _collider.enabled = false;

        // Si algún renderer tiene _Dissolve, se disuelve y recién después el visual pasa a su estado final.
        // Si no, cambia al instante (núcleos con un material común, sin disolve).
        List<Renderer> dissolving = GetDissolveTargets();

        if(dissolving.Count > 0) StartCoroutine(DissolveRoutine(dissolving));
        else ApplyBrokenVisual();

        if(_breakParticle != null) _breakParticle.Play();

        if(_breakSound != null) _breakSound.Play();

        SetActiveAll(_activeWhileIntact, false);
        SetActiveAll(_activeWhenBroken, true);
        RemoveLinePositions();
        Broken?.Invoke(this);
    }

    // Saca las posiciones de este núcleo de la línea (se redibuja sin ellas y con el loop abierto) y después destruye
    // sus GameObjects. El orden importa: primero se sacan de la lista del LineController y recién después se destruyen
    // (Destroy es diferido, pero así la línea nunca depende de un Transform destruido).
    // Con LineController asignado, romper el núcleo abre el loop aunque este núcleo no tenga posiciones propias.
    private void RemoveLinePositions()
    {
        if(_lineController != null) _lineController.RemovePositions(_linePositions);

        if(_linePositions == null) return;

        foreach(Transform position in _linePositions)
        {
            // Nunca se destruye el propio núcleo ni un padre suyo (IsChildOf también da true para sí mismo):
            // se llevaría puesto a este componente mientras termina de romperse.
            if(position == null || transform.IsChildOf(position)) continue;

            Destroy(position.gameObject);
        }
    }

    // Sube _Dissolve de 0 a 1 a _dissolveSpeed por segundo (0.5 = 2 s). Corre una sola vez, al romperse el núcleo.
    private IEnumerator DissolveRoutine(List<Renderer> targets)
    {
        List<Material> materials = GetDissolveMaterials(targets);
        float dissolve = 0f;
        float speed = Mathf.Max(_dissolveSpeed, 0.01f);

        SetDissolve(materials, dissolve);

        while(dissolve < 1f)
        {
            dissolve = Mathf.MoveTowards(dissolve, 1f, speed * Time.deltaTime);
            SetDissolve(materials, dissolve);
            yield return null;
        }

        // Ya disuelto del todo no se ve: se apaga para no seguir dibujándolo. La malla que recibe el material
        // purificado queda afuera, porque ApplyBrokenVisual() la vuelve a mostrar con el material nuevo.
        Renderer purifiedMesh = GetPurifiedMesh();

        foreach(Renderer r in targets)
            if(r != null && r != purifiedMesh) r.enabled = false;

        ApplyBrokenVisual();
    }

    // r.materials da una instancia propia de cada renderer: M_Enredaderas lo comparten todas las enredaderas del nivel,
    // y escribir en el asset las disolvería a todas. No se usa MaterialPropertyBlock porque el GPU Resident Drawer
    // (activo en PC_RPAsset) no es compatible con property blocks.
    private List<Material> GetDissolveMaterials(List<Renderer> targets)
    {
        List<Material> materials = new();

        foreach(Renderer r in targets)
        {
            if(r == null) continue;

            foreach(Material material in r.materials)
                if(material != null && material.HasProperty(DissolveId)) materials.Add(material);
        }

        _dissolveMaterials.AddRange(materials);

        return materials;
    }

    private static void SetDissolve(List<Material> materials, float value)
    {
        foreach(Material material in materials)
            if(material != null) material.SetFloat(DissolveId, value);
    }

    // Las instancias creadas por r.materials no las libera Unity solas hasta cambiar de escena.
    private void OnDestroy()
    {
        foreach(Material material in _dissolveMaterials)
            if(material != null) Destroy(material);
    }

    // Renderers del núcleo (él mismo y sus hijos activos) cuyo material tiene _Dissolve. Devuelve una lista vacía si
    // el objeto está inactivo (sin corrutina no hay animación) o si ningún material la tiene; en los dos casos el
    // visual cambia al instante, como antes.
    private List<Renderer> GetDissolveTargets()
    {
        List<Renderer> targets = new();

        if(!gameObject.activeInHierarchy) return targets;

        foreach(Renderer r in GetComponentsInChildren<Renderer>())
            if(HasDissolve(r)) targets.Add(r);

        return targets;
    }

    private static bool HasDissolve(Renderer r)
    {
        foreach(Material material in r.sharedMaterials)
            if(material != null && material.HasProperty(DissolveId)) return true;

        return false;
    }

    // Estado final del visual: la malla recibe el material purificado, o se oculta si no hay uno asignado.
    private void ApplyBrokenVisual()
    {
        if(_visual == null) return;

        //Renderer mesh = GetPurifiedMesh();
        //
        //if(mesh != null) mesh.sharedMaterial = _purifiedMaterial;

        //else _visual.SetActive(false);
        gameObject.SetActive(false);
    }

    // Malla de 'Visual' que recibe el material purificado. null si no hay material asignado (el visual se oculta).
    private Renderer GetPurifiedMesh()
    {
        return _purifiedMaterial != null && _visual != null ? _visual.GetComponentInChildren<Renderer>(true) : null;
    }

    private static void SetActiveAll(GameObject[] objects, bool active)
    {
        if(objects == null) return;

        foreach(GameObject go in objects)
            if(go != null) go.SetActive(active);
    }
}
