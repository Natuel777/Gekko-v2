using UnityEngine;

// Camara de la escena vitrina (solo sandbox).
//   1..9 / Tab: ir a cada estacion      F: seguir la pelota / volver a la estacion
//   Click der. + arrastrar: orbitar     Rueda: zoom      H: ocultar el panel
//   Click izq.: onda en el agua         E: soltar un objeto donde apunta el mouse
//   La pelota (WASD / Espacio / R) aparece en la estacion elegida.
[AddComponentMenu("Gekko/River/Sandbox/River Showcase Camera")]
public class RiverShowcaseCamera : MonoBehaviour
{
    [SerializeField] RiverShowcaseStation[] _stations;
    [SerializeField] RiverSandboxBall _ball;
    [SerializeField] GameObject[] _dropPrefabs;
    [SerializeField] float _dropHeight = 2.5f;
    [SerializeField] float _orbitSpeed = 4f;
    [SerializeField] float _sharpness = 4f;

    int _index;
    bool _followBall;
    bool _showPanel = true;
    float _yaw, _pitch, _distance;
    Vector3 _focus;
    GUIStyle _titleStyle, _bodyStyle, _listStyle;

    void Start()
    {
        GoTo(0, true);
    }

    void GoTo(int index, bool instant)
    {
        if(_stations == null || _stations.Length == 0)
            return;

        _index = (index + _stations.Length) % _stations.Length;
        RiverShowcaseStation station = _stations[_index];
        _yaw = station.Yaw;
        _pitch = station.Pitch;
        _distance = station.Distance;
        _followBall = false;

        if(_ball != null && station.BallSpawn != null)
            _ball.Teleport(station.BallSpawn.position);

        if(instant)
            _focus = station.transform.position;
    }

    void LateUpdate()
    {
        for(int i = 0; i < 9; i++)
        {
            if(Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                GoTo(i, false);
        }
        if(Input.GetKeyDown(KeyCode.Tab))
            GoTo(_index + 1, false);
        if(Input.GetKeyDown(KeyCode.F))
            _followBall = !_followBall;
        if(Input.GetKeyDown(KeyCode.H))
            _showPanel = !_showPanel;

        bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) + Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        if(moving && !_followBall && _ball != null)
        {
            _followBall = true;
            _distance = Mathf.Min(_distance, 14f);
        }

        if(Input.GetMouseButton(1))
        {
            _yaw += Input.GetAxis("Mouse X") * _orbitSpeed;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * _orbitSpeed, 5f, 89f);
        }
        _distance = Mathf.Clamp(_distance * (1f - Input.mouseScrollDelta.y * 0.1f), 3f, 90f);

        Vector3 target = _followBall && _ball != null
            ? _ball.transform.position
            : (_stations != null && _stations.Length > 0 ? _stations[_index].transform.position : _focus);
        _focus = Vector3.Lerp(_focus, target, 1f - Mathf.Exp(-_sharpness * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);

        if(Input.GetMouseButtonDown(0) && TryGetRiverPoint(out SplineRiver river, out Vector3 point))
            river.AddSplash(point, 1f);
        if(Input.GetKeyDown(KeyCode.E))
            TryDrop();
    }

    bool TryGetRiverPoint(out SplineRiver river, out Vector3 point)
    {
        river = null;
        point = default;
        Camera cam = GetComponent<Camera>();
        if(cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if(!Physics.Raycast(ray, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Collide))
            return false;

        foreach(SplineRiver candidate in SplineRiver.All)
        {
            if(candidate.Sample(hit.point, out SplineRiver.RiverSample sample) && sample.Inside
               && Mathf.Abs(hit.point.y - sample.SurfaceY) < 3f)
            {
                river = candidate;
                var plane = new Plane(Vector3.up, new Vector3(0f, sample.SurfaceY, 0f));
                point = plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : sample.SurfacePoint;
                return true;
            }
        }
        return false;
    }

    void TryDrop()
    {
        if(_dropPrefabs == null || _dropPrefabs.Length == 0 || !TryGetRiverPoint(out _, out Vector3 point))
            return;
        GameObject prefab = _dropPrefabs[Random.Range(0, _dropPrefabs.Length)];
        if(prefab != null)
            Instantiate(prefab, point + Vector3.up * _dropHeight, Random.rotation);
    }

    void OnGUI()
    {
        if(_stations == null || _stations.Length == 0)
            return;

        if(_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            _listStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
        }

        if(!_showPanel)
        {
            GUI.Label(new Rect(12, 8, 300, 24), "H: mostrar panel", _listStyle);
            return;
        }

        RiverShowcaseStation station = _stations[_index];
        float width = Mathf.Min(460f, Screen.width - 24f);
        float bodyHeight = _bodyStyle.CalcHeight(new GUIContent(station.Description), width - 24f);
        GUI.Box(new Rect(12, 12, width, bodyHeight + 60f), GUIContent.none);
        GUI.Label(new Rect(24, 18, width - 24f, 30f), (_index + 1) + ". " + station.Title, _titleStyle);
        GUI.Label(new Rect(24, 50, width - 24f, bodyHeight), station.Description, _bodyStyle);

        float listHeight = _stations.Length * 18f + 120f;
        float y = Screen.height - listHeight - 12f;
        GUI.Box(new Rect(12, y, 330, listHeight), GUIContent.none);
        for(int i = 0; i < _stations.Length; i++)
        {
            string line = (i + 1) + "  " + _stations[i].Title;
            GUI.Label(new Rect(24, y + 8 + i * 18, 310, 20), i == _index ? "<b><color=#7fe0ff>" + line + "</color></b>" : line, _listStyle);
        }
        GUI.Label(new Rect(24, y + 14 + _stations.Length * 18, 310, 100),
            "<b>1-9 / Tab</b> estaciones   <b>F</b> seguir pelota   <b>H</b> panel\n" +
            "<b>WASD</b> pelota   <b>Espacio</b> saltar   <b>R</b> reiniciar\n" +
            "<b>Click izq.</b> onda   <b>E</b> soltar objeto\n" +
            "<b>Click der.</b> orbitar   <b>Rueda</b> zoom", _listStyle);
    }
}
