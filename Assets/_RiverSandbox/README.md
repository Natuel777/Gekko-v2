# River Sandbox — ríos con spline

**Vitrina con todas las funcionalidades:** `RiverShowcase.unity` (Tools > Gekko > Build River Showcase).
En Play: teclas 1–9 para ir a cada estación; el panel de arriba a la izquierda explica cada una.

Escena de prueba: `RiverSandbox.unity`. Se regenera desde **Tools > Gekko > Build River Sandbox**
(pisa la escena y los materiales generados).

## Cómo usarlo en un nivel
1. Crear un GameObject con `SplineContainer` y dibujar el recorrido.
2. Agregar `SplineRiver` (Add Component > Gekko > River > Spline River), asignar la spline y
   `M_RiverWater`. Si el río va sobre terreno propio, apagar **Generar cauce**.
3. Ancho por tramo: lista **Knot Width Multipliers** (uno por knot). Donde se angosta, corre más rápido.

## Interacción (Play)
- Todo Rigidbody que entra/sale del agua salpica, y deja estela al moverse (ondas en el shader).
- La corriente arrastra Rigidbodies. **El jugador no se arrastra** salvo que se active *Afectar al jugador*.
- `RiverFloater` hace flotar un objeto y lo lleva la corriente.
- Código: `river.Sample(pos, out s)` (altura, dirección y velocidad del agua), `river.AddSplash(pos)`,
  evento `river.Splashed` (para sonido/partículas).

## Controles de la escena de prueba
WASD mover la pelota · Espacio saltar · R reiniciar · Click izq. onda · E soltar objeto ·
Click der. + arrastrar orbitar · Rueda zoom.

## Requisitos
URP Asset con **Depth Texture** y **Opaque Texture** (el PC_RPAsset ya los tiene). Sin Opaque Texture,
destildar *Refraccion* en el material.
