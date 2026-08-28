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
    [SerializeField] private AudioSource _jumpSound;
    [SerializeField] private AudioSource _landingSound;
    [SerializeField] private AudioSource _tongueSound;
    [SerializeField] private AudioSource _slurpSound;
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
    #endregion
    #region Particles
    public bool IsTrailPlaying() => _trail.isPlaying;
    public void PlayTrail() => _trail.Play();
    public void StopTrail() => _trail.Stop();
    #endregion
    #region sounds
    public bool IsWalkSoundPlaying() => _walkSound.isPlaying;
    public void WalkSoundPlay() => _walkSound.Play();
    public void WalkSoundStop() => _walkSound.Stop();
    public void LandingSoundPlay() => _landingSound.Play();
    public void JumpSoundPlay() => _jumpSound.Play();
    public void SlurpSoundPlay() => _slurpSound.Play();
    public void TongueSoundPlay() => _tongueSound.Play();
    #endregion

}
