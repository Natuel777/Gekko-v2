using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Corrupto con colisión (espinas o barreras que bloquean el paso). Se quema igual que <see cref="CorruptedPlant"/>
    /// y además apaga sus colliders: purificar la región abre el camino. Es gameplay, no solo arte.
    /// </summary>
    public class CorruptedBarrier : CorruptedPlant
    {
        [Header("Colisión")]
        [Tooltip("Colliders que se apagan. Si se deja vacío, usa los de este objeto y sus hijos.")]
        [SerializeField] private Collider[] _colliders;
        [Tooltip("Apagar los colliders apenas empieza a quemarse (se puede pasar enseguida) en vez de al terminar.")]
        [SerializeField] private bool _apagarAlEmpezar;

        protected override void OnReached()
        {
            if(_apagarAlEmpezar) ApagarColliders();
            base.OnReached();
        }

        protected override void OnBurned() => ApagarColliders();

        private void ApagarColliders()
        {
            if(_colliders == null || _colliders.Length == 0) _colliders = GetComponentsInChildren<Collider>(true);

            foreach(Collider c in _colliders)
                if(c != null) c.enabled = false;
        }
    }
}
