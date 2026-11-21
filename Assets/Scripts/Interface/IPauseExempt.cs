// Marca un Behaviour que la pausa NO tiene que apagar (ver ScreenGO.KeepEnabledOnPause).
//
// Al pausar, ScreenGO apaga todos los Behaviour de la escena de juego para congelarla. Eso está bien para la lógica, pero
// hay componentes que solo DIBUJAN y que al apagarse dejan de verse: por ejemplo ScatterField y GrassField, que dibujan
// con Graphics.DrawMesh* (no tienen MeshRenderer) y en OnDisable sueltan sus chunks. Con la pausa, toda la vegetación
// pintada con la tool de props desaparecía. Estos componentes no hacen nada de gameplay, así que se quedan prendidos.
public interface IPauseExempt { }
