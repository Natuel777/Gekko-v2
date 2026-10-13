using UnityEngine;

// Activa al gecko amigo (arranca desactivado en la escena) cuando el jefe es derrotado. Separado de
// CorruptedGeckoFriend porque un objeto inactivo no puede suscribirse solo a su propio evento de reveal.
public class CorruptedGeckoFriendReveal : MonoBehaviour
{
    [SerializeField] private CorruptedGeckoBoss _boss;
    [SerializeField] private GameObject _friendGekko;

    private void OnEnable()
    {
        if(_boss != null) _boss.Defeated += Reveal;
    }

    private void OnDisable()
    {
        if(_boss != null) _boss.Defeated -= Reveal;
    }

    private void Reveal()
    {
        if(_friendGekko != null) _friendGekko.SetActive(true);
    }
}
