using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnTest : MonoBehaviour
{

    public GameObject FirePoint;
    public List<GameObject> vfx = new List<GameObject>();

    private GameObject effectToSpawn;
    public GameObject muzzlePrefab;
    public GameObject muzzleSpawn;

    void Start()
    {
        effectToSpawn = vfx[0];
    }


    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            if (muzzlePrefab != null)
            {
                var muzzleVFX = Instantiate(muzzlePrefab, muzzleSpawn.transform.position, Quaternion.identity);
                muzzleVFX.transform.forward = muzzleSpawn.transform.forward;
            }
            SpawnVFX();
        }
    }

    void SpawnVFX ()
    {
        GameObject vfx;

        if(FirePoint != null)
        {
            vfx = Instantiate(effectToSpawn, FirePoint.transform.position, Quaternion.identity);
        }
        else
        { 
            Debug.Log("No Fire Point");
        }
    }
}
