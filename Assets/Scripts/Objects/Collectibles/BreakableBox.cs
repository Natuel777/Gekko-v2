using System.Collections;
using UnityEngine;

public class BreakableBox : MonoBehaviour, IDamageable
{
    [SerializeField] private float _life = 2;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private Transform _spawn;
    [SerializeField] private int _minQuant;
    [SerializeField] private int _maxQuant;
    [SerializeField] private float _upForceMin = 4f;
    [SerializeField] private float _upForceMax = 7f;
    [SerializeField] private float _sideForceMin = 1.5f;
    [SerializeField] private float _sideForceMax = 3.5f;
    public void Damage(float dmg)
    {
        StartCoroutine(Anim());
        _life--;
    }
    private IEnumerator Anim()
    {
        if (_life <= 0) yield break;
        yield return null;
        int quantity = Random.Range(_minQuant, _maxQuant + 1); // el máximo es exclusivo en int, por eso el +1
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlankPlaced);
        for (int i = 0; i < quantity; i++)
        {
            GameObject prefab = InstantiatePrefab();
            Rigidbody rb = prefab.GetComponent<Rigidbody>();

            // Dirección horizontal aleatoria
            float angle = (360f / quantity) * i + Random.Range(-15f, 15f);
            Vector3 horizontal = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Hacia arriba + hacia un costado
            Vector3 force = Vector3.up * Random.Range(_upForceMin, _upForceMax)
                          + horizontal * Random.Range(_sideForceMin, _sideForceMax);

            rb.AddForce(force, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse); // giro para que se vea natural
        }
        if (_life <= 0) Destroy(gameObject);

    }
    private GameObject InstantiatePrefab()
    {
        return Instantiate(_prefab, _spawn.position, _spawn.rotation);
    }

}
