using UnityEngine;

public class DuckPosition : MonoBehaviour
{
    private bool _ocupied = false;
    public bool Ocupied => _ocupied;
    public void Ocupe()
    {
        _ocupied = true;
    }
}
