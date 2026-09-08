using UnityEngine;

public class HeavyBeetleView
{
    private Camera _mainCamera;
    private ParticleSystem _indicatorPS, _purifiedPS, _angryPS;
    private Transform _transform;
    private Material _purifiedMat;
    private SkinnedMeshRenderer[] _skinnedMeshRenderers;

    public HeavyBeetleView(Transform transform)
    {
        if(_mainCamera == null)
            _mainCamera = Camera.main;

        _transform = transform;
    }

    public HeavyBeetleView SetPS(ParticleSystem indicatorPS, ParticleSystem purifiedPS, 
                                ParticleSystem angryPS)
    {
        _indicatorPS = indicatorPS;
        _purifiedPS = purifiedPS;
        _angryPS = angryPS;
        return this;
    }

    public HeavyBeetleView SetMaterials(Material purifiedMat)
    {
        _purifiedMat = purifiedMat;
        return this;
    }

    public HeavyBeetleView SetMeshRenderers(SkinnedMeshRenderer[] skinnedMeshRenderers) 
    {
        _skinnedMeshRenderers = skinnedMeshRenderers;
        return this;
    }

    public void UpdateCanvasPosition()
    {
        CorrectBillboardParticle(_indicatorPS, Vector3.up * 1.76f);
        CorrectBillboardParticle(_purifiedPS, new Vector3(0.031f, 0.99f, 0f));
    }

    private void CorrectBillboardParticle(ParticleSystem ps, Vector3 offset)
    {
        if(ps == null) return;

        if(_mainCamera == null)
            _mainCamera = Camera.main;

        ps.transform.position = _transform.position + offset;

        if(_mainCamera != null)
            ps.transform.rotation = _mainCamera.transform.rotation;
    }

    public void ApplyPurifiedMaterial()
    {
        if(_purifiedMat == null || _skinnedMeshRenderers.Length == 0)  return;

        foreach(SkinnedMeshRenderer skinnedMesh in _skinnedMeshRenderers)
            skinnedMesh.sharedMaterial = _purifiedMat;
    }

    public void SetAngry(bool value)
    {
        if(_angryPS == null) return;

        if(value)
        {
            if(!_angryPS.gameObject.activeSelf)
                _angryPS.gameObject.SetActive(true);
            
            _angryPS.Play();
        }
        
        else
        {
            _angryPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _angryPS.gameObject.SetActive(false);
        }
    }

    public void PlayPurifiedPS()
    {
        //De ser necesario, escalar a un método genérico que consulte una colección de PS
        //Y ejecute aquel que coincida con la KEY pasada por parametro.
        _purifiedPS?.Play();
    }
}
