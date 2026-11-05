using UnityEngine;

// Para quien quiera enterarse de que la punta de la lengua del Gekko le llegó, aunque el golpe no le haga daño (ej. una planta
// invulnerable mientras prepara el mordisco). TongueManager lo avisa justo antes de llamar a Damage().
public interface ITongueContactListener
{
    // tongueStart = de dónde sale la lengua. tongueTip = dónde terminó la punta: la lengua apunta al pivote del objetivo, así que
    // suele quedar adentro de su collider, y el punto de impacto en la superficie hay que buscarlo sobre ese recorrido.
    void OnTongueContact(Vector3 tongueStart, Vector3 tongueTip);
}
