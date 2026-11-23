using UnityEngine;

// Haz de luz vertical que marca los ganchos (GrapplePoint = bellota, Gancho = mariquita) para que se noten de lejos.
// Va en la raíz de cada prefab, con un GrappleHighlightDataSO por tipo (color, alto, ancho, intensidad). El haz usa el shader
// Gekko/FX/Light Beam: siempre mira a la cámara, se apaga hacia arriba y se desvanece al acercarse para no tapar la vista.
public class GrappleHighlight : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int HeightId = Shader.PropertyToID("_Height");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int FadeStartId = Shader.PropertyToID("_NearFadeStart");
    private static readonly int FadeEndId = Shader.PropertyToID("_NearFadeEnd");

    // Hijo ancla del gancho (el punto al que se engancha la lengua): de ahí sale el haz.
    private const string AnchorName = "GrapplePoint";

    private static Mesh _beamMesh;

    [SerializeField] private GrappleHighlightDataSO _data;

    private Material _beamMaterial;

    private void Awake()
    {
        if(_data == null || _data.beamShader == null || _data.beamHeight <= 0f) return;

        CreateBeam();
    }

    private void OnDestroy()
    {
        if(_beamMaterial != null) Destroy(_beamMaterial);
    }

    private void CreateBeam()
    {
        Transform anchor = transform.Find(AnchorName);

        var go = new GameObject("__Beam");
        go.transform.SetParent(transform, false);
        go.transform.position = anchor != null ? anchor.position : transform.position;
        go.transform.rotation = Quaternion.identity;
        go.layer = 2; // Ignore Raycast

        // El shader arma el tamaño en mundo: la escala del padre no tiene que influir.
        Vector3 lossy = transform.lossyScale;
        go.transform.localScale = new Vector3(SafeInverse(lossy.x), SafeInverse(lossy.y), SafeInverse(lossy.z));

        go.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _beamMaterial = new Material(_data.beamShader) { name = "LightBeam (" + name + ")" };
        _beamMaterial.SetColor(ColorId, _data.color);
        _beamMaterial.SetFloat(IntensityId, _data.beamIntensity);
        _beamMaterial.SetFloat(HeightId, _data.beamHeight);
        _beamMaterial.SetFloat(WidthId, _data.beamWidth);
        _beamMaterial.SetFloat(FadeStartId, _data.beamFadeStart);
        _beamMaterial.SetFloat(FadeEndId, _data.beamFadeEnd);
        mr.sharedMaterial = _beamMaterial;
    }

    // Quad de -0.5..0.5 en X y 0..1 en Y; los bounds cubren lo que el shader puede ocupar al girarlo hacia la cámara.
    private static Mesh BeamMesh()
    {
        if(_beamMesh != null) return _beamMesh;

        _beamMesh = new Mesh { name = "LightBeamQuad" };
        _beamMesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
        _beamMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        _beamMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        _beamMesh.bounds = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(10f, 10f, 10f));
        return _beamMesh;
    }

    private static float SafeInverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
}
