using System.Collections;
using System.IO;
using UnityEngine;

// Records the game frame by frame to edit the trailer (only with -capture <folder>).
// Game time advances exactly 1/30 s per frame no matter how long saving takes, so the
// footage is smooth. Next to the frames it writes events.txt ("frame<TAB>event": rounds,
// results, sounds...) and the sound effects as .wav, to rebuild the audio when editing.
public class TrailerCapture : MonoBehaviour
{
    public const int Fps = 30;

    private static TrailerCapture instance;
    private string folder;
    private StreamWriter events;
    private int frame;

    public static void StartIfRequested(GameObject host)
    {
        if (string.IsNullOrEmpty(Autopilot.CaptureFolder) || instance != null) return;
        instance = host.AddComponent<TrailerCapture>();
    }

    void Awake()
    {
        folder = Autopilot.CaptureFolder;
        Directory.CreateDirectory(Path.Combine(folder, "frames"));
        RuntimeSounds.ExportAll(Path.Combine(folder, "sfx"));
        events = new StreamWriter(Path.Combine(folder, "events.txt")) { AutoFlush = true };
        Time.captureFramerate = Fps;
        StartCoroutine(CaptureRoutine());
    }

    public static void Log(string message)
    {
        if (instance == null) return;
        instance.events.WriteLine($"{instance.frame}\t{message}");
    }

    private IEnumerator CaptureRoutine()
    {
        WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "frames", $"f{frame:D6}.jpg"), shot.EncodeToJPG(92));
            Destroy(shot);
            frame++;
        }
    }

    // When the recording is done (after the ending) the game closes
    public static void Finish()
    {
        if (instance == null) return;
        Log("end");
        instance.events.Close();
        instance = null; // the menu still logs while the game is quitting
        Application.Quit();
    }
}
