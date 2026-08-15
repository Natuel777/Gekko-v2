using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputs
{
    private CharacterInput _input;
    private PlayerController pjController; 
    private TongueManager _tongue;
    private AimManager _aimM;
    private CameraFollow _cam;
    private InteractionManager _pjInteract;
    private Player _pj;

    public PlayerInputs(PlayerController controller, TongueManager tongue, AimManager aim, CameraFollow cam, InteractionManager interact, Player pj)
    {
        pjController = controller;
        _input = new CharacterInput();
        _tongue = tongue;
        _aimM = aim;
        _cam = cam;
        _pjInteract = interact;
        _pj = pj;
    }
    public void ArtificialEnable()
    {
        _input.Enable();
        _input.Character.Jump.performed += JumpInput;
        _input.Character.Jump.canceled += JumpCancel;
        _input.Character.Movement.performed += MoveInput;
        _input.Character.Movement.canceled += MoveCancel; 
        _input.Character.Tongue.performed += TongueInput;
        _input.Character.Restart.performed += Respawn;
        _input.Character.Pause.performed += PauseInput;
        _input.Character.Lock.performed += LockInput;
        _input.Character.ChangeTarget.performed += ChangeTarget;
        _input.Character.Debug.performed += ChangeValues;
        _input.Character.Rotation.performed += RotateInput;
        _input.Character.Rotation.canceled += RotateCancel;
        _input.Character.Interact.performed += InteractInput;
    }

    public void ArtificialDisable()
    {
        _input.Disable();
        _input.Character.Jump.performed -= JumpInput;
        _input.Character.Jump.canceled -= JumpCancel;
        _input.Character.Movement.performed -= MoveInput;
        _input.Character.Movement.canceled -= MoveCancel;
        _input.Character.Tongue.performed -= TongueInput;
        _input.Character.Restart.performed -= Respawn;
        _input.Character.Pause.performed -= PauseInput;
        _input.Character.Lock.performed -= LockInput;
        _input.Character.ChangeTarget.performed -= ChangeTarget;
        _input.Character.Debug.performed -= ChangeValues;
        _input.Character.Rotation.performed -= RotateInput;
        _input.Character.Rotation.canceled -= RotateCancel;
        _input.Character.Interact.performed -= InteractInput;
    }
    public void DeactivatePlayerInputs()
    {
        _input.Character.Jump.performed -= JumpInput;
        _input.Character.Jump.canceled -= JumpCancel;
        _input.Character.Movement.performed -= MoveInput;
        _input.Character.Movement.canceled -= MoveCancel;
        _input.Character.Lock.performed -= LockInput;
        _input.Character.ChangeTarget.performed -= ChangeTarget;
        _input.Character.Debug.performed -= ChangeValues;
        _input.Character.Rotation.performed -= RotateInput;
        _input.Character.Rotation.canceled -= RotateCancel;
    }
    public void ReactivatePlayerInputs()
    {
        _input.Character.Jump.performed += JumpInput;
        _input.Character.Jump.canceled += JumpCancel;
        _input.Character.Movement.performed += MoveInput;
        _input.Character.Movement.canceled += MoveCancel;
        _input.Character.Lock.performed += LockInput;
        _input.Character.ChangeTarget.performed += ChangeTarget;
        _input.Character.Debug.performed += ChangeValues;
        _input.Character.Rotation.performed += RotateInput;
        _input.Character.Rotation.canceled += RotateCancel;
    }
    private bool DialogueActive => UIManager.Instance != null && UIManager.Instance.HasActiveDialogue();

    private void MoveInput(InputAction.CallbackContext value)
    {
        pjController.RawInput = value.ReadValue<Vector2>();
    }

    private void MoveCancel(InputAction.CallbackContext value)
    {
        pjController.RawInput = Vector2.zero;
        pjController.CancelMovement();
    }

    private void RotateInput(InputAction.CallbackContext value)
    {
        _cam.MovingCamera = true;
    }

    private void RotateCancel(InputAction.CallbackContext value) {_cam.MovingCamera = false;}

    private void JumpInput(InputAction.CallbackContext value)
    {
        pjController.JumpPressed = true;
    }

    private void InteractInput(InputAction.CallbackContext value)
    {
        if (DialogueActive) { UIManager.Instance.AdvanceDialogue(); return; }
        _pj.Interactor?.TryInteract();
        _pjInteract.Interact();
    }

    private void JumpCancel(InputAction.CallbackContext value) 
    {
        pjController.JumpPressed = false;
        pjController.CancelJump();
    }

    private void TongueInput(InputAction.CallbackContext value)
    {
        // Durante el diálogo, Click avanza la frase en vez de sacar la lengua.
        if (DialogueActive) { UIManager.Instance.AdvanceDialogue(); return; }
        if (!pjController.TongueOut || _tongue.IsAttached)
            _tongue.ShootTongue();
    }
    public void Respawn(InputAction.CallbackContext value) {GameManager.Instance.Respawn();}



    private void PauseInput(InputAction.CallbackContext value)
    {
        if (ScreenManager.Instance != null)
            if (ScreenManager.Instance.CanPause)
            {
                GameManager.Instance.Pause();
                Cursor.lockState = CursorLockMode.Confined;
            }
    }

    private void LockInput(InputAction.CallbackContext value) { _aimM.ToggleLock(); }

    private void ChangeTarget(InputAction.CallbackContext value) {_aimM.SwitchTarget(value.ReadValue<Vector2>()); }

    private void ChangeValues(InputAction.CallbackContext value) { _pj.ChangeVariables(); }
}

