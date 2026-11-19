using UnityEngine;

// Parpadeo de daño de la planta: cuando Gekko la daña (golpe que cuenta, sin purificarla), todos sus SkinnedMeshRenderer pasan al
// material "herido" (DamagedPlant.mat: el mismo de la planta pero teñido de rojo) durante unos segundos, y después vuelven a los
// materiales que tenían.
//
// Clase plana: la maneja CarnivorousPlant con Play() y ArtificialLateUpdate(). El plazo se mira con Time.time en LateUpdate y no
// con una corrutina ni en Update: la planta frena su Update mientras está aturdida por el golpe (justo cuando corre este efecto)
// y una corrutina muere si se apaga el GameObject. Así, si el efecto se interrumpe, el material se restaura en el primer
// LateUpdate que corra después de vencido el plazo.
public class PlantDamageFlash
{
    private readonly SkinnedMeshRenderer[] _renderers;
    private readonly Material _damagedMaterial;
    private readonly float _duration;

    // Materiales de cada renderer antes del parpadeo (mismo índice que _renderers).
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

        // Otro golpe con el parpadeo en curso solo reinicia el plazo. Los originales ya están guardados y no se vuelven a leer:
        // a esta altura los renderers tienen puesto el material herido.
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

    // Corta el parpadeo ya y devuelve los materiales originales (lo usa la purificación, que pone su propio material después).
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
