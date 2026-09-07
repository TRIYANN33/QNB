using System.Text.Json;

namespace QNB;

public sealed class AppUsageInfo
{
    public DateTime CreationDate { get; set; }
    public DateTime? PreviousUseDate { get; set; }
    public DateTime LastUseDate { get; set; }

    private const string ResetVersion = "2026-09-07-clean-1";
    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB");
    private static string DataFilePath => Path.Combine(DataDirectory, "usage.json");
    private static string ResetMarkerPath => Path.Combine(DataDirectory, $"usage-reset-{ResetVersion}.marker");

    public static AppUsageInfo LoadAndRegisterCurrentUse()
    {
        Directory.CreateDirectory(DataDirectory);
        var now = DateTime.Now;

        // Réinitialisation unique demandée : dates de création/utilisation repartent de zéro logique
        // au premier lancement de cette version. Le marqueur empêche un nouveau reset à chaque démarrage.
        if (!File.Exists(ResetMarkerPath))
        {
            var fresh = new AppUsageInfo { CreationDate = now, PreviousUseDate = null, LastUseDate = now };
            try
            {
                File.WriteAllText(DataFilePath, JsonSerializer.Serialize(fresh, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(ResetMarkerPath, now.ToString("O"));
            }
            catch { }
            return fresh;
        }

        AppUsageInfo info;
        try
        {
            if (File.Exists(DataFilePath))
            {
                var json = File.ReadAllText(DataFilePath);
                info = JsonSerializer.Deserialize<AppUsageInfo>(json) ?? new AppUsageInfo();
                if (info.CreationDate == default) info.CreationDate = now;
                info.PreviousUseDate = info.LastUseDate == default ? null : info.LastUseDate;
                info.LastUseDate = now;
            }
            else
            {
                info = new AppUsageInfo { CreationDate = now, PreviousUseDate = null, LastUseDate = now };
            }

            File.WriteAllText(DataFilePath, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            info = new AppUsageInfo { CreationDate = now, PreviousUseDate = null, LastUseDate = now };
        }

        return info;
    }
}
