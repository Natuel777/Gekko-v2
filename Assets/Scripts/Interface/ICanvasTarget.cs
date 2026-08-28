using UnityEngine;

public interface IParticleSystemTarget
{
    ParticleSystem Indicator { get; }
    bool CanBeTargeted { get; }
}
