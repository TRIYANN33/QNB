using System.Globalization;
using System.Text;

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

internal sealed class ImportDuplicateCheckResult
{
    public int TotalOperations { get; set; }
    public List<BankOperation> NewOperations { get; set; } = new();
    public List<BankOperation> DuplicateOperations { get; set; } = new();
    public int DuplicateCount => DuplicateOperations.Count;
    public int NewCount => NewOperations.Count;
}

internal sealed class ImportSaveResult
{
    public string? SavedPath { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
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

    public static ImportDuplicateCheckResult CheckDuplicates(BankImportResult result)
    {
        var check = new ImportDuplicateCheckResult { TotalOperations = result.Operations.Count };
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var previousImport in BankingRepository.LoadImports().Where(previous => SameAccount(previous, result)))
        {
            foreach (var operation in previousImport.Operations)
                known.Add(BuildOperationFingerprint(operation));
        }

        foreach (var operation in result.Operations)
        {
            var fingerprint = BuildOperationFingerprint(operation);
            if (!known.Add(fingerprint))
                check.DuplicateOperations.Add(operation);
            else
                check.NewOperations.Add(operation);
        }

        return check;
    }

    public static ImportSaveResult SaveImportWithoutDuplicates(BankImportResult result)
    {
        var check = CheckDuplicates(result);
        if (check.NewCount == 0)
        {
            return new ImportSaveResult
            {
                SavedPath = null,
                ImportedCount = 0,
                DuplicateCount = check.DuplicateCount
            };
        }

        var filtered = CloneWithOperations(result, check.NewOperations);
        var path = BankingRepository.SaveImport(filtered);
        return new ImportSaveResult
        {
            SavedPath = path,
            ImportedCount = check.NewCount,
            DuplicateCount = check.DuplicateCount
        };
    }

    public static string SaveImport(BankImportResult result)
    {
        var check = CheckDuplicates(result);

        if (check.DuplicateCount > 0)
        {
            MessageBox.Show(
                $"Vérification des doublons terminée.\n\n" +
                $"Opérations analysées : {check.TotalOperations}\n" +
                $"Nouvelles opérations : {check.NewCount}\n" +
                $"Doublons détectés : {check.DuplicateCount}\n\n" +
                "Les doublons seront automatiquement exclus du transfert.",
                "QNB - Contrôle des doublons",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        if (check.NewCount == 0)
        {
            MessageBox.Show(
                "Aucune nouvelle opération à importer. Toutes les lignes du relevé existent déjà pour ce compte.",
                "QNB - Aucun transfert nécessaire",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return string.Empty;
        }

        var filtered = CloneWithOperations(result, check.NewOperations);
        return BankingRepository.SaveImport(filtered);
    }

    private static BankImportResult CloneWithOperations(BankImportResult result, List<BankOperation> operations)
    {
        return new BankImportResult
        {
            SourceFile = result.SourceFile,
            AccountId = result.AccountId,
            BankName = result.BankName,
            AccountDisplayName = result.AccountDisplayName,
            AccountReference = result.AccountReference,
            AccountHolder = result.AccountHolder,
            AccountType = result.AccountType,
            BalanceDate = result.BalanceDate,
            Balance = result.Balance,
            Currency = result.Currency,
            Operations = operations
        };
    }

    private static bool SameAccount(BankImportResult left, BankImportResult right)
    {
        if (left.AccountId.HasValue && right.AccountId.HasValue)
            return left.AccountId.Value == right.AccountId.Value;

        return Normalize(left.BankName) == Normalize(right.BankName)
            && Normalize(left.AccountReference) == Normalize(right.AccountReference);
    }

    private static string BuildOperationFingerprint(BankOperation operation)
    {
        return string.Join("|",
            operation.Date.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            operation.ValueDate?.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? string.Empty,
            operation.Debit.ToString("0.00##", CultureInfo.InvariantCulture),
            operation.Credit.ToString("0.00##", CultureInfo.InvariantCulture),
            Normalize(operation.Currency),
            Normalize(operation.Nature),
            Normalize(operation.InterbankLabel),
            Normalize(operation.Details));
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim().ToUpperInvariant();
        var builder = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace) builder.Append(' ');
                previousWasSpace = true;
            }
            else
            {
                builder.Append(ch);
                previousWasSpace = false;
            }
        }
        return builder.ToString();
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
