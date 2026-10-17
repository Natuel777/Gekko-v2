using UnityEngine;
[System.Serializable]
public struct SimonRounds
{
    //[SerializeField] private float _speed;
    [SerializeField] private int[] _order;

    public int[] Order => _order;
}
