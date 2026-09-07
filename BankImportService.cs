using ExcelDataReader;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    public static BankImportResult ImportExcel(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        do
        {
            var rows = new List<List<object?>>();
            while (reader.Read())
            {
                var row = new List<object?>(reader.FieldCount);
                for (var column = 0; column < reader.FieldCount; column++)
                    row.Add(reader.GetValue(column));
                rows.Add(row);
            }

            var headerIndex = FindExcelHeader(rows);
            if (headerIndex >= 0)
                return ParseExcelStatement(filePath, rows, headerIndex);
        }
        while (reader.NextResult());

        throw new InvalidDataException("Aucune feuille Excel contenant les colonnes Date, Libellé, Débit et Crédit n'a été détectée.");
    }

    private static BankImportResult ParseExcelStatement(string filePath, List<List<object?>> rows, int headerIndex)
    {
        var result = new BankImportResult
        {
            SourceFile = filePath,
            Currency = "EUR"
        };

        for (var i = 0; i < headerIndex; i++)
        {
            var first = ExcelCellText(rows[i], 0);
            var second = ExcelCellText(rows[i], 1);

            if (first.StartsWith("Compte ", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(first, @"n\s*[°º]?\s*([A-Z0-9 ]+)$", RegexOptions.IgnoreCase);
                result.AccountReference = match.Success ? match.Groups[1].Value.Trim() : first.Trim();
                result.AccountHolder = FindPreviousNonEmpty(rows, i);
            }

            if (second.StartsWith("Solde au ", StringComparison.OrdinalIgnoreCase))
            {
                result.BalanceDate = ParseDate(second["Solde au ".Length..]);
                result.Balance = ExcelCellAmount(rows[i], 2);
            }
        }

        if (rows.Take(headerIndex).Any(row => ExcelCellText(row, 0).StartsWith("Téléchargement du ", StringComparison.OrdinalIgnoreCase))
            && rows.Take(headerIndex).Any(row => ExcelCellText(row, 0).StartsWith("Compte courant", StringComparison.OrdinalIgnoreCase)))
        {
            result.BankName = "Crédit Agricole";
        }

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var date = ExcelCellDate(rows[i], 0);
            if (!date.HasValue) continue;

            var label = ExcelCellText(rows[i], 1).Trim();
            var debitValue = ExcelCellAmount(rows[i], 2) ?? 0m;
            var creditValue = ExcelCellAmount(rows[i], 3) ?? 0m;

            if (string.IsNullOrWhiteSpace(label) && debitValue == 0m && creditValue == 0m)
                continue;

            result.Operations.Add(new BankOperation
            {
                Date = date.Value,
                Nature = ExtractNature(label),
                Debit = debitValue == 0m ? 0m : -Math.Abs(debitValue),
                Credit = creditValue == 0m ? 0m : Math.Abs(creditValue),
                Currency = result.Currency,
                InterbankLabel = label,
                Details = label.Contains('\n') ? label.Replace("\r", string.Empty).Replace("\n", " | ") : string.Empty
            });
        }

        if (result.Operations.Count == 0)
            throw new InvalidDataException("Aucune opération bancaire n'a été détectée dans le fichier Excel.");

        DetectDeferredCardSummaries(result);
        return result;
    }

    private static int FindExcelHeader(List<List<object?>> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.Equals(ExcelCellText(rows[i], 0).Trim(), "Date", StringComparison.OrdinalIgnoreCase)
                && ExcelCellText(rows[i], 1).Contains("Libell", StringComparison.OrdinalIgnoreCase)
                && ExcelCellText(rows[i], 2).Contains("Débit", StringComparison.OrdinalIgnoreCase)
                && ExcelCellText(rows[i], 3).Contains("Crédit", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static string FindPreviousNonEmpty(List<List<object?>> rows, int beforeIndex)
    {
        for (var i = beforeIndex - 1; i >= 0; i--)
        {
            var value = ExcelCellText(rows[i], 0).Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return string.Empty;
    }

    private static string ExtractNature(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        var firstLine = label.Replace("\r", string.Empty).Split('\n')[0].Trim();
        var separator = firstLine.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? firstLine[..separator].Trim() : firstLine;
    }

    private static string ExcelCellText(IReadOnlyList<object?> row, int column)
    {
        if (column < 0 || column >= row.Count || row[column] is null) return string.Empty;
        var value = row[column];
        return value switch
        {
            DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.GetCultureInfo("fr-FR")) ?? string.Empty
        };
    }

    private static DateTime? ExcelCellDate(IReadOnlyList<object?> row, int column)
    {
        if (column < 0 || column >= row.Count || row[column] is null) return null;
        var value = row[column];
        if (value is DateTime date) return date.Date;
        if (value is double oa && oa > 1 && oa < 100000)
        {
            try { return DateTime.FromOADate(oa).Date; }
            catch { }
        }

        var text = ExcelCellText(row, column).Trim();
        if (DateTime.TryParseExact(text, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.Date;
        return null;
    }

    private static decimal? ExcelCellAmount(IReadOnlyList<object?> row, int column)
    {
        if (column < 0 || column >= row.Count || row[column] is null) return null;
        var value = row[column];
        return value switch
        {
            decimal d => d,
            double d => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
            float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
            int i => i,
            long l => l,
            _ => ParseAmountNullable(ExcelCellText(row, column))
        };
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
        DateTime.TryParseExact(text.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    private static decimal ParseAmount(string text) => ParseAmountNullable(text) ?? 0m;

    private static decimal? ParseAmountNullable(string text)
    {
        var normalized = text.Trim()
            .Replace("€", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("\u202F", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("fr-FR"), out var value) ? value : null;
    }
}
