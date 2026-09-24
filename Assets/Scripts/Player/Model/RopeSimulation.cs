using UnityEngine;

// =====================================================================================================================
//  RopeSimulation: la soga que se ve cuando Gekko se cuelga de un punto
// =====================================================================================================================
//
//  QUÉ ES
//  ------
//  Cuando Gekko dispara la lengua y se engancha, se dibuja una soga entre su boca y el punto de anclaje. Antes esa
//  soga era una línea recta. Esta clase la convierte en una soga de verdad: cuelga por su propio peso, se queda atrás
//  cuando Gekko se mueve, rebota cuando frena y se va asentando sola.
//
//  Es SOLO VISUAL: únicamente calcula posiciones para dibujar el LineRenderer. No toca el Rigidbody de Gekko ni el
//  SpringJoint, así que no puede cambiar cómo se juega ni empujar a nadie.
//
//
//  LA IDEA: UN COLLAR DE CUENTAS
//  -----------------------------
//  La soga no es una curva: es una fila de PUNTOS (las cuentas) unidos por HILOS de largo fijo.
//  Ejemplo con quality = 10 (10 hilos, 11 puntos):
//
//        P0  P1  P2  P3  P4  P5  P6  P7  P8  P9  P10
//        o---o---o---o---o---o---o---o---o---o---o
//
//     P0        = la lengua de Gekko. Está CLAVADA: la posición la manda Gekko.
//     P1 a P9   = los puntos LIBRES. Son los únicos que la simulación mueve.
//     P10       = el ancla (el punto de enganche). Está CLAVADA: queda fija.
//     cada "---" = un hilo. Siempre mide lo mismo.
//
//    - Siempre hay un punto más que hilos. Es la misma cantidad de puntos que tiene el LineRenderer, así que dibujar
//      es copiar los puntos uno por uno.
//    - Las dos PUNTAS (primer y último punto) no se simulan: se clavan donde diga GekkoSwinging.
//
//
//  LAS 3 REGLAS (se repiten en cada paso de tiempo, siempre en este orden)
//  ----------------------------------------------------------------------
//  1) CLAVAR LAS PUNTAS  ->  PinEnds()
//     La lengua se pone donde esté la boca de Gekko y el ancla donde esté el punto de enganche.
//
//  2) MOVER LOS PUNTOS LIBRES  ->  Integrate()
//     Cada punto de en medio sigue moviéndose como venía (inercia) y además cae un poco por la gravedad.
//     El truco de Verlet: en vez de guardar la velocidad de cada punto, se guarda DÓNDE ESTABA en el paso anterior, y
//     la velocidad sale de restar:   velocidad = posición actual - posición anterior.
//        Ejemplo: un punto estaba en x = 1.00 y ahora está en x = 1.10. Se mueve 0.10 por paso, así que el próximo
//                 paso lo lleva a x = 1.20 (más lo que baje por la gravedad).
//     Guardar posiciones y no velocidades tiene una ventaja enorme: cuando la regla 3 mueve un punto, su velocidad se
//     corrige sola (porque cambió su posición) y no hay que acordarse de tocar nada más. Por eso es tan estable.
//
//  3) QUE LOS HILOS NO SE ESTIREN  ->  SolveConstraints()
//     Después de mover los puntos, algunos hilos quedan más largos o más cortos de lo que deberían. Se recorre cada
//     hilo y se acercan (o se alejan) sus dos puntos hasta que vuelva a medir lo justo.
//
//          antes:     o-------------o      mide 0.60, pero debería medir 0.50 (está estirado)
//                       ->         <-      los dos puntos se acercan 0.05 cada uno
//          después:     o-----------o      mide 0.50
//
//     Si uno de los dos puntos es una punta clavada (la lengua o el ancla), no se puede mover: el otro punto se mueve
//     TODO lo necesario.
//     Arreglar un hilo desarregla a sus vecinos (comparten un punto), como cuando se ajusta una cadena de a un eslabón.
//     Por eso se repite el recorrido completo varias veces (iterations): en cada pasada la cadena queda más cerca de
//     cumplir todas las medidas a la vez.
//
//
//  POR QUÉ SE MUEVE COMO UNA SOGA (sin programar cada efecto)
//  ----------------------------------------------------------
//  Si Gekko se balancea hacia la izquierda, la lengua (P0) se corre a la izquierda. Nadie le dice a la soga que siga
//  a Gekko: pasa sola.
//     1. El hilo P0-P1 queda estirado, así que la regla 3 arrastra a P1 hacia la izquierda.
//     2. P1 arrastra a P2, P2 a P3, y así hasta el ancla. Pero cada punto tiene además su propia inercia y su peso, así
//        que reacciona un poco tarde: la soga forma una curva que "se queda atrás" del movimiento.
//     3. Si Gekko frena, los puntos siguen un poco por inercia, se pasan y vuelven: la soga rebota.
//     4. El damping les va quitando energía y la soga se asienta.
//  Izquierda, derecha, adelante o atrás da lo mismo: son posiciones 3D. Alcanza con que la lengua cambie de lugar; no
//  hace falta leer el input ni la velocidad del Rigidbody.
//
//
//  EL LARGO DE LA SOGA Y POR QUÉ CUELGA
//  ------------------------------------
//  Quien la usa (GekkoSwinging) le dice cuánto mide la soga en reposo (restLength). Si mide MÁS que la distancia entre
//  las puntas, le sobra soga y cuelga formando una panza. Si mide igual, queda recta como una barra.
//     Ejemplo: puntas a 5 m y restLength = 5.25 m -> sobran 25 cm de soga, que se ven como una panza de ~0.7 m.
//
//
//  EL TIEMPO: POR QUÉ HAY "SUBPASOS"
//  ---------------------------------
//  La simulación anda mejor con pasos de tiempo chicos y parecidos, pero cada frame dura distinto:
//     144 fps -> el frame dura ~7 ms   -> se hace UN paso de 7 ms.
//      60 fps -> el frame dura ~17 ms  -> se hace UN paso de 17 ms.
//      30 fps -> el frame dura ~33 ms  -> se hacen DOS subpasos de ~17 ms (para no perder precisión).
//  Siempre hay al menos un paso por frame, así la soga se mueve suave en cada frame y no a los saltos. Como los pasos
//  no duran todos lo mismo, Integrate() corrige la velocidad según cuánto duró el paso anterior y ajusta el damping al
//  largo de cada paso. Resultado: la soga se comporta igual a 30, 60 o 144 fps.
//
//
//  CÓMO SE USA
//  -----------
//     var rope = new RopeSimulation(quality, damping, gravity, iterations);   // 1. una sola vez, al crear el sistema
//     rope.Reset(posicionDeLaLengua);                                         // 2. al engancharse: soga colapsada y quieta
//     rope.Simulate(Time.deltaTime, lengua, ancla, largoEnReposo);            // 3. CADA frame mientras se está colgado
//     rope.Points[i]                                                          // 4. posiciones para dibujar (0 = lengua)
//
//
//  QUÉ CAMBIA CADA PARÁMETRO
//  -------------------------
//     quality     (hilos)     Más = curva más suave y un poco más de costo. 10 alcanza de sobra.
//     damping     (0.9 a 1)   1 = la soga nunca pierde energía y se balancea para siempre. 0.99 = se asienta en unos
//                             segundos. Menos = se frena enseguida.
//     gravity     (m/s²)      Cuánto pesa. 9.81 = peso real. Menos = más liviana, como una lengua.
//     iterations  (pasadas)   Más = más rígida (no se estira). Menos = más elástica, como una goma. 8 va bien.
//
//
//  LÍMITES (lo que NO hace)
//  ------------------------
//   - No choca con el escenario: si la soga pasa por una pared, la atraviesa.
//   - No influye en el gameplay: la física real del swing sigue siendo el SpringJoint de GekkoSwinging.
// =====================================================================================================================
public sealed class RopeSimulation
{
    // ==== CONSTANTES ================================================================================================

    // "Paso de referencia": 1/60 de segundo, el tamaño de paso con el que se pensó la simulación. Se usa para:
    //   a) decidir cuántos subpasos hacer cuando un frame dura mucho (ver Simulate).
    //   b) medir el damping: "pierde el 1% de la energía cada 1/60 s".
    private const float StepTime = 1f / 60f;

    // Máximo de tiempo que se simula en un solo frame. Si el juego se congela (carga de escena, breakpoint), el frame
    // siguiente podría durar 2 segundos y habría que simular 120 pasos de golpe. Con este tope se simulan como mucho
    // 0.1 s (unos 6 pasos) y la soga simplemente se atrasa un instante.
    private const float MaxFrameTime = 0.1f;

    // Distancia mínima entre dos puntos para poder corregir el hilo que los une. Si están (casi) en el mismo lugar no
    // hay una "dirección" hacia la cual separarlos, y calcularla implicaría dividir por ~0: daría NaN y la soga
    // desaparecería.
    private const float MinDistance = 1e-5f;

    // ==== ESTADO DE LA SOGA =========================================================================================

    // Las DOS posiciones de cada punto (el truco de Verlet):
    //   _currentPos[i]  = dónde está el punto i ahora.
    //   _previousPos[i] = dónde estaba en el paso anterior.
    // La diferencia entre las dos es su velocidad. Ejemplo: antes en x = 1.00 y ahora en x = 1.10 -> va a 0.10 por paso.
    private readonly Vector3[] _currentPos;
    private readonly Vector3[] _previousPos;

    // Cantidad de HILOS (es el "quality" de GekkoSwinging). Hay _segments + 1 puntos:
    //   punto 0                   = la lengua (clavada)
    //   puntos 1 a _segments - 1  = los libres, los únicos que se mueven
    //   punto _segments           = el ancla (clavada)
    private readonly int _segments;

    // Cuánta energía conserva la soga cada 1/60 s (StepTime): 1 = nunca pierde y se balancea para siempre;
    // 0.99 = pierde un 1% cada vez. Es el "rozamiento con el aire": sin él la soga oscilaría eternamente.
    private readonly float _damping;

    // Qué tan rápido cae: aceleración hacia abajo en m/s². 9.81 = peso real (igual a Physics.gravity del proyecto).
    private readonly float _gravity;

    // Cuántas veces se recorren los hilos para corregirlos en cada paso. Más = soga más rígida y precisa;
    // menos = más elástica (se estira un poco, como una goma).
    private readonly int _iterations;

    // Cuánto duró el último paso simulado. Hace falta porque los pasos no duran todos lo mismo, y la velocidad
    // (actual - anterior) depende del tiempo que pasó entre esas dos posiciones (ver Integrate).
    private float _lastStep = StepTime;

    // Las posiciones actuales de los puntos, listas para dibujar. Índice 0 = lengua, último = ancla.
    // Es solo para leer: nadie de afuera debería escribirlas.
    public Vector3[] Points => _currentPos;

    // ==== CREAR Y REINICIAR =========================================================================================

    // quality = cantidad de hilos. damping, gravity e iterations: ver la descripción de cada campo más arriba.
    public RopeSimulation(int quality, float damping, float gravity, int iterations)
    {
        // Mathf.Max(1, ...) garantiza al menos un hilo: con 0 no habría soga y más abajo se divide por _segments.
        _segments = Mathf.Max(1, quality);

        // Un hilo une dos puntos, así que N hilos necesitan N + 1 puntos (como 3 tramos de vereda necesitan 4 postes).
        // Es la misma cantidad que usa el LineRenderer, por eso se puede dibujar copiando punto por punto.
        _currentPos = new Vector3[_segments + 1];
        _previousPos = new Vector3[_segments + 1];

        _damping = damping;
        _gravity = gravity;

        // Al menos una pasada de corrección: con 0 nadie mantendría el largo de los hilos y los puntos caerían sueltos.
        _iterations = Mathf.Max(1, iterations);
    }

    // Deja TODA la soga colapsada en 'position' (todos los puntos en el mismo lugar) y sin movimiento.
    // Se llama al engancharse. Sin este reset, la soga arrancaría con la forma y la velocidad del enganche anterior.
    public void Reset(Vector3 position)
    {
        for(int i = 0; i <= _segments; i++)
        {
            _currentPos[i] = position;
            _previousPos[i] = position;   // posición actual == anterior  =>  velocidad cero
        }

        _lastStep = StepTime;
    }

    // ==== AVANZAR LA SIMULACIÓN =====================================================================================

    // Avanza la soga 'deltaTime' segundos. Se llama UNA vez por frame mientras Gekko está colgado.
    //   tip        : dónde está la lengua de Gekko ahora (punto 0, clavado).
    //   end        : dónde está el ancla (último punto, clavado).
    //   restLength : cuánto mide la soga en total. Si es mayor que la distancia entre tip y end, sobra soga y cuelga;
    //                si es igual, queda recta.
    public void Simulate(float deltaTime, Vector3 tip, Vector3 end, float restLength)
    {
        // 1) Largo de cada hilo: todos miden lo mismo, así que es el largo total repartido en partes iguales.
        //    Ejemplo: restLength = 5.25 m con 10 hilos -> cada hilo mide 0.525 m.
        float segmentLength = restLength / _segments;

        // 2) Cuánto tiempo simular. Se recorta a MaxFrameTime para que un frame larguísimo (un congelamiento) no
        //    dispare decenas de pasos de golpe.
        float time = Mathf.Min(deltaTime, MaxFrameTime);

        // Caso especial: no hay tiempo que simular (juego en pausa con timeScale = 0). No se mueve nada por inercia:
        // solo se dejan las puntas y los hilos en orden. También evita guardar un paso de duración 0 en _lastStep,
        // porque en el próximo Integrate se dividiría por ese 0.
        if(time <= 0f)
        {
            PinEnds(tip, end);
            SolveConstraints(segmentLength);
            return;
        }

        // 3) Cuántos pasos hacer en este frame. time / StepTime dice "cuántos pasos de 1/60 s entran", y se redondea:
        //      144 fps: 0.42  -> redondea a 0, pero se fuerza a 1  -> 1 paso de ~7 ms
        //       60 fps: 1.0                                        -> 1 paso de ~17 ms
        //       30 fps: 2.0                                        -> 2 subpasos de ~17 ms
        //    Siempre hay al menos un paso, así las puntas y el cuerpo de la soga avanzan juntos en CADA frame.
        //    (Antes se usaban pasos fijos de 1/60 s: con frames más rápidos el cuerpo de la soga se movía a ráfagas
        //    mientras las puntas seguían a Gekko en cada frame, y se veía trabado en monitores de 120/144 Hz.)
        int steps = Mathf.Max(1, Mathf.RoundToInt(time / StepTime));
        float step = time / steps;

        // 4) El paso completo, repetido 'steps' veces. El orden importa:
        //      a) PinEnds:          las puntas van a su lugar de este frame (la lengua siguió a Gekko).
        //      b) Integrate:        los puntos libres se mueven por inercia y gravedad. Los hilos se pueden deformar.
        //      c) SolveConstraints: se arreglan los hilos para que vuelvan a medir lo justo.
        //    Como (c) es lo último y las puntas no se mueven ahí, la soga siempre termina bien pegada a la lengua
        //    y al ancla.
        for(int s = 0; s < steps; s++)
        {
            PinEnds(tip, end);
            Integrate(step);
            SolveConstraints(segmentLength);
        }
    }

    // ==== REGLA 1: CLAVAR LAS PUNTAS ================================================================================

    // Las puntas se escriben directo, sin inercia ni gravedad: no las mueve la simulación sino quien usa la clase
    // (la lengua sigue a Gekko y el ancla es el punto de enganche).
    private void PinEnds(Vector3 tip, Vector3 end)
    {
        _currentPos[0] = tip;
        _currentPos[_segments] = end;
    }

    // ==== REGLA 2: MOVER LOS PUNTOS LIBRES ==========================================================================

    // Cada punto libre sigue con la velocidad que traía y además cae por la gravedad.
    private void Integrate(float dt)
    {
        // Cuánto de la velocidad anterior se conserva. Son dos correcciones multiplicadas:
        //  a) Ajuste por duración: la velocidad (actual - anterior) es lo que el punto se movió en el paso ANTERIOR.
        //     Si ese paso duró distinto que este, hay que reescalarla. Ejemplo: si el anterior duró 16 ms y este dura
        //     8 ms, el punto se movería la mitad. Por eso se multiplica por dt / _lastStep.
        //  b) Damping (pérdida de energía). _damping está definido "por cada 1/60 s"; Pow lo convierte al largo de este
        //     paso. Para un paso de 1/60 s da justo _damping (0.99); para uno de 1/120 s da su raíz cuadrada (~0.995).
        //     Así la soga pierde la misma energía POR SEGUNDO a cualquier framerate.
        float velocityScale = dt / _lastStep * Mathf.Pow(_damping, dt / StepTime);

        // Cuánto cae por la gravedad en este paso. En Verlet la aceleración se suma directo a la posición como
        // aceleración * dt². Ejemplo: 9.81 * (1/60)² = 0.0027 m, unos 2.7 mm hacia abajo por paso.
        // Es igual para todos los puntos, así que se calcula una sola vez antes del loop.
        Vector3 gravityStep = Vector3.down * (_gravity * dt * dt);

        // Solo los puntos libres (del 1 al penúltimo). Los puntos 0 y _segments son las puntas y no se integran.
        for(int i = 1; i < _segments; i++)
        {
            // Velocidad implícita: lo que el punto se movió desde el paso anterior, con los ajustes (a) y (b).
            Vector3 velocity = (_currentPos[i] - _previousPos[i]) * velocityScale;

            // Se guarda la posición vieja ANTES de mover el punto: es lo que permite calcular la velocidad en el
            // próximo paso.
            _previousPos[i] = _currentPos[i];

            // Mover: sigue con su velocidad y además cae por la gravedad.
            _currentPos[i] += velocity + gravityStep;
        }

        // Se recuerda cuánto duró este paso para reescalar la velocidad en el próximo.
        _lastStep = dt;
    }

    // ==== REGLA 3: QUE LOS HILOS NO SE ESTIREN ======================================================================

    // Hace que cada hilo vuelva a medir segmentLength.
    private void SolveConstraints(float segmentLength)
    {
        // Se repite todo el recorrido _iterations veces. Corregir un hilo desarregla a sus vecinos (comparten un
        // punto), así que una sola pasada no alcanza: cada pasada acerca la cadena a cumplir todas las medidas a la
        // vez. Con 10 hilos y 8 pasadas son 80 correcciones por paso, que es muy poco trabajo.
        for(int k = 0; k < _iterations; k++)
        {
            for(int i = 0; i < _segments; i++)
            {
                // Se mide el hilo i, que une el punto i con el punto i + 1:
                //   delta    = la flecha que va del punto i al punto i + 1
                //   distance = el largo actual del hilo
                Vector3 delta = _currentPos[i + 1] - _currentPos[i];
                float distance = delta.magnitude;

                // Si los dos puntos están (casi) en el mismo lugar no hay dirección hacia la cual separarlos: se
                // salta este hilo (dividir por ~0 daría NaN).
                if(distance < MinDistance) continue;

                // ¿Puede moverse cada extremo del hilo? Las puntas están clavadas: no se mueven (peso 0). Los puntos
                // libres sí (peso 1).
                //   - Si un extremo está clavado, el otro absorbe TODA la corrección.
                //   - Si están clavados los dos (soga de un solo hilo), no hay nada que corregir.
                bool pinnedA = i == 0;
                bool pinnedB = i + 1 == _segments;

                if(pinnedA && pinnedB) continue;

                float weightA = pinnedA ? 0f : 1f;
                float weightB = pinnedB ? 0f : 1f;

                // Cuánto hay que corregir, como un vector:
                //   (distance - segmentLength) / distance = qué fracción del hilo sobra (positivo: está estirado) o
                //   falta (negativo: está comprimido). Se multiplica por delta (la dirección del hilo) y se reparte
                //   entre los dos extremos según su peso.
                //   Ejemplo: un hilo de 0.60 que debe medir 0.50, con los dos extremos libres. Sobra 0.10 (una sexta
                //   parte del hilo) y cada extremo corre la mitad: 0.05.
                Vector3 correction = delta * ((distance - segmentLength) / distance / (weightA + weightB));

                // Aplicar: si el hilo está estirado, el punto A avanza hacia B y el B hacia A (se acercan). Si está
                // comprimido, se alejan. En los dos casos el hilo queda del largo justo.
                _currentPos[i] += correction * weightA;
                _currentPos[i + 1] -= correction * weightB;
            }
        }
    }
}
