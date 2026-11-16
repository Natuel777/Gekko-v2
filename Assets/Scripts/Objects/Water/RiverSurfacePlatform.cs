using UnityEngine;

// Suelo del agua de un SplineRiver con RiverSolidSurface. Hace lo mismo que PushPlatform (los pushingWater de LvlOne): Gekko
// resbala (SlipperyPlatform) y la corriente lo arrastra. La diferencia es que el empuje sigue el sentido real del río en el
// punto donde está parado, no una dirección fija: acompaña las curvas y empuja cuesta abajo en las cascadas.
// Lo crea RiverSolidSurface; no se agrega a mano.
public class RiverSurfacePlatform : SlipperyPlatform
{
    private SplineRiver _river;

    public void Init(SplineRiver river, float currentForce)
    {
        _river = river;
        _slideForce = currentForce;
    }

    protected override void OnCollisionStay(Collision col)
    {
        if(_river == null || _slideForce <= 0f) return;

        // Igual que PushPlatform: colgado de la lengua o trepando, la corriente no lo arrastra.
        if(col.gameObject.TryGetComponent(out Player pj))
        {
            if(pj.Swinging != null && pj.Swinging.IsSwinging) return;
            if(pj.PjController != null && pj.PjController.Climbing) return;
        }

        // A los objetos con RiverFloater ya los lleva el propio río.
        if(col.gameObject.TryGetComponent(out RiverFloater _)) return;

        if(!col.gameObject.TryGetComponent(out Rigidbody rb)) return;

        if(_river.Sample(rb.worldCenterOfMass, out SplineRiver.RiverSample sample))
            rb.AddForce(sample.FlowDirection * _slideForce, ForceMode.Acceleration);
    }
}
