using UnityEngine;
using UnityEngine.UI;

// Viñeta radial generada por código en Awake (sin importar ninguna textura): la
// forma (centro transparente, borde opaco) queda horneada en el canal alpha de la
// textura, y el color/intensidad final se aplica aparte mult iplicando el color del
// RawImage, así SetIntensity/SetColor no necesitan regenerar la textura.
[RequireComponent(typeof(RawImage))]
public class UIRadialVignette : MonoBehaviour
{
    [SerializeField] private int _resolution = 256;
    [Tooltip("Distancia (0-1, desde el centro) donde arranca a oscurecer.")]
    [SerializeField, Range(0f, 1f)] private float _innerRadius = 0.4f;
    [SerializeField] private Color _color = Color.black;
    [SerializeField, Range(0f, 1f)] private float _intensity = 0.45f;

    private RawImage _rawImage;
    private Texture2D _texture;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        if(_rawImage.raycastTarget) _rawImage.raycastTarget = false; // la viñeta nunca debe bloquear clicks

        GenerateTexture();
        Apply();
    }

    private void OnDestroy()
    {
        if(_texture != null) Destroy(_texture);
    }

    private void GenerateTexture()
    {
        _texture = new Texture2D(_resolution, _resolution, TextureFormat.Alpha8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "Generated_RadialVignette"
        };

        float half = _resolution * 0.5f;
        Color[] pixels = new Color[_resolution * _resolution];

        for(int y = 0; y < _resolution; y++)
        {
            for(int x = 0; x < _resolution; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_innerRadius, 1f, dist));
                pixels[y * _resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;
    }

    public void SetColor(Color color)
    {
        _color = color;
        Apply();
    }

    public void SetIntensity(float intensity)
    {
        _intensity = Mathf.Clamp01(intensity);
        Apply();
    }

    private void Apply()
    {
        if(_rawImage == null) return;

        Color c = _color;
        c.a = _intensity;
        _rawImage.color = c;
    }
}
