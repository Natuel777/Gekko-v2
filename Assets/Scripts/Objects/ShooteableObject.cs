using UnityEngine;

public class ShooteableObject : MonoBehaviour
{
    public ShooteableObjectDataSO data;
    [SerializeField] private string _name;

    public string Name => _name;
}
