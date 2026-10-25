using System;
using UnityEngine;

public class SimonPiece : MonoBehaviour
{
    private int _id;
    public event Action<int> OnPressed;
    public void SetNumber(int num) =>  _id = num;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<Player>())
        {
            OnPressed?.Invoke(_id);
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.GetComponent<Player>())
        {
            GetComponent<Renderer>().material.color = Color.white;
        }
    }
}
