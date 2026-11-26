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
    private GekkoSwinging _swinging;

    public PlayerInputs(PlayerController controller, TongueManager tongue, AimManager aim, CameraFollow cam, InteractionManager interact, Player pj, GekkoSwinging swinging)
    {
        pjController = controller;
        _input = new CharacterInput();
        InputRebindStore.ApplySaved(_input.asset);
        _tongue = tongue;
        _aimM = aim;
        _cam = cam;
        _pjInteract = interact;
        _pj = pj;
        _swinging = swinging;
        //PauseInputSet(true);
    }

    public void ReloadBindings()
    {
        InputRebindStore.ApplySaved(_input.asset);
        // Queda en el Player.log del build: sirve para confirmar que el cambio de teclas del menú le llegó al jugador.
        var move = _input.Character.Movement.bindings;
        string moveKeys = "";
        foreach (var b in move) if (b.isPartOfComposite) moveKeys += b.name + "=" + b.effectivePath + " ";
        Debug.Log($"[PlayerInputs] Teclas aplicadas al jugador. Movimiento: {moveKeys}| Salto: {_input.Character.Jump.bindings[0].effectivePath}");
    }

    public void ArtificialEnable()
    {
        InputRebindStore.OnChanged -= ReloadBindings;
        InputRebindStore.OnChanged += ReloadBindings;
        _controlLocked = false;
        _input.Enable();
        _input.Character.Jump.performed += JumpInput;
        _input.Character.Jump.canceled += JumpCancel;
        _input.Character.Dive.performed += DiveInput;
        _input.Character.Dive.canceled += DiveCancel;
        _input.Character.Movement.performed += MoveInput;
        _input.Character.Movement.canceled += MoveCancel; 
        _input.Character.Tongue.performed += TongueInput;
        _input.Character.Restart.performed += Respawn;
        _input.Character.Pause.performed += PauseInput;
        // _input.Character.Lock.performed += LockInput; // Lock ya no existe en el action asset
        _input.Character.ChangeTarget.performed += ChangeTarget;
        _input.Character.Debug.performed += ChangeValues;
        _input.Character.Rotation.performed += RotateInput;
        _input.Character.Rotation.canceled += RotateCancel;
        _input.Character.Interact.performed += InteractInput;
        _input.Character.TongueAttach.performed += TongueAttachInput;
        _input.Character.TongueAttach.canceled += TongueAttachCancel;
    }

    public void ArtificialDisable()
    {
        InputRebindStore.OnChanged -= ReloadBindings;
        _input.Disable();
        _input.Character.Jump.performed -= JumpInput;
        _input.Character.Jump.canceled -= JumpCancel;
        _input.Character.Dive.performed -= DiveInput;
        _input.Character.Dive.canceled -= DiveCancel;
        _input.Character.Movement.performed -= MoveInput;
        _input.Character.Movement.canceled -= MoveCancel;
        _input.Character.Tongue.performed -= TongueInput;
        _input.Character.Restart.performed -= Respawn;
        _input.Character.Pause.performed -= PauseInput;
        // _input.Character.Lock.performed -= LockInput; // Lock ya no existe en el action asset
        _input.Character.ChangeTarget.performed -= ChangeTarget;
        _input.Character.Debug.performed -= ChangeValues;
        _input.Character.Rotation.performed -= RotateInput;
        _input.Character.Rotation.canceled -= RotateCancel;
        _input.Character.Interact.performed -= InteractInput;
        _input.Character.TongueAttach.performed -= TongueAttachInput;
        _input.Character.TongueAttach.canceled -= TongueAttachCancel;
    }
    public void PauseInputSet(bool a)
    {
        if (a) 
            _input.Character.Pause.performed += PauseInput;
        else
            _input.Character.Pause.performed -= PauseInput;

    }
    // Control cortado por diálogo / cinemática (Inputs(false)). La lengua y el swing no se desuscriben (Click
    // avanza el diálogo), así que se frenan con este flag.
    private bool _controlLocked;
    // Cortado porque hay otra cámara (cinemática, plano de una cámara) en vez de la de juego. Ver Player.UpdateCameraLock.
    private bool _cameraLocked;
    private bool Blocked => _controlLocked || _cameraLocked;

    public void SetCameraLocked(bool locked)
    {
        if(_cameraLocked == locked) return;
        _cameraLocked = locked;

        if(locked)
        {
            StopAllMovement();
            return;
        }

        // Si mantenía apretada una dirección durante la cámara, sigue caminando al volver sin tener que soltarla.
        if(!_controlLocked)
        {
            Vector2 move = _input.Character.Movement.ReadValue<Vector2>();
            pjController.RawInput = move;
            _swinging.ThrustInput = move;
        }
    }

    private void StopAllMovement()
    {
        pjController.RawInput = Vector2.zero;
        pjController.CancelMovement();
        pjController.JumpPressed = false;
        pjController.DivePressed = false;
        _swinging.ThrustInput = Vector2.zero;
    }

    public void DeactivatePlayerInputs()
    {
        _controlLocked = true;
        _input.Character.Jump.performed -= JumpInput;
        _input.Character.Jump.canceled -= JumpCancel;
        _input.Character.Dive.performed -= DiveInput;
        _input.Character.Dive.canceled -= DiveCancel;
        _input.Character.Movement.performed -= MoveInput;
        _input.Character.Movement.canceled -= MoveCancel;
        // _input.Character.Lock.performed -= LockInput; // Lock ya no existe en el action asset
        _input.Character.ChangeTarget.performed -= ChangeTarget;
        _input.Character.Debug.performed -= ChangeValues;
        _input.Character.Rotation.performed -= RotateInput;
        _input.Character.Rotation.canceled -= RotateCancel;
    }

    public void ReactivatePlayerInputs()
    {
        // Evita suscribir dos veces (Inputs(true) llamado sin un Inputs(false) antes): duplicaba los handlers.
        if(!_controlLocked) return;
        _controlLocked = false;

        _input.Character.Jump.performed += JumpInput;
        _input.Character.Jump.canceled += JumpCancel;
        _input.Character.Dive.performed += DiveInput;
        _input.Character.Dive.canceled += DiveCancel;
        _input.Character.Movement.performed += MoveInput;
        _input.Character.Movement.canceled += MoveCancel;
        // _input.Character.Lock.performed += LockInput; // Lock ya no existe en el action asset
        _input.Character.ChangeTarget.performed += ChangeTarget;
        _input.Character.Debug.performed += ChangeValues;
        _input.Character.Rotation.performed += RotateInput;
        _input.Character.Rotation.canceled += RotateCancel;
    }

    private bool DialogueActive => UIManager.Instance != null && UIManager.Instance.HasActiveDialogue();

    private void MoveInput(InputAction.CallbackContext value)
    {
        if(Blocked) return;
        Vector2 move = value.ReadValue<Vector2>();
        pjController.RawInput = move;
        _swinging.ThrustInput = move;
    }

    private void MoveCancel(InputAction.CallbackContext value)
    {
        pjController.RawInput = Vector2.zero;
        pjController.CancelMovement();
        _swinging.ThrustInput = Vector2.zero;
    }

    private void RotateInput(InputAction.CallbackContext value) => _cam.MovingCamera = true;

    private void RotateCancel(InputAction.CallbackContext value) => _cam.MovingCamera = false;

    private void JumpInput(InputAction.CallbackContext value)
    {
        //if(!_swinging.IsSwinging && _swinging.GrappleBehaviour.SecondJumpEnabled)
        //{
        //    _swinging.TrySecondJump();
        //    return;
        //}

        if(Blocked || _swinging.IsGrappling) return;

        pjController.JumpPressed = true;
    }

    // Bucear (C / Ctrl): mantenido baja a Gekko en el agua profunda (ver GekkoSwimming).
    private void DiveInput(InputAction.CallbackContext value) { if(!Blocked) pjController.DivePressed = true; }

    private void DiveCancel(InputAction.CallbackContext value) => pjController.DivePressed = false;

    private void InteractInput(InputAction.CallbackContext value)
    {
        if(DialogueActive) { UIManager.Instance.AdvanceDialogue(); return; }
        
        //_pj.Interactor?.TryInteract();
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
        if(DialogueActive) { UIManager.Instance.AdvanceDialogue(); return; }

        // En cinemática / con otra cámara no se puede sacar la lengua.
        if(Blocked) return;

        if(!pjController.TongueOut || _tongue.IsAttached)
            _tongue.ShootTongue();
    }

    public void Respawn(InputAction.CallbackContext value)
    {
        if(RespawnController.Instance != null) RespawnController.Instance.TriggerRespawn(false);
        else GameManager.Instance.Respawn();
    }


    private void PauseInput(InputAction.CallbackContext value)
    {
        if(ScreenManager.Instance != null)
            
            if(ScreenManager.Instance.CanPause)
            {
                GameManager.Instance.Pause();
                // El cursor se libera solo si el menú de pausa quedó abierto. Pause() no hace nada cuando CanPause ya está en
                // true sin que haya pausa (fundidos de ScreenFader, respawn, secuencias de MomDuck): liberarlo igual dejaba el
                // cursor suelto, visible y confinado a la ventana, con el juego corriendo, hasta la próxima pausa.
                if(GameManager.Instance.IsPaused) Cursor.lockState = CursorLockMode.Confined;
            }
    }

    // private void LockInput(InputAction.CallbackContext value) { _aimM.ToggleLock(); } // Lock ya no existe en el action asset

    private void ChangeTarget(InputAction.CallbackContext value) {_aimM.SwitchTarget(value.ReadValue<Vector2>()); }

    private void ChangeValues(InputAction.CallbackContext value) { _pj.ChangeVariables(); }

    // Click derecho: engancha lo que apunta la cámara. Un gancho de swing arranca el swing; un GrapplePoint, el grapple.
    private void TongueAttachInput(InputAction.CallbackContext value) { if(!Blocked) _swinging.AttachTongue(); }

    private void TongueAttachCancel(InputAction.CallbackContext value) => _swinging.StopSwing();

}

