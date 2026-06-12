using UnityEngine;

public class XRayFollow : MonoBehaviour
{
    public static int PosID = Shader.PropertyToID("_PlayerPos");
    public static int SizeID = Shader.PropertyToID("_Size");

    public Material WoodMat;
    public Material LeavesMat;
    public Material PineLeavesMat;
    public Camera cam;
    public LayerMask mask;

    private void Update()
    {
        var dir = cam.transform.position - transform.position;
        var ray = new Ray(transform.position, dir.normalized);

        if (Physics.Raycast(ray, 3000f, mask))
        {
            WoodMat.SetFloat(SizeID, 1);
            LeavesMat.SetFloat(SizeID, 1);
            PineLeavesMat.SetFloat(SizeID, 1);
        }
        else
        {
            WoodMat.SetFloat(SizeID, 0);
            LeavesMat.SetFloat(SizeID, 0);
            PineLeavesMat.SetFloat(SizeID, 0);
        }

        var view = cam.WorldToViewportPoint(transform.position);
        WoodMat.SetVector("_PlayerPos", view);
    }
}
