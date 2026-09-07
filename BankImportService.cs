using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QNB;

internal sealed class BankImportResult
{
    public string SourceFile { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountDisplayName { get; set; } = string.Empty;
    public string AccountReference { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public BankAccountType AccountType { get; set; } = BankAccountType.Courant;
    public DateTime? BalanceDate { get; set; }
    public decimal? Balance { get; set; }
    public string Currency { get; set; } = "EUR";
    public List<BankOperation> Operations { get; set; } = new();
}

internal sealed class BankOperation
{
    public DateTime Date { get; set; }
    public string Nature { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime? ValueDate { get; set; }
    public string InterbankLabel { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsDeferredCardSummary { get; set; }
    public decimal Amount => Credit + Debit;
}

internal static class BankImportService
{
    public static BankImportResult ImportCsv(string filePath)
    {
        var lines = ReadAllLinesShared(filePath);
        if (lines.Count < 7)
            throw new InvalidDataException("Le fichier CSV ne contient pas suffisamment de lignes.");

        var rows = lines.Select(ParseCsvLine).ToList();
        var result = new BankImportResult
        {
            SourceFile = filePath,
            BankName = Cell(rows, 0, 0).Trim(),
            AccountReference = Cell(rows, 1, 0).Trim(),
            AccountHolder = Cell(rows, 1, 1).Trim(),
            BalanceDate = ParseDate(Cell(rows, 3, 1)),
            Balance = ParseAmountNullable(Cell(rows, 4, 1)),
            Currency = string.IsNullOrWhiteSpace(Cell(rows, 4, 2)) ? "EUR" : Cell(rows, 4, 2).Trim()
        };

        var headerIndex = rows.FindIndex(row => row.Count > 0 && string.Equals(row[0].Trim(), "Date", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
            throw new InvalidDataException("La ligne d'en-tête des opérations est introuvable.");

        BankOperation? current = null;
        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var dateText = Cell(row, 0).Trim();

            if (DateTime.TryParseExact(dateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var operationDate))
            {
                current = new BankOperation
                {
                    Date = operationDate,
                    Nature = Cell(row, 1).Trim(),
                    Debit = ParseAmount(Cell(row, 2)),
                    Credit = ParseAmount(Cell(row, 3)),
                    Currency = string.IsNullOrWhiteSpace(Cell(row, 4)) ? result.Currency : Cell(row, 4).Trim(),
                    ValueDate = ParseDate(Cell(row, 5)),
                    InterbankLabel = Cell(row, 6).Trim()
                };
                result.Operations.Add(current);
            }
            else if (current is not null)
            {
                var detail = Cell(row, 1).Trim();
                if (!string.IsNullOrWhiteSpace(detail))
                    current.Details = string.IsNullOrWhiteSpace(current.Details) ? detail : current.Details + " | " + detail;
            }
        }

        if (result.Operations.Count == 0)
            throw new InvalidDataException("Aucune opération bancaire n'a été détectée dans le fichier.");

        DetectDeferredCardSummaries(result);
        return result;
    }

    public static void BindAccount(BankImportResult result, BankAccountProfile account)
    {
        result.AccountId = account.Id;
        result.BankName = account.BankName;
        result.AccountDisplayName = account.AccountName;
        result.AccountReference = account.AccountReference;
        result.AccountHolder = account.Holder;
        result.AccountType = account.Type;

        if (account.Type == BankAccountType.CarteDifferee)
        {
            foreach (var operation in result.Operations)
                operation.IsDeferredCardSummary = true;
        }
        else
        {
            DetectDeferredCardSummaries(result);
        }
    }

    public static string SaveImport(BankImportResult result)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB", "Imports");
        Directory.CreateDirectory(directory);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"import-{stamp}.json");
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }

    private static void DetectDeferredCardSummaries(BankImportResult result)
    {
        foreach (var operation in result.Operations)
        {
            var text = $"{operation.Nature} {operation.InterbankLabel} {operation.Details}".ToUpperInvariant();
            var mentionsCard = text.Contains("CARTE") || text.Contains(" CB ") || text.StartsWith("CB ") || text.Contains("C.B.");
            var mentionsDeferred = text.Contains("DIFFERE") || text.Contains("DIFFÉRÉ") || text.Contains("RELEVE CARTE") || text.Contains("RELEVÉ CARTE");
            if (mentionsCard && mentionsDeferred)
                operation.IsDeferredCardSummary = true;
        }
    }

    private static List<string> ReadAllLinesShared(string filePath)
    {
        var lines = new List<string>();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ';' && !inQuotes) { values.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }

        values.Add(current.ToString());
        if (values.Count > 0) values[0] = values[0].TrimStart('\uFEFF');
        return values;
    }

    private static string Cell(IReadOnlyList<List<string>> rows, int row, int column) =>
        row >= 0 && row < rows.Count ? Cell(rows[row], column) : string.Empty;

    private static string Cell(IReadOnlyList<string> row, int column) =>
        column >= 0 && column < row.Count ? row[column] : string.Empty;

    private static DateTime? ParseDate(string text) =>
        DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    private static decimal ParseAmount(string text) => ParseAmountNullable(text) ?? 0m;

    private static decimal? ParseAmountNullable(string text)
    {
        var normalized = text.Trim().Replace(" ", string.Empty).Replace("\u00A0", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("fr-FR"), out var value) ? value : null;
    }
}
