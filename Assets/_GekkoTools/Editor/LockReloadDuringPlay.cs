using UnityEditor;

namespace Gekko.Tools.EditorTools
{
    /// <summary>
    /// Evita que Unity recargue los scripts en medio del Play. Si un .cs cambia mientras se juega (al guardar en el IDE, al
    /// hacer un pull, etc.), Unity lo compila igual pero recién lo aplica al salir del Play.
    ///
    /// Por que existe: con la opcion de fabrica de Unity (Preferences > General > Script Changes While Playing = "Recompile
    /// And Continue Playing") la recarga en pleno Play borra todo lo que no se serializa. Casi toda la logica del juego vive
    /// en clases comunes creadas en Awake/Start (PlayerController, la StateMachine de los NPCs, el scheduler de los
    /// desafios, los ScriptableObject en runtime...) y en singletons estaticos (GameManager.Instance), asi que despues de
    /// la recarga quedan en null y cada Update tira NullReferenceException (Player.cs, StrawBerry, CameraFollow,
    /// PurificationChallenge, CarnivorousPlant...). El juego queda roto hasta salir del Play.
    ///
    /// Es lo mismo que elegir "Recompile After Finished Playing" en las Preferences, pero vale para todo el equipo sin
    /// que cada uno tenga que cambiarlo a mano.
    /// </summary>
    [InitializeOnLoad]
    public static class LockReloadDuringPlay
    {
        // LockReloadAssemblies es un contador: cada Lock necesita su Unlock. Esto evita bloquear dos veces.
        private static bool _locked;

        static LockReloadDuringPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Por si este dominio se cargo estando ya en Play (por ejemplo, la primera vez que se compila este script).
            if(EditorApplication.isPlaying) Lock();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change == PlayModeStateChange.EnteredPlayMode) Lock();
            else if(change == PlayModeStateChange.ExitingPlayMode) Unlock();
        }

        private static void Lock()
        {
            if(_locked) return;
            EditorApplication.LockReloadAssemblies();
            _locked = true;
        }

        private static void Unlock()
        {
            if(!_locked) return;
            EditorApplication.UnlockReloadAssemblies();
            _locked = false;
        }
    }
}
