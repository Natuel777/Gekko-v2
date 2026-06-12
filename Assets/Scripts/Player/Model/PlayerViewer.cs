using UnityEngine;

public class PlayerViewer : MonoBehaviour
{
    private Animator _anim;
    [SerializeField] private Animator _tongueAnim;
    private void Awake()
    {
        _anim = GetComponent<Animator>();
    }
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
}
