using UnityEngine;

// La condición de "Gekko está en el agua". Vivía suelta dentro de PlayerController.OnWater (que la usa para sacarle la
// fricción al collider) y ahora está acá, para que la compartan las demás mecánicas de Gekko sin repetir la layer ni el
// alcance del raycast: hoy la usan PlayerController (fricción), GekkoDetection (no detecta ganchos en el agua) y
// GekkoGrapple (no hay segundo salto en el agua).
//
// El agua del juego es de dos tipos:
//   - Con collider sólido en la layer Water (lakeCol, pushingWater...): Gekko camina encima y el raycast de
//     IsOnWaterSurface lo ve.
//   - Visual, sin collider (los planos y las cascadas de LvlTwo, por ejemplo): se marca con un WaterZone.
public static class GekkoWater
{
    // Layer Water (la 4, la que trae Unity de fábrica). Un collider de agua que no esté en esa layer, o que sea un
    // trigger, no lo ve IsOnWaterSurface.
    public const int WaterLayer = 4;

    // Gekko está sobre un collider sólido de la layer Water: hay uno debajo, a 1 m o menos de sus pies. Los metros valen
    // para un Gekko de escala 1 y se escalan con su tamaño real. Es un raycast corto, así que se puede llamar en cada
    // frame. Es lo que usa PlayerController para sacarle la fricción: no incluye las WaterZone.
    public static bool IsOnWaterSurface(Transform gekko)
    {
        float scale = gekko.lossyScale.x;
        return Physics.Raycast(gekko.position, Vector3.down, 1f * scale, 1 << WaterLayer, QueryTriggerInteraction.Ignore);
    }
    public static bool IsOnWaterSurface(Vector3 gekko, float scale)
    {
        return Physics.Raycast(gekko, Vector3.down, 1f * scale, 1 << WaterLayer, QueryTriggerInteraction.Ignore);
    }

    // Gekko está en el agua, de cualquiera de los dos tipos: sobre un collider de la layer Water o dentro de un
    // WaterZone. Es lo que usan las mecánicas que no se pueden hacer en el agua.
    public static bool IsInWater(Transform gekko)
    {
        return IsOnWaterSurface(gekko) || WaterZone.Contains(gekko.position);
    }
}
