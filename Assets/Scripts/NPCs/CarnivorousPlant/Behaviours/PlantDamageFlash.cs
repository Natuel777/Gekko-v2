using UnityEngine;

// Plant damage flash: when Gekko damages it (a hit that counts, without purifying it), all of its SkinnedMeshRenderers switch to the
// "hurt" material (DamagedPlant.mat: the plant's own material tinted red) for a few seconds, and then go back to the
// materials they had.
//
// Plain class: CarnivorousPlant drives it with Play() and ArtificialLateUpdate(). The deadline is checked with Time.time in LateUpdate and not
// with a coroutine or in Update: the plant stops its Update while it is stunned by the hit (exactly when this effect runs)
// and a coroutine dies if the GameObject is turned off. That way, if the effect is interrupted, the material is restored in the first
// LateUpdate that runs after the deadline has passed.
public class PlantDamageFlash
{
    private readonly SkinnedMeshRenderer[] _renderers;
    private readonly Material _damagedMaterial;
    private readonly float _duration;

    // Materials of each renderer before the flash (same index as _renderers).
    private readonly Material[][] _originals;
    private bool _active;
    private float _endTime;

    public bool IsActive => _active;

    public PlantDamageFlash(SkinnedMeshRenderer[] renderers, Material damagedMaterial, float duration)
    {
        _renderers = renderers;
        _damagedMaterial = damagedMaterial;
        _duration = duration;
        _originals = new Material[renderers != null ? renderers.Length : 0][];
    }

    public void Play()
    {
        if(_renderers == null || _damagedMaterial == null || _duration <= 0f) return;

        // Another hit while the flash is running only restarts the deadline. The originals are already saved and are not read again:
        // by this point the renderers have the hurt material on.
        if(!_active)
        {
            for(int i = 0; i < _renderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = _renderers[i];

                if(renderer == null) continue;

                Material[] original = renderer.sharedMaterials;
                _originals[i] = original;

                Material[] damaged = new Material[original.Length];

                for(int j = 0; j < damaged.Length; j++)
                    damaged[j] = _damagedMaterial;

                renderer.sharedMaterials = damaged;
            }

            _active = true;
        }

        _endTime = Time.time + _duration;
    }

    public void ArtificialLateUpdate()
    {
        if(_active && Time.time >= _endTime) Restore();
    }

    // Cuts the flash right now and gives back the original materials (used by purification, which sets its own material afterwards).
    public void Cancel()
    {
        if(_active) Restore();
    }

    private void Restore()
    {
        _active = false;

        for(int i = 0; i < _renderers.Length; i++)
        {
            if(_renderers[i] != null && _originals[i] != null)
                _renderers[i].sharedMaterials = _originals[i];

            _originals[i] = null;
        }
    }
}
