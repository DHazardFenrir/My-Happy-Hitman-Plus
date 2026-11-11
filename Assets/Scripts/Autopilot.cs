using System;
using UnityEngine;

// The game plays itself (attract mode / recording the trailer). Command line options:
//   -autopilot          the bot presses the button at the right time
//   -missChance 0.2     chance of pressing at the wrong time on purpose
//   -failsForIrl 3      fails before the "real life" death (default: the GameManager value)
//   -capture <folder>   saves every frame at 30 fps + events.txt + the sound effects (see TrailerCapture)
public static class Autopilot
{
    public static bool Enabled { get; private set; }
    public static float MissChance { get; private set; } = 0.15f;
    public static int FailsForIrl { get; private set; } = -1;
    public static string CaptureFolder { get; private set; }

    private static int simulatedPressFrame = -1;

    // Reads the command line once, before the first scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "-autopilot": Enabled = true; break;
                case "-missChance": if (float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m)) MissChance = m; break;
                case "-failsForIrl": if (int.TryParse(next, out int f)) FailsForIrl = f; break;
                case "-capture": CaptureFolder = next; break;
            }
        }

        // Keeps playing (and recording) even if the window loses focus
        if (Enabled) Application.runInBackground = true;
    }

    // The bot "presses" the button this frame (read through SingleInput.ActionPressed)
    public static void Press() => simulatedPressFrame = Time.frameCount;

    public static bool PressedThisFrame => simulatedPressFrame == Time.frameCount;

    // The bot keeps the button down (read through SingleInput.ActionHeld)
    public static bool Holding { get; private set; }
    public static void Hold(bool down)
    {
        if (down && !Holding) Press();
        Holding = down;
    }
}
