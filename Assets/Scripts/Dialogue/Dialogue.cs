using UnityEngine;

[System.Serializable]
public struct Dialogue
{
    [Tooltip("Quién habla (nombre interno: lo usan los retratos y las cámaras). El nombre que se muestra se traduce con el Localizer (clave speaker_<nombre>) si existe.")]
    public string speakerName;
    [Tooltip("Frases en español.")]
    [TextArea(2, 5)] public string[] sentences;
    [Tooltip("Las mismas frases en inglés, en el mismo orden. Vacío = se usan las de español en los dos idiomas.")]
    [TextArea(2, 5)] public string[] sentencesEnglish;
    [Tooltip("Opcional, para diálogos con más de un interlocutor: un nombre por frase (mismo largo que sentences). Vacío o de otro largo = todas las frases usan speakerName.")]
    public string[] speakerNames;

    // Las frases en el idioma elegido (Localizer). Si falta la traducción, las del otro idioma: nunca queda mudo.
    public string[] LocalizedSentences
    {
        get
        {
            bool english = Localizer.Current == GameLanguage.English;
            string[] preferred = english ? sentencesEnglish : sentences;
            string[] fallback = english ? sentences : sentencesEnglish;
            if(preferred != null && preferred.Length > 0) return preferred;
            return fallback ?? new string[0];
        }
    }
}
