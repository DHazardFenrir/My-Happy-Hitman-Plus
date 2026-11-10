using UnityEngine;

// The drone the player is training this run. It gets a random name when the game starts,
// it's destroyed after a failed job (the "real life" death) and it earns a star at the target score.
public static class AssignedDrone
{
    private static readonly string[] Models = { "RVN", "HK", "MQ", "SPX", "VLT", "KTR", "ZEP", "OWL" };
    private static readonly string[] Nicknames =
    {
        "PICKLES", "BISCUIT", "SPARKY", "NUGGET", "DOLORES", "PANCHO", "WALDO", "MOCHI",
        "CHUY", "RAMIRO", "TOASTER", "BEANS", "LUPITA", "GOMEZ", "MUFFIN", "TOFU",
        "CHICHARRON", "BUBBLES", "KEVIN", "MR. WHISKERS", "PAQUITA", "TACO", "GERALD", "PELUCHE"
    };

    // e.g. RVN-27 "PICKLES"
    public static string Name => $"{Code} \"{Nickname}\"";
    // e.g. RVN-27 PICKLES (for camera labels)
    public static string Tag => $"{Code} {Nickname}";
    public static string Code { get; private set; } = "TR8-01";
    public static string Nickname { get; private set; } = "TRAINEE";
    public static bool Certified { get; private set; }

    public static void AssignNew()
    {
        string previous = Nickname;
        Code = $"{Models[Random.Range(0, Models.Length)]}-{Random.Range(10, 100)}";
        do Nickname = Nicknames[Random.Range(0, Nicknames.Length)];
        while (Nickname == previous);
        Certified = false;
        TrailerCapture.Log($"drone assigned {Name}");
    }

    public static void Certify() => Certified = true;
}
