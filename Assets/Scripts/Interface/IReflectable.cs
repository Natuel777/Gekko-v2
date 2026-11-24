using UnityEngine;

// Something that can be sent back: used by Mirror, which doesn't need to know what it is (today, only TurretBullet).
public interface IReflectable
{
    // Sends it back the way it came: inverts its direction. This is what a mirror without a bounce point does.
    void Reflect();

    // Sends it towards direction, which must be a non-zero unit vector. This is what a mirror with a bounce point does (see Mirror).
    void ReflectTowards(Vector3 direction);
}
