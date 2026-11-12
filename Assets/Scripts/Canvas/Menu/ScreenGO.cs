using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenGO : IScreen
{
    Dictionary<Behaviour, bool> _before;
    public AudioListener _audio;
    public Transform root;

    public ScreenGO(Transform root, AudioListener audio)
    {
        _before = new Dictionary<Behaviour, bool>();
        _audio = audio;
        this.root = root;    
    }

    public void Activate()
    {
        foreach (var keyValue in _before)
        {
            if (!IsAuthoringOnly(keyValue.Key)) keyValue.Key.enabled = keyValue.Value;
            if (keyValue.Key.gameObject.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
        }
        if(_audio != null) _audio.enabled = false;
        _before.Clear();
    }

    public void Deactivate()
    {
        if (_audio != null) _audio.enabled = true;
        foreach (var b in root.GetComponentsInChildren<Behaviour>())
        {
            _before[b] = b.enabled;
            if (!IsAuthoringOnly(b)) b.enabled = false;

            if(b.gameObject.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
    }

    // Los componentes de Polybrush (PolybrushMesh, etc.) son de autoría: en juego no hacen nada, así que la pausa no los apaga.
    // Si se los vuelve a prender (al salir de la pausa), PolybrushMesh.OnEnable revisa la malla de su MeshFilter y, si no
    // coincide con la que tiene guardada, relee la malla del FBX. Las rocas (R_Tall_lp, R_Small_lp) no tienen Read/Write
    // activado, y eso tiraba ~10 errores "Not allowed to access ... on mesh" por roca cada vez que se salía de la pausa.
    private static bool IsAuthoringOnly(Behaviour b)
    {
        string ns = b.GetType().Namespace;
        return ns != null && ns.StartsWith("UnityEngine.Polybrush", System.StringComparison.Ordinal);
    }

    public void Free()
    {
        GameObject.Destroy(root.gameObject);
    }
}
