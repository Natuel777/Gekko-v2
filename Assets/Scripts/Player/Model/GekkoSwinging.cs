using UnityEngine;

public class GekkoSwinging : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    [SerializeField] private Transform _tongueMuzzle;
    [SerializeField] private LayerMask _grappableLayers;

    private void Awake() 
    {    
        _lineRenderer = GetComponent<LineRenderer>();
    }
}
