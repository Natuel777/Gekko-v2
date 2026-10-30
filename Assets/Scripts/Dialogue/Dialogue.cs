using UnityEngine;

[System.Serializable]
public struct Dialogue
{
    public string speakerName;
    [TextArea(2, 5)] public string[] sentences;
    [Tooltip("Opcional, para diálogos con más de un interlocutor: un nombre por frase (mismo largo que sentences). Vacío o de otro largo = todas las frases usan speakerName.")]
    public string[] speakerNames;
}
