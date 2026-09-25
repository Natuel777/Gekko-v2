using System.Collections;
using UnityEngine;


public class LittleBarricade : MonoBehaviour,IDamageable
{
    [SerializeField] private float _life = 1;
    [SerializeField] private MeshRenderer _head;
    [SerializeField] private GameObject _barricade;
    [SerializeField] private float _fadeDuration = 3f;
    [SerializeField] private ParticleSystem _particlePurification;

    public void Damage(float dmg)
    {
        _life -= dmg;

        if(_life <=0)
        {
            _particlePurification.Play();
            GetComponent<Collider>().enabled = false;
            _head.enabled = false;
            StartCoroutine(Disapear());
        }
    }
    private IEnumerator Disapear()
    {

        MeshRenderer[] renderers = _barricade.GetComponentsInChildren<MeshRenderer>();

        Material[] materials = new Material[0];
        var materialList = new System.Collections.Generic.List<Material>();

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials) // .materials (plural) instancia automáticamente
            {
                materialList.Add(mat);
            }
        }
        materials = materialList.ToArray();

        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeDuration);

            foreach (var mat in materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = alpha;
                    mat.color = c;
                }
            }

            yield return null;
        }

        _barricade.SetActive(false);
    }
} 
