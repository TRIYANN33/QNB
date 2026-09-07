using System.Text.Json;

namespace QNB;

public sealed class AppUsageInfo
{
    public DateTime CreationDate { get; set; }
    public DateTime? PreviousUseDate { get; set; }
    public DateTime LastUseDate { get; set; }

    private static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB");

    private static string DataFilePath => Path.Combine(DataDirectory, "usage.json");

    public static AppUsageInfo LoadAndRegisterCurrentUse()
    {
        Directory.CreateDirectory(DataDirectory);

        var now = DateTime.Now;
        AppUsageInfo info;

        try
        {
            if (File.Exists(DataFilePath))
            {
                var json = File.ReadAllText(DataFilePath);
                info = JsonSerializer.Deserialize<AppUsageInfo>(json) ?? new AppUsageInfo();

                if (info.CreationDate == default)
                    info.CreationDate = now;

                info.PreviousUseDate = info.LastUseDate == default ? null : info.LastUseDate;
                info.LastUseDate = now;
            }
            else
            {
                info = new AppUsageInfo
                {
                    CreationDate = now,
                    PreviousUseDate = null,
                    LastUseDate = now
                };
            }

            File.WriteAllText(
                DataFilePath,
                JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            info = new AppUsageInfo
            {
                CreationDate = now,
                PreviousUseDate = null,
                LastUseDate = now
            };
        }

        return info;
    }
}
