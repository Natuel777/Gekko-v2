// Marca los IDamageable que deben recibir un solo Damage() por lengüetazo.
// TongueManager llama Damage(1) en la punta y otra vez al terminar la retracción; los objetivos que
// no implementan esta interfaz (escarabajos, Bug, Sphere...) conservan ese comportamiento histórico.
public interface IHitOncePerLick { }
