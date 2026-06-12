using UnityEngine;
using UnityEngine.Animations.Rigging;

public class Spine : MonoBehaviour
{
    [SerializeField] private Transform _neck;      // cuello — el que ya rotás
    [SerializeField] private Transform _col4;      // columna_4 — cerca del cuello
    [SerializeField] private Transform _col3;
    [SerializeField] private Transform _col2;
    [SerializeField] private Transform _col1;      // columna — más lejos

    [SerializeField] private float _followSpeed = 5f;
    [SerializeField] private float _falloff = 0.7f; // cuánto reduce cada hueso (0-1)

    private Quaternion _lastNeckRot;

    private void Start()
    {
        _lastNeckRot = _neck.rotation;
    }

    private void LateUpdate()
    {
        if (_lastNeckRot == _neck.rotation) return;
        // Delta de rotación del cuello este frame
        Quaternion neckDelta = _neck.rotation * Quaternion.Inverse(_lastNeckRot);
        _lastNeckRot = _neck.rotation;

        // Cada hueso aplica una fracción del delta
        //ApplyDelta(_col4, neckDelta, Mathf.Pow(_falloff, 1));
        //ApplyDelta(_col3, neckDelta, Mathf.Pow(_falloff, 2));
        //ApplyDelta(_col2, neckDelta, Mathf.Pow(_falloff, 3));
        //ApplyDelta(_col1, neckDelta, Mathf.Pow(_falloff, 4));

        //Vector3 frontNormal = (_legs.RASurfaceNormal + _legs.LASurfaceNormal).normalized;
        //    //if (frontNormal == Vector3.zero) return;
        //    //
        //    //// Solo rotamos el hueso para que su up apunte a la normal
        //    //// sin tocar el forward (eje rojo bloqueado)
        //    //Quaternion targetRot = Quaternion.FromToRotation(_neckBone.up, frontNormal) * _neckBone.rotation;
        //    //
        //    //_neckBone.rotation = Quaternion.Slerp(_neckBone.rotation, targetRot, _adaptSpeed * Time.deltaTime);
        //    //_neckBone.localRotation = new Quaternion(_neckBone.localRotation.x, 0, 0, 1);
    }

    private void ApplyDelta(Transform bone, Quaternion delta, float weight)
    {
        // Interpolamos el delta según el peso
        Quaternion weightedDelta = Quaternion.Slerp(Quaternion.identity, delta, weight);
        bone.rotation = weightedDelta * bone.rotation;
    }

}
