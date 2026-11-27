using System.Collections;
using UnityEngine;

// Gekko que vive en una casa de la Aldea. Arranca escondido y aparece cuando el jugador completa el nivel que le
// toca (lo maneja AldeaIntroSequence, en orden de la lista "Casas").
//  - MostrarInstantaneo(): ya había aparecido en una visita anterior, queda en su casa desde el primer frame.
//  - Aparecer(): la aparición de esta visita (crece con un rebote y festeja).
public class AldeaHabitante : MonoBehaviour
{
    [Tooltip("Gekko rescatado que vive acá (mismo nombre que registra FriendGekkoDialogue, ej. Celeste, Moki). " +
             "Con nombre: aparece solo si ese gekko fue rescatado. Vacío = aparece según el nivel ganado (orden de Casas).")]
    [SerializeField] private string _rescateId;
    [Tooltip("Lo que se prende/apaga. Vacío = este mismo GameObject.")]
    [SerializeField] private GameObject _visual;
    [Tooltip("Partículas opcionales al aparecer (ej. destello, hojas).")]
    [SerializeField] private ParticleSystem _particulasAparicion;

    [Header("Aparición")]
    [SerializeField, Min(0.05f)] private float _duracion = 0.6f;
    [Tooltip("Cuánto se pasa de su tamaño antes de asentarse (0 = sin rebote).")]
    [SerializeField, Range(0f, 1f)] private float _rebote = 0.25f;
    [SerializeField] private bool _festejar = true;

    private Vector3 _escalaOriginal;
    private bool _inicializado;

    public bool Visible { get; private set; }

    public string RescateId => _rescateId;
    public bool DependeDeRescate => !string.IsNullOrWhiteSpace(_rescateId);

    private GameObject Visual => _visual != null ? _visual : gameObject;

    private void Init()
    {
        if(_inicializado) return;
        _inicializado = true;
        _escalaOriginal = Visual.transform.localScale;
    }

    public void Esconder()
    {
        Init();
        Visible = false;
        Visual.SetActive(false);
    }

    public void MostrarInstantaneo()
    {
        Init();
        Visible = true;
        Visual.transform.localScale = _escalaOriginal;
        Visual.SetActive(true);
    }

    public IEnumerator Aparecer()
    {
        Init();
        Visible = true;
        Transform t = Visual.transform;
        t.localScale = Vector3.zero;
        Visual.SetActive(true);

        if(_particulasAparicion != null) _particulasAparicion.Play();

        float k = 0f;
        while(k < 1f)
        {
            k = Mathf.Min(1f, k + Time.deltaTime / _duracion);
            t.localScale = _escalaOriginal * EaseOutBack(k, _rebote);
            yield return null;
        }
        t.localScale = _escalaOriginal;

        if(_festejar)
        {
            PlayerViewer viewer = Visual.GetComponentInChildren<PlayerViewer>();
            Animator anim = viewer != null ? viewer.GetComponent<Animator>() : null;
            if(anim != null && anim.runtimeAnimatorController != null) viewer.Celebrate();
        }
    }

    // Curva que se pasa un poco de 1 y vuelve (rebote suave).
    private static float EaseOutBack(float x, float rebote)
    {
        float c1 = 1.70158f * rebote * 4f;
        float c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
