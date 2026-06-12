using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    Stack<IScreen> _stack;

    static public ScreenManager Instance;

    public AudioSource ButtonSound;
    public bool CanPause;

    void Awake()
    {
        Instance = this;

        _stack = new Stack<IScreen>();
    }

    public void Pop()
    {
        if (_stack.Count <= 1) return;

        _stack.Pop().Free();

        if (_stack.Count > 0)
        {
            _stack.Peek().Activate();
        }
    }

    public void Push(IScreen screen)
    {
        if (_stack.Count > 0)
        {
            _stack.Peek().Deactivate();
        }
        _stack.Push(screen);

        screen.Activate();
    }

    public void Push(string resource)
    {
        var go = Instantiate(Resources.Load<Screens>(resource));

        Push(go.GetComponent<IScreen>());
    }
  
}
