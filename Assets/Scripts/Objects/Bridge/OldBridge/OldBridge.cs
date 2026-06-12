using UnityEngine;

public class OldBridge : MonoBehaviour
{
    [SerializeField] private OldBridge_Plank[] _planks;
    [SerializeField] private Collider _ObstacleCol;

    public void PlankPositioned()
    {
        bool finish = true;
        for (int i = 0; i < _planks.Length; i++)
        {
            if (!_planks[i].OnPosition) finish = false;
        }
        if (finish)
        {
            _ObstacleCol.enabled = false;
            LevelOneManager.Instance.BridgeFinish();
        }
    }
}
