using UnityEngine;
using UnityEngine.InputSystem;

public class TestInputs
{
    private CharacterInput _input;
    private Move _pjMove;
    private LegsManager _pjLegs;

    public TestInputs(Move move, LegsManager legs)
    {
        _pjMove = move;
        _pjLegs = legs;
        _input = new CharacterInput();
    }
    public void ArtificialEnable()
    {
        _input.Enable();
        _input.Character.Jump.performed += JumpInput;
        _input.Character.Jump.canceled += JumpCancel;
        _input.Character.Movement.performed += MoveInput;
        _input.Character.Movement.canceled += MoveCancel;
       
    }
    public void ArtificialDisable()
    {
        _input.Disable();
        _input.Character.Jump.performed -= JumpInput;
        _input.Character.Jump.canceled -= JumpCancel;
        _input.Character.Movement.performed -= MoveInput;
        _input.Character.Movement.canceled -= MoveCancel;
    }
    private void MoveInput(InputAction.CallbackContext value) { _pjMove.RawInput = value.ReadValue<Vector2>(); }

    private void MoveCancel(InputAction.CallbackContext value)
    {
        _pjMove.RawInput = Vector2.zero;
        _pjMove.CancelMovement();
    }
    private void JumpInput(InputAction.CallbackContext value)
    {
        _pjMove.JumpPressed = true;
        _pjLegs.Jumping(true);
    }

    private void JumpCancel(InputAction.CallbackContext value)
    {
        _pjMove.JumpPressed = false;
        _pjLegs.Jumping(false);
        _pjMove.CancelJump();
    }
}
