using UnityEngine;

// La condición de "Gekko está en el agua". Vivía suelta dentro de PlayerController.OnWater (que la usa para sacarle la
// fricción al collider) y ahora está acá, para que la compartan las demás mecánicas de Gekko sin repetir la layer ni el
// alcance del raycast: hoy la usan PlayerController (fricción), GekkoDetection (no detecta ganchos en el agua) y
// GekkoGrapple (no hay segundo salto en el agua).
//
// El agua del juego es de tres tipos:
//   - Con collider sólido en la layer Water puesto a mano (lakeCol, pushingWater...): Gekko camina encima y el raycast de
//     IsOnWaterSurface lo ve.
//   - Un SplineRiver con RiverSolidSurface (LvlTwo): arma ese mismo collider a partir del propio río, así que siempre
//     coincide con el agua que se ve.
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
    // WaterZone, o nadando. Es lo que usan las mecánicas que no se pueden hacer en el agua.
    // PlayerSwimming hace falta porque, nadando, Gekko está DEBAJO de la cara de arriba del collider de agua: el raycast de
    // IsOnWaterSurface arranca adentro del collider y no lo ve.
    public static bool IsInWater(Transform gekko)
    {
        return PlayerSwimming || IsOnWaterSurface(gekko) || WaterZone.Contains(gekko.position);
    }

    // Lo prende y apaga GekkoSwimming.
    public static bool PlayerSwimming { get; set; }

    // ---------------------------------------------------------------------------------------------------------------------
    // Superficie del agua en un punto, para el nado (GekkoSwimming) y el efecto de cámara bajo el agua.
    // ---------------------------------------------------------------------------------------------------------------------

    public struct WaterInfo
    {
        public float SurfaceY;
        public Vector3 Normal;      // normal de la superficie: en una cascada o tobogán es casi horizontal
        public Vector3 Current;     // corriente en m/s (horizontal o siguiendo el río)
        public bool CanDive;        // dentro de un DeepWaterZone
        public float BottomY;       // hasta dónde se puede bajar buceando (solo vale con CanDive)
        public bool NoSwim;         // agua que no se nada (WaterZone con "Se puede nadar" apagado): se hunde, como antes
        public bool OutsideSwimZone; // NoSwim porque es un río que se nada solo en sus SwimZone y el punto está afuera
        public float CurrentFactor; // cuánto arrastra la corriente en esta agua; negativo = el valor general de SwimSettings
        public bool IsFlat(float maxSlopeDeg) => Vector3.Angle(Normal, Vector3.up) <= maxSlopeDeg;
    }

    // Busca agua en el punto, de cualquiera de los tipos. reachAbove: cuánto puede estar el punto POR ENCIMA de la superficie
    // y seguir contando (para detectar a Gekko justo cuando la toca). reachBelow: cuánto puede estar POR DEBAJO.
    // Orden: DeepWaterZone (manda para bucear), SplineRiver, colliders sólidos de la layer Water y WaterZone.
    public static bool TryGetSurface(Vector3 point, float reachAbove, float reachBelow, out WaterInfo info)
    {
        info = new WaterInfo { Normal = Vector3.up, CurrentFactor = -1f };

        // 1) Agua profunda marcada a mano: la única donde se bucea.
        if(DeepWaterZone.TryFind(point, reachAbove, out DeepWaterZone zone))
        {
            info.SurfaceY = zone.SurfaceY;
            info.BottomY = zone.BottomY;
            info.Current = zone.Current;
            info.CanDive = true;
            return true;
        }

        // 2) Ríos. La superficie y la corriente salen del propio río, así que acompañan curvas y cascadas.
        // Un río nadable gana sobre uno que no lo es (el río de fondo de LvlTwo es enorme y pasa por debajo de otros): el que
        // no se nada solo se devuelve si no hay otro.
        var rivers = SplineRiver.All;
        bool foundNoSwim = false;
        WaterInfo noSwimInfo = default;
        for(int i = 0; i < rivers.Count; i++)
        {
            if(rivers[i] == null || !rivers[i].Sample(point, out SplineRiver.RiverSample sample)) continue;
            if(!sample.InsideWithMargin(0.1f)) continue;

            float dy = point.y - sample.SurfaceY;
            if(dy > reachAbove || dy < -reachBelow) continue;

            info.SurfaceY = sample.SurfaceY;
            // Normal aproximada: perpendicular a la corriente y al ancho. En lo plano da casi (0,1,0); en una cascada se acuesta.
            Vector3 normal = Vector3.Cross(sample.FlowDirection, sample.Right);
            info.Normal = normal.sqrMagnitude > 1e-6f ? (normal.y < 0f ? -normal : normal).normalized : Vector3.up;
            info.Current = sample.FlowVelocity;
            // Ríos de decoración / de fondo: se camina sobre su superficie sólida, no se nadan.
            bool hasSolid = rivers[i].TryGetComponent(out RiverSolidSurface solid);
            info.NoSwim = hasSolid && !solid.Swimmable;
            info.CurrentFactor = hasSolid ? solid.SwimCurrentFactor : -1f;
            // Ríos que se nadan solo en sus pozones (SwimZone): fuera de ellos, como uno que no se nada. Adentro, el pozón es agua
            // mansa: su propia corriente.
            if(hasSolid && solid.Swimmable && solid.SwimOnlyInZones)
            {
                if(!SwimZone.TryFind(point, out SwimZone swimZone))
                {
                    info.NoSwim = true;
                    info.OutsideSwimZone = true;
                }
                else if(swimZone.CurrentFactor >= 0f) info.CurrentFactor = swimZone.CurrentFactor;
            }
            // Buceo en el río: hasta el fondo de su agua (volumen o cauce), un poco antes para no salirse por abajo.
            if(hasSolid && solid.CanDive)
            {
                info.CanDive = true;
                info.BottomY = sample.SurfaceY - rivers[i].WaterDepth + 0.1f;
            }
            if(!info.NoSwim) return true;

            if(!foundNoSwim) { foundNoSwim = true; noSwimInfo = info; }
            info = new WaterInfo { Normal = Vector3.up, CurrentFactor = -1f };
        }

        if(foundNoSwim)
        {
            info = noSwimInfo;
            return true;
        }

        // 3) Colliders sólidos de agua (lakeCol, pushingWater...): un rayo desde arriba encuentra la cara de arriba. Busca una
        // superficie entre reachBelow por encima del punto (el punto bajo el agua) y reachAbove por debajo (el punto encima).
        Vector3 origin = point + Vector3.up * reachBelow;
        if(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reachAbove + reachBelow, 1 << WaterLayer, QueryTriggerInteraction.Ignore))
        {
            info.SurfaceY = hit.point.y;
            info.Normal = hit.normal;
            // Los pushingWater empujan en una dirección fija: nadando eso se vuelve una corriente suave en el mismo sentido.
            if(hit.collider.TryGetComponent(out PushPlatform push) && push.Direction != Vector3.zero)
                info.Current = push.Direction.normalized * PushCurrentSpeed;
            return true;
        }

        // 4) Agua visual sin collider (WaterZone).
        if(WaterZone.TryGetSurface(point, out float surfaceY, out Vector3 zoneNormal, out float zoneDepth, out bool swimmable))
        {
            float dy = point.y - surfaceY;
            if(dy <= reachAbove && dy >= -Mathf.Min(reachBelow, Mathf.Max(zoneDepth, 0.5f)))
            {
                info.SurfaceY = surfaceY;
                info.Normal = zoneNormal;
                info.NoSwim = !swimmable;
                return true;
            }
        }

        return false;
    }

    // Velocidad (m/s) de la corriente de un pushingWater mientras Gekko nada en él.
    private const float PushCurrentSpeed = 2f;
}
