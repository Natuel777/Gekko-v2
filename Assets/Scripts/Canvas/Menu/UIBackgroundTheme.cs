using UnityEngine;

// Variante de color para el fondo de menú (Bosque, Purificado, Miel, Corrupto...).
// El color de fondo tiñe el tile (sirve para reusar la misma textura en escala de
// grises/neutra entre temas), y la viñeta se aplica aparte.
[CreateAssetMenu(fileName = "UIBackgroundTheme", menuName = "Gekko/UI/Background Theme")]
public class UIBackgroundTheme : ScriptableObject
{
    [Header("Tile")]
    public Texture2D tileTexture;

    [Header("Colores")]
    public Color backgroundColor = Color.white;
    public Color vignetteColor = Color.black;
    [Range(0f, 1f)] public float vignetteIntensity = 0.45f;
}
