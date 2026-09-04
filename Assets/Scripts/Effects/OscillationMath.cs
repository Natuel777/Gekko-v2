using UnityEngine;

public static class OscillationMath
{
    public static float PulseT(float timer, float period)
    {
        float t = Mathf.PingPong(timer * 2f / period, 1f);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    public static float Swing(float timer, float period, float amplitude)
    {
        return amplitude * Mathf.Sin(timer * 2f * Mathf.PI / period);
    }
}
