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
            keyValue.Key.enabled = keyValue.Value;
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
            b.enabled = false;

            if(b.gameObject.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }
    }

    public void Free()
    {
        GameObject.Destroy(root.gameObject);
    }
}
