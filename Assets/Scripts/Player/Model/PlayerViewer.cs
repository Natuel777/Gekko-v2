using UnityEngine;

public class PlayerViewer : MonoBehaviour
{
    [Header("Animator")]
    private Animator _anim;
    [SerializeField] private Animator _tongueAnim;
    [Header("Particles")]
    [SerializeField] private ParticleSystem _trail;
    [Header("Sounds")]
    [SerializeField] private AudioSource _walkSound; 
    [SerializeField] private AudioSource _waterSound;
    private void Awake()
    {
        _anim = GetComponent<Animator>();
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
    #endregion

}
