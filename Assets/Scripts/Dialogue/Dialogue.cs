using UnityEngine;

[System.Serializable]
public struct Dialogue
{
    public string speakerName;
    [TextArea(2, 5)] public string[] sentences;
}
