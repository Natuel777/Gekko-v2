using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Presentación del desafío de espejos (PrimerChallenge de LvlTwo). La mecánica que hay que entender: la torreta le dispara
// a Gekko, así que Gekko se para DETRÁS de un espejo levantado; la bala pega en el espejo, vuelve purificada y purifica a la
// torreta. Para enseñarlo se usa un espejo "de práctica" que devuelve la bala a la misma torreta que disparó:
//  1. La primera vez que ese espejo queda levantado (normalmente al pisar la primera placa, resaltada por PlateHighlight),
//     la cámara muestra el espejo y la torreta y aparece la marca en el piso (StandMarker) detrás del espejo.
//  2. Disparo de demostración (solo visual, no purifica): una bala sale de la torreta hacia la marca, pega en el espejo, se
//     vuelve turquesa y regresa a la torreta. Se repite _demoShots veces.
//  3. Vuelve la cámara de juego y aparece el cartel de pista en el HUD (HintBanner, localizado).
//  4. La marca queda mientras el espejo esté levantado y la torreta sin purificar. Al purificarla: se va la marca y aparece
//     el cartel de "bien hecho".
// Si hay un MirrorRevealCinematic, la demostración va pegada a su plano (sin soltar a Gekko en el medio). Si el espejo se
// levanta de otra forma (otra placa), la demostración arma su propia cinemática.
public class MirrorTutorial : MonoBehaviour
{
    [Header("Piezas del desafío")]
    [Tooltip("Espejo de la demostración: devuelve la bala a la misma torreta que disparó.")]
    [SerializeField] private Mirror _mirror;
    [Tooltip("Torreta que se purifica con ese espejo.")]
    [SerializeField] private ChallengeTurret _turret;
    [Tooltip("Opcional: si está, la demostración va pegada al plano que muestra el espejo al pisar la primera placa.")]
    [SerializeField] private MirrorRevealCinematic _reveal;

    [Header("Presentación")]
    [Tooltip("Marca en el piso, detrás del espejo: donde tiene que pararse Gekko.")]
    [SerializeField] private StandMarker _marker;
    [Tooltip("Cámara que encuadra torreta, espejo y marca de costado.")]
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private int _cameraPriority = 40;
    [Tooltip("Segundos del viaje de la cámara (ida y vuelta) cuando la demostración arma su propia cinemática.")]
    [Min(0.1f)]
    [SerializeField] private float _blendSeconds = 1f;

    [Header("Disparo de demostración (solo visual)")]
    [Tooltip("Prefab de la bala de la torreta: se usa una copia sin lógica, que no daña ni purifica.")]
    [SerializeField] private TurretBullet _bulletPrefab;
    [SerializeField] private float _demoSpeed = 7f;
    [Tooltip("Tamaño de la bala de demostración respecto de la real (más grande para que se lea en el plano).")]
    [Min(0.1f)]
    [SerializeField] private float _demoScale = 2.2f;
    [Tooltip("Luz que acompaña a la bala: violeta (corrupta) a la ida, turquesa (purificada) a la vuelta. Intensidad 0 = sin luz.")]
    [SerializeField] private float _demoLightIntensity = 4f;
    [SerializeField] private Color _demoCorruptColor = new Color(0.75f, 0.25f, 1f);
    [SerializeField] private Color _demoPurifiedColor = new Color(0.2f, 1f, 0.85f);
    [Range(1, 5)]
    [SerializeField] private int _demoShots = 2;
    [SerializeField] private float _pauseBeforeShots = 0.3f;
    [SerializeField] private float _pauseBetweenShots = 0.3f;
    [SerializeField] private float _holdAfterDemo = 0.4f;

    [Header("Pistas en el HUD")]
    [SerializeField] private HintBanner _hint;
    [SerializeField] private string _standHintKey = "mirror_hint_stand";
    [SerializeField] private float _standHintSeconds = 7f;
    [SerializeField] private string _doneHintKey = "mirror_hint_done";
    [SerializeField] private float _doneHintSeconds = 5f;

    private bool _demoPlayed, _running, _finished, _frozen;
    private int _cameraBasePriority;
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _savedBlend;
    private bool _blendSaved;

    private void OnEnable()
    {
        if(_reveal != null) _reveal.AfterReveal = AfterReveal;
    }

    private void OnDisable()
    {
        if(_reveal != null && _reveal.AfterReveal == AfterReveal) _reveal.AfterReveal = null;
        RestoreCamera();
        RestoreBlend();
        Freeze(false);
    }

    private void Start()
    {
        if(_mirror == null || _turret == null)
        {
            Debug.LogWarning($"[{name}] MirrorTutorial necesita el espejo y la torreta de la demostración.", this);
            enabled = false;
            return;
        }

        if(_camera != null) _cameraBasePriority = _camera.Priority.Value;

        // Ya resuelta (ej. se volvió a cargar la escena con la torreta purificada): no hay nada que enseñar.
        if(_turret.IsPurified)
        {
            _finished = true;
            return;
        }

        _turret.Purified += OnTurretPurified;
    }

    private void OnDestroy()
    {
        if(_turret != null) _turret.Purified -= OnTurretPurified;
    }

    private void Update()
    {
        if(_finished) return;

        // La marca solo tiene sentido con el espejo levantado (si lo bajan, pararse ahí no sirve).
        if(_demoPlayed && !_running && _marker != null)
        {
            if(_mirror.IsActive) _marker.Show();
            else _marker.Hide();
        }

        // El espejo se levantó sin el plano de la placa (otra placa, o el plano ya se usó): la demostración arma su propia cinemática.
        if(!_demoPlayed && !_running && _mirror.IsActive && (_reveal == null || !_reveal.IsPlaying))
            StartCoroutine(StandaloneRoutine());
    }

    // Lo llama MirrorRevealCinematic con la cámara todavía en el espejo y Gekko congelado.
    private IEnumerator AfterReveal(PressurePlateRuna plate)
    {
        if(_finished || _demoPlayed || _running || !_mirror.IsActive) return null;
        return DemoRoutine(false);
    }

    private IEnumerator StandaloneRoutine()
    {
        _running = true;
        Freeze(true);
        SetBlend();
        yield return DemoRoutine(true);
        RestoreBlend();
        Freeze(false);
    }

    // waitCameraBack: la demostración arma su propia cinemática, así que espera el viaje de la cámara a la ida y a la vuelta.
    // Pegada al plano de la placa no hace falta: la cámara ya está en el espejo y vuelve junto con la del plano.
    private IEnumerator DemoRoutine(bool waitCameraBack)
    {
        _running = true;
        _demoPlayed = true;

        // Que termine de levantarse (tarda ~0.6 s; el tope es por si algo lo traba).
        for(float t = 0f; t < 3f && !_mirror.raise.IsFullyRaised; t += Time.deltaTime) yield return null;

        if(_camera != null)
        {
            _camera.Priority = _cameraPriority;
            // Pegada al plano de la placa la cámara ya está ahí (Cam_Mirror tiene el mismo encuadre): no hay viaje que esperar.
            if(waitCameraBack) yield return new WaitForSeconds(_blendSeconds);
        }

        if(_marker != null) _marker.Show();
        yield return new WaitForSeconds(_pauseBeforeShots);

        for(int i = 0; i < _demoShots; i++)
        {
            if(i > 0) yield return new WaitForSeconds(_pauseBetweenShots);
            yield return DemoShot();
        }

        yield return new WaitForSeconds(_holdAfterDemo);

        RestoreCamera();
        if(waitCameraBack && _camera != null) yield return new WaitForSeconds(_blendSeconds);

        _running = false;
        StartCoroutine(ShowStandHintWhenFree());
    }

    // Una bala de mentira: de la boca de la torreta hacia la marca, pega en el espejo, se vuelve turquesa y regresa a la torreta.
    private IEnumerator DemoShot()
    {
        if(_bulletPrefab == null || _turret.Muzzle == null) yield break;

        Vector3 start = _turret.Muzzle.position;
        Vector3 aim = (_marker != null ? _marker.transform.position : _mirror.transform.position) + Vector3.up * 0.5f;
        Vector3 dir = (aim - start).normalized;

        // Punto del recorrido más cercano al centro del espejo: ahí "pega".
        Vector3 mirrorCenter = _mirror.Surface != null ? _mirror.Surface.bounds.center : _mirror.transform.position + Vector3.up;
        float hitDistance = Mathf.Max(0.1f, Vector3.Dot(mirrorCenter - start, dir));
        Vector3 hitPoint = start + dir * hitDistance;

        TurretBullet ghost = Instantiate(_bulletPrefab, start, Quaternion.LookRotation(dir));
        ghost.enabled = false; // sin lógica: no se mueve sola, no daña, no vuelve al pool
        foreach(Collider c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        ghost.transform.localScale *= _demoScale;
        foreach(TrailRenderer trail in ghost.GetComponentsInChildren<TrailRenderer>()) trail.widthMultiplier *= _demoScale;

        Light glow = null;
        if(_demoLightIntensity > 0f)
        {
            glow = new GameObject("DemoGlow").AddComponent<Light>();
            glow.transform.SetParent(ghost.transform, false);
            glow.type = LightType.Point;
            glow.range = 3f;
            glow.intensity = _demoLightIntensity;
            glow.color = _demoCorruptColor;
            glow.shadows = LightShadows.None;
        }

        yield return Fly(ghost.transform, start, hitPoint, _demoSpeed);

        ghost.ShowPurifiedLook(true);
        if(glow != null) glow.color = _demoPurifiedColor;
        Vector3 back = _turret.Muzzle.position;
        ghost.transform.rotation = Quaternion.LookRotation(back - hitPoint);
        yield return Fly(ghost.transform, hitPoint, back, _demoSpeed * 0.75f);

        Destroy(ghost.gameObject);
    }

    private static IEnumerator Fly(Transform t, Vector3 from, Vector3 to, float speed)
    {
        float duration = Vector3.Distance(from, to) / Mathf.Max(0.1f, speed);
        for(float k = 0f; k < 1f; k += Time.deltaTime / Mathf.Max(0.01f, duration))
        {
            if(t == null) yield break;
            t.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        if(t != null) t.position = to;
    }

    // El cartel aparece cuando Gekko ya se puede mover (si la demostración iba pegada al plano de la placa, después de que vuelva).
    private IEnumerator ShowStandHintWhenFree()
    {
        while(_reveal != null && _reveal.IsPlaying) yield return null;
        if(!_finished && _hint != null) _hint.Show(_standHintKey, _standHintSeconds);
    }

    private void OnTurretPurified(ChallengeTurret turret)
    {
        if(_finished) return;
        _finished = true;
        _turret.Purified -= OnTurretPurified;

        if(_marker != null) _marker.Hide();
        if(_hint != null) _hint.Show(_doneHintKey, _doneHintSeconds);
    }

    private void RestoreCamera()
    {
        if(_camera != null) _camera.Priority = _cameraBasePriority;
    }

    // Mismo criterio que MirrorRevealCinematic: blend suave con la duración pedida mientras dura la cinemática.
    private void SetBlend()
    {
        Camera main = Camera.main;
        _brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
        if(_brain == null || _blendSaved) return;

        _savedBlend = _brain.DefaultBlend;
        _blendSaved = true;
        _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _blendSeconds);
    }

    private void RestoreBlend()
    {
        if(_brain != null && _blendSaved) _brain.DefaultBlend = _savedBlend;
        _blendSaved = false;
    }

    // Congela a Gekko y avisa a las torretas igual que una cinemática del nivel (las torretas no disparan mientras tanto).
    private void Freeze(bool frozen)
    {
        if(_frozen == frozen) return;
        _frozen = frozen;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null) pj.SetFrozenForCinematic(frozen);
        EventManager.Trigger(frozen ? "OnCinematicStart" : "OnCinematicEnd");
    }
}
