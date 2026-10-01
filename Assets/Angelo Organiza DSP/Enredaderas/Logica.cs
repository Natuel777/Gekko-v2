using UnityEngine;

public class Logica : MonoBehaviour
{
    [SerializeField] private Animator enredaderaAnimator;
    [SerializeField] private GameObject enredadera;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            ActivarEnredadera();
        }
    }

    private void ActivarEnredadera()
    {
        enredadera.SetActive(true);
    }
}
