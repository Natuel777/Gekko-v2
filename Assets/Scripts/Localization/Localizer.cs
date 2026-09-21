using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage { Spanish = 0, English = 1 }

public static class Localizer
{
    private static GameLanguage? _current;

    public static event Action OnLanguageChanged;

    public static GameLanguage Current
    {
        get
        {
            if (_current == null)
            {
                if (PlayerPrefs.HasKey(PlayerPrefsKeys.languageKey))
                    _current = (GameLanguage)PlayerPrefs.GetInt(PlayerPrefsKeys.languageKey);
                else
                    _current = Application.systemLanguage == SystemLanguage.Spanish ? GameLanguage.Spanish : GameLanguage.English;
            }
            return _current.Value;
        }
    }

    public static void SetLanguage(GameLanguage language)
    {
        if (_current == language) return;
        _current = language;
        PlayerPrefs.SetInt(PlayerPrefsKeys.languageKey, (int)language);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        if (Table.TryGetValue(key, out var entry))
            return Current == GameLanguage.Spanish ? entry.es : entry.en;
        return key;
    }

    // Para agregar textos nuevos: sumar una linea (clave, español, ingles) y usar la clave en un LocalizedText.
    private static readonly Dictionary<string, (string es, string en)> Table = new Dictionary<string, (string es, string en)>
    {
        { "tab_game", ("Juego", "Game") },
        { "tab_graphics", ("Gráficos", "Graphics") },
        { "tab_sound", ("Sonido", "Sound") },
        { "tab_controls", ("Controles", "Controls") },

        { "back", ("Volver", "Back") },
        { "restart_level", ("Reiniciar nivel", "Restart Level") },
        { "language", ("Idioma", "Language") },

        { "resolution", ("Resolución", "Resolution") },
        { "fullscreen", ("Pantalla completa", "Fullscreen") },

        { "volume_master", ("Volumen general", "Master Volume") },
        { "volume_music", ("Volumen de música", "Music Volume") },
        { "volume_sfx", ("Volumen de efectos", "SFX Volume") },
        { "music", ("Música", "Music") },
        { "on", ("Activada", "On") },
        { "off", ("Desactivada", "Off") },

        { "ctrl_up", ("Mover adelante", "Move Forward") },
        { "ctrl_down", ("Mover atrás", "Move Back") },
        { "ctrl_left", ("Mover izquierda", "Move Left") },
        { "ctrl_right", ("Mover derecha", "Move Right") },
        { "ctrl_jump", ("Saltar", "Jump") },
        { "ctrl_tongue", ("Lengua", "Tongue") },
        { "ctrl_grapple", ("Enganchar lengua", "Grapple Tongue") },
        { "ctrl_interact", ("Interactuar", "Interact") },
        { "ctrl_restart", ("Reiniciar", "Restart") },
        { "ctrl_pause", ("Pausa", "Pause") },
        { "ctrl_reset", ("Restablecer controles", "Reset Controls") },
        { "ctrl_press_key", ("Presioná una tecla...", "Press a key...") },
    };
}
