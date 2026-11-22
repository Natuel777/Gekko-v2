public enum ScenesNames
{
    None = -1,
    LoadScene,
    Menu,
    Level1,
    Level2,
    Intentocin,
    BossLvlOne,
    Aldea
}
public enum ReafirmCanvas
{
    None = -1,
    ExitGame,
    RestartLevel,
    GoToMenu,
    DeleteData
}

// OJO: los Sounds SO guardan este enum como NÚMERO. Los valores son explícitos para que insertar un sonido nuevo
// en el medio no corra los demás (ya pasó: Purify terminó sonando como el gruñido de la planta).
// Sonidos nuevos: SIEMPRE al final, con el número siguiente. Nunca renumerar ni reutilizar uno existente.
public enum SoundNames
{
    None = -1,
    Menu = 0,
    LvlOne = 1,
    UiButton = 2,
    FinishPuzzle = 3,
    PlayerJump = 4,
    PlayerLanding = 5,
    PlayerSlurp = 6,
    PlayerWalk = 7,
    PlayerTongueOut = 8,
    PlankPlaced = 9,
    PlayerSwingAttach = 10,
    PlayerSwing = 11,
    PlayerHurt = 12,
    PlayerSplash = 13,
    PlayerSwim = 14,
    BeaverVoice = 15,
    Duck = 16,
    MushroomBoing = 17,
    BeatleHurt = 18,
    PlantHurt = 19,
    Purify = 20,
    Shock = 21,
    PlantGrowl = 22,
    PlantShoot = 23,
    RuneActivation = 24,
    TurretShoot = 25,
    TurretBullet = 26,
}

public enum LevelFlags
{
    None = -1,
    Lvl1_01,
    Lvl1_02,
    Lvl1_purification1,
    Lvl1_ladybug,
    Lvl1_Ducks,
    Lvl1_purification2,
    Lvl1_03,
    Lvl1_04,
    Lvl1_05,
    Lvl1_purificationBoss,
    Lvl2_Beaver,
    Lvl2_01,
    Lvl2_Purification1,
    Lvl2_02,
    Lvl2_camp,
    Lvl2_03,
    Lvl2_HideSeek_1,
    Lvl2_HideSeek_2,
    Lvl2_purification2,
}
