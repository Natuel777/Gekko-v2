using UnityEngine;

// Picks a random sound out of a list, never the same one twice in a row.
// The last sound picked is left out of the next draw, so it has to wait one turn before it can sound again.
// The list is read on every pick, so editing the array in the Inspector during Play Mode takes effect right away.
public class NoRepeatSoundPicker
{
    private readonly SoundNames[] _sounds;
    private SoundNames _last = SoundNames.None;

    public NoRepeatSoundPicker(SoundNames[] sounds)
    {
        _sounds = sounds;
    }

    // Returns false when there is nothing valid to play (empty list, or only SoundNames.None entries).
    public bool TryPick(out SoundNames sound)
    {
        sound = SoundNames.None;
        if(_sounds == null) return false;

        // Prefer the sounds that differ from the last one. If there are none (the list holds a single distinct
        // sound), the repetition can't be avoided, so it is allowed instead of staying silent.
        bool excludeLast = true;
        int options = CountOptions(excludeLast);
        if(options == 0)
        {
            excludeLast = false;
            options = CountOptions(excludeLast);
        }
        if(options == 0) return false;

        int pick = Random.Range(0, options);
        int seen = 0;
        for(int i = 0; i < _sounds.Length; i++)
        {
            if(!IsOption(_sounds[i], excludeLast)) continue;

            if(seen == pick)
            {
                sound = _sounds[i];
                break;
            }
            seen++;
        }

        _last = sound;
        return true;
    }

    private int CountOptions(bool excludeLast)
    {
        int count = 0;
        for(int i = 0; i < _sounds.Length; i++)
        {
            if(IsOption(_sounds[i], excludeLast)) count++;
        }
        return count;
    }

    private bool IsOption(SoundNames sound, bool excludeLast)
    {
        if(sound == SoundNames.None) return false;
        return !(excludeLast && sound == _last);
    }
}
