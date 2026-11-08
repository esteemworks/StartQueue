namespace StartQueue.Core;

/// <summary>
/// The colors a profile can have are the 16 of the family (<see cref="Palette"/>); the family's profiles keep theirs
/// (<see cref="FamilyProfiles.ColorOf"/>). Standard always has the StartQueue colors, Ember. Around them StartQueue
/// keeps what only it needs: the color names of its earlier versions.
/// </summary>
public static class ProfileColors
{
    public const string StartQueueId = "glut";

    /// <summary>Colors of earlier versions, taken to the one of today that comes closest.</summary>
    static readonly Dictionary<string, string> Earlier = new()
    {
        ["koralle"] = "flamingo", ["rubin"] = "kirsche", ["magenta"] = "flamingo", ["orchidee"] = "lavendel",
        ["violett"] = "lavendel", ["indigo"] = "mitternacht", ["ozean"] = "himmel", ["tuerkis"] = "mint",
    };

    public static PaletteColor StartQueue => Palette.Ember;

    public static PaletteColor? Find(string? id)
    {
        if (id != null && Earlier.TryGetValue(id, out var today)) id = today;
        return Palette.Find(id);
    }

    /// <summary>The profile's color; Standard (null) has StartQueue's.</summary>
    public static PaletteColor Of(FamilyProfile? profile) => profile == null ? StartQueue : FamilyProfiles.ColorOf(FamilyProfiles.Current.Profiles, profile);
}