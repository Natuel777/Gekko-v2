using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class SelectRelay : MonoBehaviour, ISelectHandler
{
    [SerializeField] private UnityEvent _onSelected;

    public void OnSelect(BaseEventData eventData)
    {
        _onSelected.Invoke();
    }
}
