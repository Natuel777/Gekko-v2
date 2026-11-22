using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Al purificar al boss (CarniborousPlantBOSS) lo disuelve y revela al gekko azul escondido bajo la corrupción.
// Sibling de CarnivorousPlant: no lo toca, solo escucha su evento Purified (mismo criterio que
// PurificationChallenge, que tampoco modifica CarnivorousPlant ni esta clase).
public class BossGekkoReveal : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private CarnivorousPlant _plant;
    [Tooltip("Propiedad del shader que controla el disolve, si el material purificado la tiene (ver CorruptedPlant). Si ninguna la tiene, se encoge el objeto como alternativa.")]
    [SerializeField] private string _dissolveProperty = "_Dissolve";

    [Header("Timing")]
    [Tooltip("Segundos de espera desde que se purifica la planta hasta que empieza a disolverse (para no pisar la cinemática de purificación).")]
    [Min(0f)]
    [SerializeField] private float _delayBeforeDissolve = 2.5f;
    [Tooltip("Segundos que tarda en disolverse/encogerse del todo.")]
    [Min(0.1f)]
    [SerializeField] private float _dissolveDuration = 1.3f;

    [Header("Gekko revelado")]
    [Tooltip("Gekko azul, ya ubicado en la escena y desactivado: se activa al terminar de disolverse la planta.")]
    [SerializeField] private GameObject _friendGekko;
    [Tooltip("Dónde aparece, relativo a la planta (espacio local de la planta: X = derecha, Y = arriba, Z = adelante). Se recalcula recién al revelarlo, así no importa si la planta/la zona se movió desde que se armó la escena.")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 0.1f, 2.5f);
    [Tooltip("Para asentarlo en el piso real al aparecer (por si el punto calculado no coincide exacto con el suelo). Vacío = no reacomoda la altura.")]
    [SerializeField] private LayerMask _groundLayer;
    [Tooltip("Metros de margen sobre el punto de piso encontrado.")]
    [SerializeField] private float _groundClearance = 0.05f;
    [Tooltip("Después de activarlo, baja/sube al gekko para que el vértice más bajo de su malla (las patas) toque el piso. Corrige que el modelo hijo tenga un offset propio y quede flotando.")]
    [SerializeField] private bool _alignMeshToGround = true;
    [Tooltip("Metros entre las patas y el piso al alinear la malla (negativo = se hunde un poco en el pasto).")]
    [SerializeField] private float _meshGroundOffset = 0f;
    [Tooltip("Arranca el diálogo del gekko apenas aparece, sin que el player tenga que acercarse y apretar interactuar.")]
    [SerializeField] private bool _autoStartDialogue = true;
    [Tooltip("Segundos entre que aparece el gekko y arranca el diálogo.")]
    [Min(0f)]
    [SerializeField] private float _dialogueDelay = 0.5f;

    private int _dissolveId;
    private bool _triggered;
    private bool _hasGroundY;
    private float _groundY;

    private void Awake()
    {
        if(_plant == null) _plant = GetComponent<CarnivorousPlant>();
        _dissolveId = Shader.PropertyToID(_dissolveProperty);
    }

    private void OnEnable()
    {
        if(_plant != null) _plant.Purified += OnPlantPurified;
    }

    private void OnDisable()
    {
        if(_plant != null) _plant.Purified -= OnPlantPurified;
    }

    private void OnPlantPurified()
    {
        if(_triggered) return;
        _triggered = true;

        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        if(_delayBeforeDissolve > 0f) yield return new WaitForSeconds(_delayBeforeDissolve);

        // La cinemática de purificación (placeholder "Planta") puede seguir con el diálogo abierto acá: UIManager
        // no deja arrancar el de Celeste mientras haya uno activo, así que esperamos a que cierre el anterior.
        yield return new WaitUntil(() => UIManager.Instance == null || !UIManager.Instance.HasActiveDialogue());

        yield return DissolvePlant();

        if(_friendGekko == null) yield break;

        PlaceNextToPlant(_friendGekko.transform);
        _friendGekko.SetActive(true);
        AlignMeshToGround(_friendGekko);

        if(!_autoStartDialogue) yield break;

        // Un frame para que el gekko recién activado termine su Awake/OnEnable antes de hablar.
        yield return null;
        if(_dialogueDelay > 0f) yield return new WaitForSeconds(_dialogueDelay);

        if(UIManager.Instance != null && _friendGekko.TryGetComponent(out IDialogueable dialogueable))
            UIManager.Instance.StartDialogue(dialogueable);
    }

    // Posición relativa a la planta, calculada recién ahora (no guardada de antes): si la zona se movió o
    // reacomodó desde que se armó la escena, igual aparece pegado a donde está la planta de verdad.
    private void PlaceNextToPlant(Transform t)
    {
        Transform plantTransform = _plant.transform;
        t.position = plantTransform.position + plantTransform.TransformDirection(_spawnOffset);
        t.rotation = Quaternion.LookRotation(-plantTransform.forward, Vector3.up);

        // CorruptionChallenge (el padre de este objeto) tiene su propia escala local: sin esto, el gekko y su
        // collider de interacción quedan encogidos/agrandados según de qué objeto cuelgue en la escena.
        if(t.parent != null)
        {
            Vector3 parentLossy = t.parent.lossyScale;
            t.localScale = new Vector3(
                parentLossy.x != 0f ? 1f / parentLossy.x : 1f,
                parentLossy.y != 0f ? 1f / parentLossy.y : 1f,
                parentLossy.z != 0f ? 1f / parentLossy.z : 1f);
        }

        SnapToGround(t);
        Physics.SyncTransforms();
    }

    // Asienta al gekko sobre el piso real debajo de donde está ubicado, para no depender de que la posición
    // calculada coincida pixel a pixel con el terreno.
    private void SnapToGround(Transform t)
    {
        if(_groundLayer.value == 0) return;

        Vector3 origin = t.position + Vector3.up * 3f;
        if(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            t.position = hit.point + Vector3.up * _groundClearance;
            _groundY = hit.point.y;
            _hasGroundY = true;
        }
    }

    // El pivot queda en el piso, pero el modelo hijo tiene su propio offset (y la pose del animator/IK), así que
    // las patas quedaban flotando. Fuerza una evaluación del animator, mide el vértice más bajo real de la malla
    // y corre al gekko para que ese punto apoye sobre el piso.
    private void AlignMeshToGround(GameObject gekko)
    {
        if(!_alignMeshToGround || !_hasGroundY) return;

        foreach(Animator animator in gekko.GetComponentsInChildren<Animator>())
            if(animator.isActiveAndEnabled) animator.Update(0f);

        float minY = float.MaxValue;
        Mesh baked = new Mesh();

        foreach(SkinnedMeshRenderer smr in gekko.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.BakeMesh(baked, true);
            foreach(Vector3 v in baked.vertices)
                minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
        }

        foreach(MeshFilter mf in gekko.GetComponentsInChildren<MeshFilter>())
        {
            if(mf.sharedMesh == null) continue;
            foreach(Vector3 v in mf.sharedMesh.vertices)
                minY = Mathf.Min(minY, mf.transform.TransformPoint(v).y);
        }

        Destroy(baked);
        if(minY == float.MaxValue) return;

        gekko.transform.position += Vector3.up * (_groundY + _meshGroundOffset - minY);
        Physics.SyncTransforms();
    }

    // Mismo patrón que CorruptedPlant (Assets/_Gekko/Purification): anima _Dissolve en instancias de material
    // si el material purificado lo tiene; si no, encoge el objeto hasta desaparecer.
    private IEnumerator DissolvePlant()
    {
        List<Renderer> renderers = new();
        renderers.AddRange(_plant.GetComponentsInChildren<SkinnedMeshRenderer>());
        renderers.AddRange(_plant.GetComponentsInChildren<MeshRenderer>());

        List<Material> instances = new();
        foreach(Renderer r in renderers)
        {
            bool tieneDisolve = false;
            foreach(Material shared in r.sharedMaterials)
                if(shared != null && shared.HasProperty(_dissolveId)) { tieneDisolve = true; break; }

            if(!tieneDisolve) continue;

            foreach(Material material in r.materials)
                if(material != null && material.HasProperty(_dissolveId)) instances.Add(material);
        }

        Transform plantTransform = _plant.transform;
        Vector3 startScale = plantTransform.localScale;
        float t = 0f;

        while(t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / _dissolveDuration);

            if(instances.Count > 0)
            {
                foreach(Material material in instances)
                    if(material != null) material.SetFloat(_dissolveId, t);
            }
            else
            {
                plantTransform.localScale = startScale * (1f - t * t);
            }

            yield return null;
        }

        foreach(Renderer r in renderers)
            if(r != null) r.enabled = false;
    }
}
