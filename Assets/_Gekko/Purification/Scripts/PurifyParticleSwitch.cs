using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Partículas ambientales de la región: las corruptas (ceniza, esporas) dejan de emitir y las que ya están se
    /// apagan solas; las sanas (luciérnagas, polen, mariposas) empiezan a emitir.
    /// </summary>
    public class PurifyParticleSwitch : PurifyReactive
    {
        [Header("Partículas")]
        [Tooltip("Partículas corruptas: dejan de emitir cuando llega la onda.")]
        [SerializeField] private ParticleSystem[] _corruptas;
        [Tooltip("Partículas sanas: arrancan apagadas y empiezan a emitir cuando llega la onda.")]
        [SerializeField] private ParticleSystem[] _sanas;

        protected override void Start()
        {
            base.Start();

            if(Disparado || Region == null) return;

            foreach(ParticleSystem ps in _sanas)
                if(ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        protected override void OnReached()
        {
            foreach(ParticleSystem ps in _corruptas)
                if(ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            foreach(ParticleSystem ps in _sanas)
                if(ps != null) ps.Play(true);
        }

        protected override void OnPurifiedInstant()
        {
            foreach(ParticleSystem ps in _corruptas)
                if(ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach(ParticleSystem ps in _sanas)
                if(ps != null) ps.Play(true);
        }
    }
}
