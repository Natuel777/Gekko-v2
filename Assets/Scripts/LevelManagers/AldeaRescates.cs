using System.Collections.Generic;
using UnityEngine;

// Qué gekkos amigos rescató el jugador (Celeste, Moki...). Lo registra FriendGekkoDialogue al terminar la charla de
// rescate y lo lee AldeaIntroSequence para decidir quién aparece en su casa de la Aldea.
// Se guarda en PlayerPrefs como una lista separada por ';' (PlayerPrefsKeys.aldeaRescatadosKey). Sin distinguir mayúsculas.
public static class AldeaRescates
{
    private const char Separador = ';';

    // Si alguna vez se guardó algo (aunque sea vacío). Sirve para migrar partidas de antes de este sistema.
    public static bool HayDatos => PlayerPrefs.HasKey(PlayerPrefsKeys.aldeaRescatadosKey);

    public static bool EstaRescatado(string nombre)
    {
        if(string.IsNullOrWhiteSpace(nombre)) return false;
        return Leer().Contains(Normalizar(nombre));
    }

    public static void Registrar(string nombre)
    {
        if(string.IsNullOrWhiteSpace(nombre)) return;

        HashSet<string> rescatados = Leer();
        if(!rescatados.Add(Normalizar(nombre)) && HayDatos) return;

        PlayerPrefs.SetString(PlayerPrefsKeys.aldeaRescatadosKey, string.Join(Separador.ToString(), rescatados));
        PlayerPrefs.Save();
        Debug.Log($"[Aldea] Rescatado: {nombre}.");
    }

    public static void Borrar()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKeys.aldeaRescatadosKey);
        PlayerPrefs.Save();
    }

    private static HashSet<string> Leer()
    {
        var set = new HashSet<string>();
        string guardado = PlayerPrefs.GetString(PlayerPrefsKeys.aldeaRescatadosKey, string.Empty);
        foreach(string s in guardado.Split(Separador))
            if(!string.IsNullOrWhiteSpace(s)) set.Add(Normalizar(s));
        return set;
    }

    private static string Normalizar(string nombre) => nombre.Trim().ToLowerInvariant();
}
