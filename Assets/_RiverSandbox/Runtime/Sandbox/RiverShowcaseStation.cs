using UnityEngine;

// Una "estacion" de la escena vitrina: a donde va la camara, que se explica y donde aparece la pelota.
[AddComponentMenu("Gekko/River/Sandbox/River Showcase Station")]
public class RiverShowcaseStation : MonoBehaviour
{
    public string Title = "Estacion";
    [TextArea(3, 12)] public string Description;
    public Transform BallSpawn;
    public float Yaw = 30f;
    public float Pitch = 40f;
    public float Distance = 30f;
}
