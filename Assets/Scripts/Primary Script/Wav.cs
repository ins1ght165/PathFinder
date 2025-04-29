using System;
using UnityEngine;

public class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavFile, string clipName)
    {
        WAV wav = new WAV(wavFile);
        AudioClip audioClip = AudioClip.Create(clipName, wav.SampleCount, 1, wav.Frequency, false);
        audioClip.SetData(wav.LeftChannel, 0);
        return audioClip;
    }
}

// WAV File Parser
public class WAV
{
    public float[] LeftChannel { get; private set; }
    public int SampleCount { get; private set; }
    public int Frequency { get; private set; }

    public WAV(byte[] wavFile)
    {
        int headerOffset = 44; // WAV header size
        int fileLength = wavFile.Length - headerOffset;

        SampleCount = fileLength / 2;
        LeftChannel = new float[SampleCount];

        for (int i = 0, j = headerOffset; i < SampleCount; i++, j += 2)
        {
            short sample = BitConverter.ToInt16(wavFile, j);
            LeftChannel[i] = sample / 32768.0f;
        }

        Frequency = BitConverter.ToInt32(wavFile, 24);
    }
}
