using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gekko.Tools.EditorTools
{
    /// <summary>
    /// Herramienta de UN SOLO USO: aplica a los jugadores de la escena abierta el mismo recentrado de pivote
    /// que ya tiene Assets/Prefabs/pj.prefab.
    ///
    /// Por que existe: el pivote de 'pj' (el origen de la raiz) estaba ~0.25 adelantado respecto del centro
    /// de los 4 apoyos del cuerpo, asi que al girar el gecko pivotaba sobre las manos y la cola barria de mas.
    /// La solucion es correr TODO el cuerpo 0.25 hacia adelante dentro de la raiz (el pivote queda en el centro).
    ///
    /// Que mueve (+0.25 en Z local de la raiz): el modelo (PF_MainCharacterGekko), el Interaction Point, la
    /// lengua (tongueGekko), el trail (p_Trail3) y el centro del CapsuleCollider. SwingTongue no se mueve
    /// porque su LineRenderer usa coordenadas de mundo.
    ///
    /// Esta herramienta existe porque la copia de 'pj' que hay en LvlOne NO esta vinculada al prefab, asi que
    /// el cambio del prefab no la alcanza. Se hace desde el Editor y no editando el archivo de la escena para no
    /// pisar cambios sin guardar. Es segura de correr dos veces: si el cuerpo ya esta corrido, no hace nada.
    /// Se puede deshacer con Ctrl+Z. Despues de usarla se puede borrar este archivo.
    /// </summary>
    public static class RecenterPlayerPivot
    {
        private const string MenuPath = "Tools/Gekko/Recentrar pivote del jugador (escena abierta)";

        // Cuanto se corre el cuerpo hacia adelante. Tiene que ser el mismo valor que se uso en pj.prefab.
        private const float Shift = 0.25f;

        // Margen para comparar floats al detectar el estado del modelo.
        private const float Tolerance = 0.001f;

        // Hijos directos de la raiz que acompanan al cuerpo. El trail es opcional (no todas las copias lo tienen).
        private static readonly string[] BodyChildren = { "PF_MainCharacterGekko", "Interaction Point", "tongueGekko", "p_Trail3" };

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Recentrar pivote", "Salí de Play Mode primero: los cambios hechos jugando se pierden al salir.", "OK");
                return;
            }

            Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (players.Length == 0)
            {
                EditorUtility.DisplayDialog("Recentrar pivote", "No hay ningún Player en las escenas abiertas.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Recentrar pivote del jugador");
            int undoGroup = Undo.GetCurrentGroup();

            int applied = 0;
            string report = "";

            foreach (Player player in players)
            {
                string result = Apply(player.transform);
                if (result == null) applied++;
                else report += $"\n- {player.name}: {result}";
            }

            Undo.CollapseUndoOperations(undoGroup);

            string summary = applied > 0
                ? $"Pivote recentrado en {applied} jugador(es). Falta guardar la escena (Ctrl+S)."
                : "No se cambió nada.";

            Debug.Log($"[RecenterPlayerPivot] {summary}{report}");
            EditorUtility.DisplayDialog("Recentrar pivote", summary + report, "OK");
        }

        // Devuelve null si aplico el cambio, o el motivo por el que no lo hizo.
        private static string Apply(Transform root)
        {
            Transform model = root.Find("PF_MainCharacterGekko");
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();

            if (model == null || capsule == null)
                return "no tiene PF_MainCharacterGekko o CapsuleCollider (estructura distinta a la esperada), no se toca.";

            // El modelo dice en que estado esta la copia: Z = 0 es el original, Z = Shift ya esta recentrada.
            float modelZ = model.localPosition.z;

            if (Mathf.Abs(modelZ - Shift) < Tolerance)
                return "ya estaba recentrado.";

            if (Mathf.Abs(modelZ) > Tolerance)
                return $"el modelo está en Z = {modelZ} (se esperaba 0), no se toca para no desarmar un ajuste manual.";

            foreach (string childName in BodyChildren)
            {
                Transform child = root.Find(childName);

                if (child == null)
                {
                    Debug.LogWarning($"[RecenterPlayerPivot] '{root.name}' no tiene el hijo '{childName}', se omite.");
                    continue;
                }

                Undo.RecordObject(child, "Recentrar pivote del jugador");
                child.localPosition += new Vector3(0f, 0f, Shift);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            }

            Undo.RecordObject(capsule, "Recentrar pivote del jugador");
            capsule.center += new Vector3(0f, 0f, Shift);
            PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return null;
        }
    }
}
