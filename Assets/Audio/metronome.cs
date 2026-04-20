using UnityEngine;
using System.Collections;
using System;

// The code example shows how to implement a metronome that procedurally generates the click sounds via the OnAudioFilterRead callback.
// While the game is paused or suspended, this time will not be updated and sounds playing will be paused. Therefore developers of music scheduling routines do not have to do any rescheduling after the app is unpaused

[RequireComponent(typeof(AudioSource))]
public class ExampleClass : MonoBehaviour
{
    public double bpm = 48.0F;
    public float gain = 0.5F;
    public int signatureHi = 4;
    public int signatureLo = 4;
    private double nextTick = 0.0F;
    private float amp = 0.0F;
    private float phase = 0.0F;
    private double sampleRate = 0.0F;
    private int accent;
    private bool running = false;

    // Measure tracking
    private int currentMeasure = 0;
    private int currentBeat = 0;
    private bool hasPlayedPickupBeat = false; // Track if pickup beat has been played

    // Events for measure and beat changes
    public event Action<int, int> OnBeatChanged; // (measure, beat)
    public event Action<int> OnMeasureChanged;   // (measure)

    // Static instance for easy access
    public static ExampleClass Instance { get; private set; }
    void Start()
    {
        Instance = this;
        accent = signatureHi;
        currentMeasure = 0;
        currentBeat = 0;
        hasPlayedPickupBeat = false;
        double startTick = AudioSettings.dspTime;
        sampleRate = AudioSettings.outputSampleRate;
        nextTick = startTick * sampleRate;
        running = true;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!running)
            return;

        double samplesPerTick = sampleRate * 60.0F / bpm * 4.0F / signatureLo;
        double sample = AudioSettings.dspTime * sampleRate;
        int dataLen = data.Length / channels;
        int n = 0;
        while (n < dataLen)
        {
            float x = gain * amp * Mathf.Sin(phase);
            int i = 0;
            while (i < channels)
            {
                data[n * channels + i] += x;
                i++;
            }
            while (sample + n >= nextTick)
            {
                nextTick += samplesPerTick;
                amp = 1.0F;
                
                int previousBeat = currentBeat;
                if (++accent > signatureHi)
                {
                    accent = 1;
                    amp *= 2.0F;
                    
                    if (!hasPlayedPickupBeat)
                    {
                        hasPlayedPickupBeat = true;
                        // Pickup beat played, don't increment measure yet
                    }
                    else
                    {
                        currentMeasure++;
                        OnMeasureChanged?.Invoke(currentMeasure);
                    }
                }
                currentBeat = accent - 1; // 0-indexed
                
                Debug.Log("Tick: " + accent + "/" + signatureHi + " (Measure: " + currentMeasure + ", Beat: " + currentBeat + ")");
                OnBeatChanged?.Invoke(currentMeasure, currentBeat);
            }
            phase += amp * 0.3F;
            amp *= 0.993F;
            n++;
        }
    }

    // Public methods to query current position
    public int GetCurrentMeasure() => currentMeasure;
    public int GetCurrentBeat() => currentBeat;
    
    public void ResetMeasureCount()
    {
        currentMeasure = 0;
        currentBeat = 0;
        hasPlayedPickupBeat = false;
    }
}