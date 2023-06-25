using UnityEngine;
using System.Collections.Generic;

[System.Serializable][CreateAssetMenu]
public class WaveData : ScriptableObject
{
    public List<Wave> Waves = new List<Wave>();
    public BasicWaves BasicWaveSettings = new BasicWaves(1.5f, 45.0f, 5.0f);
    public int RandomSeed = 3234;
    public float WaterMaxVisibility = 40;
}

[System.Serializable]
public struct Wave
{
    public float Amplitude;
    public float Direction;
    public float WaveLength;
    public Wave(float amp, float dir, float len)
    {
        Amplitude = amp;
        Direction = dir;
        WaveLength = len;
    }
}

[System.Serializable]
public class BasicWaves
{
    public int NumWaves = 6;
    public float Amplitude;
    public float Direction;
    public float WaveLength;

    public BasicWaves(float amp, float dir, float len)
    {
        NumWaves = 6;
        Amplitude = amp;
        Direction = dir;
        WaveLength = len;
    }
}
