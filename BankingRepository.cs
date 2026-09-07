using System.Text.Json;

namespace QNB;

internal enum BankAccountType
{
    Courant,
    Epargne,
    CarteDifferee,
    Autre
}

internal sealed class BankAccountProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountReference { get; set; } = string.Empty;
    public string Holder { get; set; } = string.Empty;
    public BankAccountType Type { get; set; } = BankAccountType.Courant;
    public string DisplayName => $"{BankName} — {AccountName}";
}

internal sealed class BankingConfiguration
{
    public List<BankAccountProfile> Accounts { get; set; } = new();
}

internal sealed class DashboardBankingStats
{
    public int Documents { get; set; }
    public int Operations { get; set; }
    public int Accounts { get; set; }
    public decimal DeferredCardAmount { get; set; }
}

internal static class BankingRepository
{
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB");
    private static string ConfigPath => Path.Combine(Root, "banking.json");
    private static string ImportsDirectory => Path.Combine(Root, "Imports");

    public static BankingConfiguration LoadConfiguration()
    {
        try
        {
            Directory.CreateDirectory(Root);
            if (!File.Exists(ConfigPath)) return new BankingConfiguration();
            return JsonSerializer.Deserialize<BankingConfiguration>(File.ReadAllText(ConfigPath)) ?? new BankingConfiguration();
        }
        catch
        {
            return new BankingConfiguration();
        }
    }

    public static void SaveConfiguration(BankingConfiguration configuration)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static BankAccountProfile SaveAccount(BankAccountProfile account)
    {
        var configuration = LoadConfiguration();
        var existing = configuration.Accounts.FirstOrDefault(x => x.Id == account.Id);
        if (existing is null)
            configuration.Accounts.Add(account);
        else
        {
            existing.BankName = account.BankName;
            existing.AccountName = account.AccountName;
            existing.AccountReference = account.AccountReference;
            existing.Holder = account.Holder;
            existing.Type = account.Type;
        }
        SaveConfiguration(configuration);
        return account;
    }

    public static List<BankImportResult> LoadImports()
    {
        var results = new List<BankImportResult>();
        if (!Directory.Exists(ImportsDirectory)) return results;

        foreach (var file in Directory.GetFiles(ImportsDirectory, "import-*.json").OrderBy(x => x))
        {
            try
            {
                var result = JsonSerializer.Deserialize<BankImportResult>(File.ReadAllText(file));
                if (result is not null) results.Add(result);
            }
            catch
            {
                // Un ancien import illisible ne bloque pas les autres imports.
            }
        }
        return results;
    }

    public static DashboardBankingStats GetDashboardStats()
    {
        var imports = LoadImports();
        var configuration = LoadConfiguration();
        return new DashboardBankingStats
        {
            Documents = imports.Count,
            Operations = imports.Sum(x => x.Operations.Count),
            Accounts = configuration.Accounts.Count,
            DeferredCardAmount = imports
                .SelectMany(x => x.Operations)
                .Where(x => x.IsDeferredCardSummary)
                .Sum(x => Math.Abs(x.Amount))
        };
    }
}
