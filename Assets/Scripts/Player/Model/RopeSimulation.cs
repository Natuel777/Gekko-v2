using UnityEngine;

// Soga visual simulada con integración de Verlet. Es una clase plana (no MonoBehaviour): GekkoSwinging la crea,
// la reinicia con Reset() al enganchar y, cada frame, le pasa el tiempo y las dos puntas con Simulate().
//
// La idea es un "collar de cuentas": _segments + 1 puntos unidos por hilos de largo fijo.
//   1. Las dos puntas (la lengua de Gekko y el ancla) NO se simulan: se clavan a donde diga GekkoSwinging.
//   2. Los puntos del medio se mueven solos por inercia + gravedad (Integrate).
//   3. Unas restricciones de distancia (SolveConstraints) impiden que los hilos se estiren o se encojan.
// El pandeo, el retraso cuando Gekko se mueve hacia un lado y el rebote al frenar salen solos de esas tres reglas:
// no hace falta una regla por cada efecto, ni leer el input, ni la velocidad del Rigidbody. Alcanza con que la
// lengua cambie de lugar, sea hacia la izquierda, la derecha, adelante o atrás.
//
// Es solo VISUAL: no toca el Rigidbody de Gekko ni el SpringJoint, así que no puede afectar al gameplay.
// Se eligió Verlet en vez de una cadena de Rigidbodies con joints porque son ~11 puntos de pura matemática:
// es barato, estable, y no puede empujar a Gekko ni chocar con el nivel.
public sealed class RopeSimulation
{
    // Duración de referencia de un paso de simulación (60 Hz). Se usa para dos cosas: los frames largos se parten
    // en subpasos de más o menos esta duración (ver Simulate) y _damping se define "por cada StepTime".
    private const float StepTime = 1f / 60f;

    // Tope de tiempo que se simula por frame. Si el juego se congela (carga de escena, breakpoint) no se corren
    // decenas de subpasos de golpe: con 0.1 s el máximo son 6.
    private const float MaxFrameTime = 0.1f;

    // Distancia mínima entre dos puntos para poder corregirlos. Por debajo no hay una dirección confiable
    // (los puntos están casi encimados) y dividir por ~0 daría NaN.
    private const float MinDistance = 1e-5f;

    // Verlet guarda DOS posiciones por punto en vez de posición + velocidad: la velocidad es simplemente
    // (actual - anterior). Así, cuando una restricción mueve un punto, su velocidad se corrige sola y no hay que
    // mantenerla a mano. Por eso Verlet es tan estable para sogas.
    private readonly Vector3[] _currentPos;   // dónde está cada punto ahora
    private readonly Vector3[] _previousPos;  // dónde estaba cada punto antes del último paso

    // Cantidad de hilos (= quality de GekkoSwinging). Hay _segments + 1 puntos: el 0 es la lengua, el _segments
    // es el ancla y los de en medio (1 a _segments - 1) son los únicos que se mueven libremente.
    private readonly int _segments;

    // Energía que conserva la soga cada StepTime (1/60 s): 1 = no pierde nunca, 0.99 = pierde un 1%.
    // Hace de rozamiento con el aire; sin él la soga se balancearía para siempre.
    private readonly float _damping;

    // Aceleración hacia abajo en m/s². 9.81 = soga con el peso "real" (coincide con Physics.gravity del proyecto).
    private readonly float _gravity;

    // Pasadas del solver de distancias por paso. Más = soga más rígida y precisa; menos = más elástica.
    private readonly int _iterations;

    // Duración del último paso simulado. Verlet la necesita para reescalar la velocidad implícita cuando el paso
    // actual dura distinto (ver Integrate).
    private float _lastStep = StepTime;

    // Posiciones actuales de los puntos, para leer (nadie de afuera las escribe). Índice 0 = lengua, último = ancla.
    public Vector3[] Points => _currentPos;

    public RopeSimulation(int quality, float damping, float gravity, int iterations)
    {
        // Mathf.Max: con 0 hilos no habría soga, y el resto del código divide por _segments.
        _segments = Mathf.Max(1, quality);

        // Un hilo une dos puntos, así que N hilos necesitan N + 1 puntos. Es el mismo conteo que el LineRenderer
        // (quality + 1), por eso la soga se puede volcar punto a punto sin conversiones.
        _currentPos = new Vector3[_segments + 1];
        _previousPos = new Vector3[_segments + 1];

        _damping = damping;
        _gravity = gravity;
        _iterations = Mathf.Max(1, iterations);
    }

    // Deja toda la soga colapsada en 'position' y sin movimiento. Se llama al enganchar: sin este reset la soga
    // heredaría la forma y la velocidad del enganche anterior.
    public void Reset(Vector3 position)
    {
        for(int i = 0; i <= _segments; i++)
        {
            _currentPos[i] = position;
            _previousPos[i] = position;   // actual == anterior  =>  velocidad cero
        }

        _lastStep = StepTime;
    }

    // Avanza la soga 'deltaTime' segundos. Se llama una vez por frame.
    //   tip        : dónde está la lengua de Gekko (punto 0), clavada.
    //   end        : dónde está el ancla (último punto), clavada.
    //   restLength : largo total de la soga en reposo. Si es mayor que la distancia entre las puntas la soga
    //                cuelga y ondula; si es igual queda recta.
    public void Simulate(float deltaTime, Vector3 tip, Vector3 end, float restLength)
    {
        // Todos los hilos miden lo mismo: el largo total repartido entre la cantidad de hilos.
        float segmentLength = restLength / _segments;

        float time = Mathf.Min(deltaTime, MaxFrameTime);

        // Sin tiempo que simular (pausa con timeScale = 0): solo se mantienen las puntas y los hilos consistentes,
        // sin mover nada por inercia. Además evita guardar un paso de duración 0 como _lastStep, que en el
        // próximo Integrate daría una división por cero.
        if(time <= 0f)
        {
            PinEnds(tip, end);
            SolveConstraints(segmentLength);
            return;
        }

        // El frame se simula completo y SIEMPRE con al menos un paso. Si dura StepTime o menos (60 fps o más) es un
        // único paso de la duración del frame; si dura más (30 fps) se parte en subpasos iguales de ~StepTime para no
        // perder precisión. Así las puntas y el cuerpo de la soga avanzan juntos en CADA frame.
        // (Con pasos fijos de 60 Hz y frames más rápidos, el cuerpo de la soga se movía a ráfagas mientras las puntas
        // seguían a Gekko en cada frame, y se veía trabado en monitores de 120/144 Hz.)
        int steps = Mathf.Max(1, Mathf.RoundToInt(time / StepTime));
        float step = time / steps;

        for(int s = 0; s < steps; s++)
        {
            // Un paso completo. El orden importa:
            //   a) clavar las puntas: la lengua sigue a Gekko y el ancla queda fija.
            //   b) mover los puntos libres con su inercia y la gravedad (los hilos se pueden deformar).
            //   c) arreglar los hilos para que vuelvan a medir lo que tienen que medir. Es lo último, y las puntas no
            //      se mueven en el solver, así que la soga siempre termina consistente con las puntas de este frame.
            PinEnds(tip, end);
            Integrate(step);
            SolveConstraints(segmentLength);
        }
    }

    // Clava las dos puntas. Las escribe directo, sin pasar por la inercia: son "mandadas" desde afuera.
    private void PinEnds(Vector3 tip, Vector3 end)
    {
        _currentPos[0] = tip;
        _currentPos[_segments] = end;
    }

    // Parte 1 de cada paso: mover los puntos libres con inercia + gravedad.
    private void Integrate(float dt)
    {
        // Verlet clásico supone que todos los pasos duran lo mismo, y entonces (actual - anterior) es "lo que el
        // punto se movió en el paso anterior". Acá los pasos duran distinto (cada frame es diferente), así que esa
        // velocidad se reescala por dt / _lastStep: si el paso anterior duró el doble, el punto se movió el doble
        // de lo que se movería ahora.
        // El damping se convierte igual a la duración de este paso: Pow(_damping, dt / StepTime) vale exactamente
        // _damping para un paso de StepTime, y así la soga pierde la misma energía POR SEGUNDO a cualquier framerate.
        float velocityScale = dt / _lastStep * Mathf.Pow(_damping, dt / StepTime);

        // En Verlet la aceleración se suma directo a la posición como aceleración * dt². Es igual para todos
        // los puntos, así que se calcula una sola vez fuera del loop.
        Vector3 gravityStep = Vector3.down * (_gravity * dt * dt);

        // Se arranca en 1 y se corta antes del último: los puntos 0 y _segments son las puntas y no se integran.
        for(int i = 1; i < _segments; i++)
        {
            // Inercia: lo que el punto se movió desde el paso anterior es lo que sigue moviéndose (velocidad
            // implícita), ya reescalada y con la pérdida de energía del damping.
            Vector3 velocity = (_currentPos[i] - _previousPos[i]) * velocityScale;

            // La posición vieja se guarda ANTES de mover el punto: pasa a ser la referencia del próximo paso.
            _previousPos[i] = _currentPos[i];

            _currentPos[i] += velocity + gravityStep;
        }

        _lastStep = dt;
    }

    // Parte 2 de cada paso: hacer que cada hilo vuelva a medir segmentLength.
    // Corregir un hilo desarregla a sus vecinos (comparten un punto), como ajustar una cadena de a un eslabón.
    // Por eso el barrido se repite _iterations veces: cada pasada deja la cadena más cerca de cumplir todas las
    // medidas a la vez. Con 10 hilos y 8 pasadas son 80 correcciones por paso, que no cuesta nada.
    private void SolveConstraints(float segmentLength)
    {
        for(int k = 0; k < _iterations; k++)
        {
            for(int i = 0; i < _segments; i++)
            {
                // El hilo i une el punto i con el i + 1.
                Vector3 delta = _currentPos[i + 1] - _currentPos[i];
                float distance = delta.magnitude;

                if(distance < MinDistance) continue;

                // Las puntas no se pueden mover. Si el hilo tiene una clavada, el otro punto absorbe TODA la
                // corrección; si tiene las dos clavadas (soga de un solo hilo) no hay nada que corregir.
                bool pinnedA = i == 0;
                bool pinnedB = i + 1 == _segments;

                if(pinnedA && pinnedB) continue;

                float weightA = pinnedA ? 0f : 1f;
                float weightB = pinnedB ? 0f : 1f;

                // (distance - segmentLength) / distance = qué fracción del hilo sobra (positivo: está estirado) o
                // falta (negativo: está comprimido). Multiplicada por delta da el vector de corrección, que se
                // reparte entre los dos puntos según su peso.
                Vector3 correction = delta * ((distance - segmentLength) / distance / (weightA + weightB));

                // Estirado: los puntos se acercan. Comprimido: se alejan. En ambos casos el hilo queda del largo justo.
                _currentPos[i] += correction * weightA;
                _currentPos[i + 1] -= correction * weightB;
            }
        }
    }
}
