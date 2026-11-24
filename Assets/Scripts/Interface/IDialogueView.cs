// Diálogos que se muestran en el mundo (ej. las páginas del libro, ver BookNPC) en vez del panel de abajo de la pantalla.
// UIManager sigue manejando el ciclo del diálogo (bloquear a Gekko, avanzar con E / click, cerrar), pero no abre su panel
// ni escribe el texto: eso lo hace el objeto, que arranca en OnDialogueStart y termina en OnDialogueEnd (IDialogueable).
public interface IDialogueView
{
    // Avanzar (E / click). Devuelve false cuando ya no queda nada para mostrar: ahí UIManager cierra el diálogo.
    bool AdvanceView();
}
