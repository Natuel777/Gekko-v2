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
            if (!KeepEnabledOnPause(keyValue.Key)) keyValue.Key.enabled = keyValue.Value;
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
            if (!KeepEnabledOnPause(b)) b.enabled = false;

            if(b.gameObject.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
    }

    // Behaviours que la pausa deja como están (ni los apaga ni, al despausar, les pisa el estado):
    //
    //  - Polybrush (PolybrushMesh, etc.): son de autoría, en juego no hacen nada. Si se los vuelve a prender (al salir de la
    //    pausa), PolybrushMesh.OnEnable revisa la malla de su MeshFilter y, si no coincide con la que tiene guardada, relee la
    //    malla del FBX. Las rocas (R_Tall_lp, R_Small_lp) no tienen Read/Write activado, y eso tiraba ~10 errores
    //    "Not allowed to access ... on mesh" por roca cada vez que se salía de la pausa.
    //
    //  - IPauseExempt (ScatterField, GrassField): solo dibujan, con Graphics.DrawMesh*, sin MeshRenderer. Apagados no se
    //    dibuja nada: toda la vegetación pintada con la tool de props desaparecía al pausar.
    //
    //  - Cámaras de Cinemachine (CinemachineCamera, OrbitalFollow/RotationComposer/etc. y extensiones como CameraRotation o
    //    el Decollider): si se apagaba la cámara de Gekko (que vive adentro de "Game"), el CinemachineBrain (en Main Camera,
    //    afuera de "Game") pasaba a la cámara activa de mayor prioridad que quedara prendida. En LvlTwo esas son las del
    //    desafío final (ChallengeFinal/Arena_Boss, prioridad 30, afuera de "Game"): al pausar la vista saltaba a la arena del
    //    jefe, mostrando el mundo desde una posición rara. Con timeScale en 0 la cámara igual no se mueve, y el input de
    //    órbita (CinemachineInputAxisController, que no es ninguno de estos tipos) sí se sigue apagando.
    private static bool KeepEnabledOnPause(Behaviour b)
    {
        if (b is IPauseExempt) return true;
        if (b is Unity.Cinemachine.CinemachineVirtualCameraBase
            || b is Unity.Cinemachine.CinemachineComponentBase
            || b is Unity.Cinemachine.CinemachineExtension) return true;

        string ns = b.GetType().Namespace;
        return ns != null && ns.StartsWith("UnityEngine.Polybrush", System.StringComparison.Ordinal);
    }

    public void Free()
    {
        GameObject.Destroy(root.gameObject);
    }
}
