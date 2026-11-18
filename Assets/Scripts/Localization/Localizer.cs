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
        { "loading_label", ("CARGANDO", "LOADING") },

        { "tab_game", ("Idioma", "Language") },
        { "tab_graphics", ("Gráficos", "Graphics") },
        { "tab_sound", ("Sonido", "Sound") },
        { "tab_controls", ("Controles", "Controls") },

        { "back", ("Volver", "Back") },
        { "restart_level", ("Reiniciar nivel", "Restart Level") },
        { "language", ("Idioma", "Language") },

        { "resolution", ("Resolución", "Resolution") },
        { "fullscreen", ("Pantalla completa", "Fullscreen") },
        { "brightness", ("Brillo", "Brightness") },
        { "camera_shake", ("Sacudida de cámara", "Camera Shake") },
        { "camera_sensitivity", ("Sensibilidad de cámara", "Camera Sensitivity") },
        { "invert_y", ("Invertir cámara Y", "Invert Camera Y") },

        { "volume_master", ("Volumen general", "Master Volume") },
        { "volume_music", ("Volumen de música", "Music Volume") },
        { "volume_sfx", ("Volumen de efectos", "SFX Volume") },
        { "music", ("Música", "Music") },
        { "on", ("Activada", "On") },
        { "yes", ("Sí", "Yes") },
        { "no", ("No", "No") },
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

        // Títulos y botones de Opciones / Pausa
        { "options", ("Opciones", "Options") },
        { "pause", ("Pausa", "Pause") },
        { "continue", ("Continuar", "Continue") },
        { "exit", ("Salir", "Exit") },

        // Cartel de confirmación (ScreenReafirm)
        { "reafirm_menu_title", ("¿Volver al menú?", "Back to the menu?") },
        { "reafirm_menu_msg", ("Vas a perder el progreso del nivel.", "You will lose your progress in this level.") },
        { "reafirm_restart_title", ("¿Reiniciar nivel?", "Restart the level?") },
        { "reafirm_restart_msg", ("El nivel vuelve a empezar y se pierde el progreso.", "The level starts over and your progress is lost.") },
        { "reafirm_delete_title", ("¿Borrar datos?", "Delete data?") },
        { "reafirm_delete_msg", ("Se borra tu progreso: arándanos, frutillas y lo que ya viste. La configuración se mantiene.", "Your progress will be deleted: blueberries, strawberries and what you've seen. Settings are kept.") },
        { "delete_data", ("Borrar datos del jugador", "Delete player data") },
        { "data_deleted", ("¡Datos borrados!", "Data deleted!") },
        { "desc_delete_data", ("Borra tu progreso (arándanos, frutillas y eventos vistos). La configuración se mantiene.", "Deletes your progress (blueberries, strawberries and seen events). Settings are kept.") },
        { "apply", ("Aplicar", "Apply") },
        { "applied", ("Aplicado", "Applied") },
        { "desc_apply", ("Guarda las teclas nuevas y las aplica al juego. Si salís sin aplicar, se descartan.", "Saves the new keys and applies them to the game. If you leave without applying, they are discarded.") },
        { "reafirm_exit_title", ("¿Salir del juego?", "Quit the game?") },
        { "reafirm_exit_msg", ("Tu partida queda guardada.", "Your game will be saved.") },

        // Panel de descripción del menú de Opciones (OptionRowInfo)
        { "quality", ("Calidad gráfica", "Graphics Quality") },
        { "lighting", ("Iluminación", "Lighting") },
        { "desc_tab_game", ("Opciones generales del juego.", "General game options.") },
        { "desc_tab_graphics", ("Ajustá cómo se ve el juego.", "Adjust how the game looks.") },
        { "desc_tab_sound", ("Ajustá los volúmenes del juego.", "Adjust the game volumes.") },
        { "desc_tab_controls", ("Cambiá las teclas y la sensibilidad del mouse.", "Change key bindings and mouse sensitivity.") },
        { "desc_language", ("Elegí el idioma del juego.", "Select your preferred language.") },
        { "desc_restart_level", ("Vuelve a empezar el nivel actual desde el principio.", "Restart the current level from the beginning.") },
        { "desc_resolution", ("Elegí la resolución de pantalla.", "Select the screen resolution.") },
        { "desc_quality", ("Mayor calidad se ve mejor pero exige más a la PC.", "Higher quality looks better but is more demanding.") },
        { "desc_fullscreen", ("Alterná entre pantalla completa y ventana.", "Switch between fullscreen and windowed mode.") },
        { "desc_lighting", ("Ajustá la iluminación general de la escena.", "Adjust the overall scene lighting.") },
        { "desc_volume_master", ("Ajustá el volumen general.", "Adjust the overall volume.") },
        { "desc_music", ("Activá o desactivá la música.", "Turn the music on or off.") },
        { "desc_volume_music", ("Ajustá el volumen de la música.", "Adjust the music volume.") },
        { "desc_volume_sfx", ("Ajustá el volumen de los efectos.", "Adjust the sound effects volume.") },
        { "mouse_sensitivity", ("Sensibilidad del mouse", "Mouse Sensitivity") },
        { "desc_mouse_sensitivity", ("Qué tan rápido gira la cámara con el mouse.", "How fast the camera turns with the mouse.") },
        { "desc_rebind", ("Seleccioná la acción y presioná la tecla nueva.", "Select the action and press the new key.") },
        { "desc_ctrl_reset", ("Vuelve todas las teclas a su configuración original (después apretá Aplicar).", "Restore all key bindings to their defaults (then press Apply).") },

        // Cartel de presentación del boss (BossTitleCard)
        { "boss1_name", ("Planta Corrupta", "Corrupted Plant") },
        { "boss1_subtitle", ("Guardiana del claro", "Guardian of the Glade") },
    };
}
