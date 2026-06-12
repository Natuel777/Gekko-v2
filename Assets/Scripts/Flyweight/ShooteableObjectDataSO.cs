using UnityEngine;

[CreateAssetMenu(fileName = "ShooteableObjectDataSO", menuName = "Scriptable Objects/ShooteableObjectDataSO")]
public class ShooteableObjectDataSO : ScriptableObject
{
    public float speed;
    public float damage = 10f;
}
