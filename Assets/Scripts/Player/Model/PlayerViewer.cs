using UnityEngine;

public class PlayerViewer : MonoBehaviour
{
    [Header("Animator")]
    private Animator _anim;
    [SerializeField] private Animator _tongueAnim;
    [Header("Particles")]
    [SerializeField] private ParticleSystem _trail;
    [Header("Splash (agua)")]
    [Tooltip("Efecto al entrar o salir del agua (WaterSplash). Vacío = solo suena.")]
    [SerializeField] private GameObject _waterSplashPrefab;
    [Tooltip("Tamaño del splash respecto del tamaño de Gekko (el prefab está hecho para un objeto de escala 1).")]
    [Min(0.01f)]
    [SerializeField] private float _splashScale = 1.75f;
    [Tooltip("Multiplicador del tamaño según la fuerza: x = entrando despacio (caminando), y = cayendo desde alto.")]
    [SerializeField] private Vector2 _splashIntensityRange = new Vector2(0.7f, 1.3f);
    [Header("Sounds")]
    [SerializeField] private AudioSource _walkSound; 
    [SerializeField] private AudioSource _waterSound;
    private void Awake()
    {
        _anim = GetComponent<Animator>();

        // Los parámetros del nado se setean solo si el Animator los tiene: así no hay warnings mientras no exista la
        // animación de nado, y apenas arte agregue los estados (con IsSwimming / IsDiving) empiezan a usarse.
        if(_anim != null && _anim.runtimeAnimatorController != null)
        {
            foreach(AnimatorControllerParameter p in _anim.parameters)
            {
                if(p.name == SwimParam) _hasSwimParam = true;
                if(p.name == DiveParam) _hasDiveParam = true;
            }
        }
    }

    private const string SwimParam = "IsSwimming";
    private const string DiveParam = "IsDiving";
    private bool _hasSwimParam, _hasDiveParam;

    // Nadando (en la superficie o bajo el agua). Mientras no haya animación de nado se ve la de caminar.
    public void Swim(bool swimming)
    {
        if(_hasSwimParam) _anim.SetBool(SwimParam, swimming);
    }

    // Todo el cuerpo bajo el agua (buceando).
    public void Dive(bool diving)
    {
        if(_hasDiveParam) _anim.SetBool(DiveParam, diving);
    }
    #region Animations
    public void Move(bool can)
    {
        _anim.SetBool("IsWalk", can);
    }
    public void Jump(bool can)
    {
        _anim.SetBool("IsJump 0", can);
        _tongueAnim.SetBool("isJumpTongue", can);
    }
    public void Floor(bool can)
    {
        _anim.SetBool("Floor", can);
        _tongueAnim.SetBool("floorTongue", can);
    }
    public void Mouth(bool isOpen)
    {
        _anim.SetBool("fullMouth", isOpen);

        if(isOpen) _anim.SetTrigger("openMouth");

    }
    public void Attack() { _anim.SetTrigger("Attack"); }
    public void Celebrate() { _anim.SetTrigger("Celebrate"); }
    #endregion
    #region Particles
    public bool IsTrailPlaying() => _trail.isPlaying;
    public void PlayTrail() => _trail.Play();
    public void StopTrail() => _trail.Stop();
    #endregion
    #region sounds
    // Algunas escenas tienen un pj copiado suelto (no instancia de pj.prefab) al que le puede faltar alguna fuente: sin ella ese
    // sonido no suena, pero no tira UnassignedReferenceException en cada frame.
    public bool IsWalkSoundPlaying() => _walkSound != null && _walkSound.isPlaying;
    public bool IsWaterSoundPlaying() => _waterSound != null && _waterSound.isPlaying;
    public void WalkSoundPlay() { if(_walkSound != null) _walkSound.Play(); }
    public void WalkSoundStop() { if(_walkSound != null) _walkSound.Stop(); }
    public void WaterSoundPlay() { if(_waterSound != null) _waterSound.Play(); }
    public void WaterSoundStop() { if(_waterSound != null) _waterSound.Stop(); }
    public void LandingSoundPlay()
    {
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerLanding);
    }
    public void JumpSoundPlay()
    {
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerJump);
    }
    public void SlurpSoundPlay()
    {
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerSlurp);
    }
    public void TongueSoundPlay()
    {
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerTongueOut);
    }
    public void SplashSound()
    {
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerSplash);
    }

    // Sonido + efecto de splash en position (la superficie del agua). gekkoScale: escala de Gekko (cambia por nivel), para
    // que el splash quede proporcionado. intensity: 0 = entra despacio, 1 = cae desde alto.
    public void Splash(Vector3 position, float gekkoScale, float intensity)
    {
        SplashSound();
        if (_waterSplashPrefab == null) return;

        GameObject splash = Instantiate(_waterSplashPrefab, position, Quaternion.identity);
        float size = gekkoScale * _splashScale * Mathf.Lerp(_splashIntensityRange.x, _splashIntensityRange.y, Mathf.Clamp01(intensity));
        splash.transform.localScale = Vector3.one * size;

        // El prefab viene en loop (para verlo en el editor): acá es una sola ráfaga. En Hierarchy para que tome la escala de
        // arriba. Se borra solo cuando terminó la última partícula.
        float lifetime = 0f;
        foreach (ParticleSystem ps in splash.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
        }
        Destroy(splash, lifetime + 0.5f);
    }
    #endregion

}
