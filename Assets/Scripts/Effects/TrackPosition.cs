using UnityEngine;

public class TrackPosition : MonoBehaviour
{
    private Material grassMat;
    private GameObject tracker;

    void Start()
    {
        grassMat = GetComponent<Renderer>().material;
        tracker = GameObject.Find("pj");
    }

    void Update()
    {
        if(tracker == null) return;

        Vector3 trackerPos = tracker.GetComponent<Transform>().position;
        grassMat.SetVector("_trackerPosition", trackerPos);
    }
}
