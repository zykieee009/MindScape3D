using System;
using System.Collections.Generic;
using UnityEngine;

// Which music track each scene plays (Resources/SceneMusicMap). A scene uses the entry with the longest matching
// name prefix, so "Chapter 3" covers every Chapter 3 episode and "Chapter 3 - Episode 8" overrides it. A scene with
// no entry keeps whatever is playing. Tracks are Resources/Music clip names.
[CreateAssetMenu(fileName = "SceneMusicMap", menuName = "Mindscape/Scene Music Map")]
public class SceneMusicMap : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string scenePrefix;
        public string track;
    }

    public List<Entry> entries = new List<Entry>();

    [Tooltip("Tracks kept out of the Next-track playlist. They only play where an entry names them.")]
    public List<string> reservedTracks = new List<string>();

    [Min(0.1f)] public float crossfadeSeconds = 1.5f;

    public string TrackFor(string sceneName)
    {
        string best = null;
        int bestLength = -1;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.scenePrefix) || e.scenePrefix.Length <= bestLength) continue;
            if (!sceneName.StartsWith(e.scenePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            best = e.track;
            bestLength = e.scenePrefix.Length;
        }
        return best;
    }
}
