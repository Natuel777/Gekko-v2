using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ArrowValueSelector : Selectable
{
    [SerializeField] private TextMeshProUGUI _valueLabel;
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;

    public UnityEvent<int> OnIndexChanged = new UnityEvent<int>();

    private List<string> _options = new List<string>();
    private int _index;

    public int CurrentIndex => _index;

    protected override void Awake()
    {
        base.Awake();
        if (_leftButton) _leftButton.onClick.AddListener(Previous);
        if (_rightButton) _rightButton.onClick.AddListener(Next);
    }

    public void SetOptions(IList<string> options, int startIndex)
    {
        _options = new List<string>(options);
        _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, _options.Count - 1));
        Refresh();
    }

    public void Next()
    {
        if (_options.Count == 0) return;
        _index = (_index + 1) % _options.Count;
        Refresh();
        OnIndexChanged.Invoke(_index);
    }

    public void Previous()
    {
        if (_options.Count == 0) return;
        _index = (_index - 1 + _options.Count) % _options.Count;
        Refresh();
        OnIndexChanged.Invoke(_index);
    }

    private void Refresh()
    {
        if (_valueLabel && _options.Count > 0)
            _valueLabel.text = _options[_index];
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left)
        {
            Previous();
            eventData.Use();
            return;
        }

        if (eventData.moveDir == MoveDirection.Right)
        {
            Next();
            eventData.Use();
            return;
        }

        // Up/Down: fall back to normal explicit navigation between rows.
        base.OnMove(eventData);
    }
}
