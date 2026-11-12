using UnityEngine;

// Al purificarse el núcleo, los renderers de los objetos elegidos (y sus hijos) que usan "From" pasan a usar "To".
// Solo se reemplazan los slots que coinciden con "From": paredes u otros materiales quedan como están.
// El cambio es solo en runtime (sharedMaterials del renderer), no toca los assets ni el SplineTerrain.
[RequireComponent(typeof(PurificationCore))]
public class PurifiedMaterialSwap : MonoBehaviour
{
    [SerializeField] private GameObject[] _targets;
    [SerializeField] private Material _from;
    [SerializeField] private Material _to;

    private PurificationCore _core;

    private void Awake()
    {
        _core = GetComponent<PurificationCore>();
    }

    private void OnEnable()
    {
        _core.Broken += OnCoreBroken;
    }

    private void OnDisable()
    {
        _core.Broken -= OnCoreBroken;
    }

    private void OnCoreBroken(PurificationCore core)
    {
        if(_from == null || _to == null || _targets == null) return;

        foreach(GameObject target in _targets)
        {
            if(target == null) continue;

            foreach(Renderer r in target.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = r.sharedMaterials;
                bool changed = false;

                for(int i = 0; i < materials.Length; i++)
                {
                    if(materials[i] != _from) continue;

                    materials[i] = _to;
                    changed = true;
                }

                if(changed) r.sharedMaterials = materials;
            }
        }
    }
}
