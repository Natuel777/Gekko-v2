using System.Collections.Generic;
using UnityEngine;

// Lleva la cuenta regresiva de re-corrupción de los escarabajos del desafío. Clase plana: la
// MonoBehaviour dueña (PurificationChallenge) le reenvía Update vía ArtificialUpdate.
public class RecorruptScheduler
{
    private class Entry
    {
        public HeavyBeetle beetle;
        public float timer;
    }

    private readonly List<Entry> _entries = new();
    private readonly float _delayMin;
    private readonly float _delayMax;
    private readonly System.Action<HeavyBeetle> _onElapsed;

    public RecorruptScheduler(float delayMin, float delayMax, System.Action<HeavyBeetle> onElapsed)
    {
        _delayMin = delayMin;
        _delayMax = Mathf.Max(delayMin, delayMax);
        _onElapsed = onElapsed;
    }

    public void Schedule(HeavyBeetle beetle)
    {
        if(beetle == null) return;

        Cancel(beetle);
        _entries.Add(new Entry { beetle = beetle, timer = Random.Range(_delayMin, _delayMax) });
    }

    public void Cancel(HeavyBeetle beetle) => _entries.RemoveAll(e => e.beetle == beetle);

    public void CancelAll() => _entries.Clear();

    public void ArtificialUpdate(float dt)
    {
        for(int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];

            if(entry.beetle == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            entry.timer -= dt;
            if(entry.timer > 0f) continue;

            // Se saca antes de avisar: el callback re-corrompe y el controlador vuelve a llamar a Cancel.
            _entries.RemoveAt(i);
            _onElapsed?.Invoke(entry.beetle);
        }
    }
}
