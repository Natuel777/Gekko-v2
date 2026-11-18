using System;
using System.Collections.Generic;

public static class CollectiblesRegister
{
    private static Dictionary<string, int> _counts = new Dictionary<string, int>();

    // Avisa cada vez que se recoge algo (no al cargar partida con SetCollectibleCount). Lo usa LevelStats para contar
    // lo recogido en el nivel actual por separado del acumulado de toda la partida.
    public static event Action<string> OnRegistered;

    public static void RegisterCollectible(string collectibleName)
    {
        if (!_counts.ContainsKey(collectibleName))
            _counts[collectibleName] = 0;
        _counts[collectibleName]++;
        OnRegistered?.Invoke(collectibleName);
    }

    public static int GetCollectibleCount(string collectibleName) =>
        _counts.TryGetValue(collectibleName, out int c) ? c : 0;
    public static void SetCollectibleCount(string collectibleName, int quant) => _counts[collectibleName] = quant;
    public static void Clear() => _counts.Clear();

    // Foto de los contadores (GameManager la toma al cargar cada escena de juego y la restaura al reiniciar el nivel,
    // para descartar lo juntado en el intento sin perder lo de niveles anteriores).
    public static Dictionary<string, int> Snapshot() => new Dictionary<string, int>(_counts);
    public static void Restore(Dictionary<string, int> snapshot)
    {
        _counts.Clear();
        if (snapshot == null) return;
        foreach (var kv in snapshot) _counts[kv.Key] = kv.Value;
    }
}
