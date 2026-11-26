using System.Text.Json;
namespace GhosttzzKBot;
sealed class Settings
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GhosttzzKBot");
    public static readonly string Folder = Path.Combine(Root, "config");
    public static readonly string DefaultArt = Path.Combine(AppContext.BaseDirectory, "assets", "background.png");
    public static readonly string Art = Path.Combine(Folder, "background.png");
    static readonly string FileName = Path.Combine(Folder, "settings.json");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public int[] Keys { get; set; } = [116, 117, 118, 119];
    public bool Move { get; set; } = true;
    public bool Clicks { get; set; } = true;
    public bool Keyboard { get; set; } = true;
    public bool Wheel { get; set; } = true;
    public bool PathVisible { get; set; } = true;
    public bool Repeat { get; set; }
    public int Interval { get; set; } = 100;
    public int Sample { get; set; } = 12;
    public int Mode { get; set; }
    public double Speed { get; set; } = 1;
    public int Glow { get; set; } = 100;
    public string Accent { get; set; } = "#00aaff";
    public string UpdateRepository { get; set; } = "Favelado157/Zyon_Macros";
    public static Settings Load()
    {
        Settings s = new();
        try { if (File.Exists(FileName)) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? s; }
        catch (JsonException) { }
        if (s.Keys is not { Length: 4 } || s.Keys.Distinct().Count() != 4 || s.Keys.Any(k => k < 113 || k > 123)) s.Keys = [116,117,118,119];
        s.Interval = Math.Clamp(s.Interval, 1, 60000);
        s.Sample = Math.Clamp(s.Sample, 1, 1000);
        s.Speed = double.IsFinite(s.Speed) ? Math.Clamp(s.Speed, .25, 3) : 1;
        s.Glow = Math.Clamp(s.Glow, 0, 100);
        s.Mode = Math.Clamp(s.Mode, 0, 1);
        if (string.IsNullOrWhiteSpace(s.Accent)) s.Accent = "#00aaff";
        return s;
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName + ".tmp", JsonSerializer.Serialize(this, Json));
        File.Move(FileName + ".tmp", FileName, true);
    }
}
