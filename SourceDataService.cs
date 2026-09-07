using Microsoft.Data.Sqlite;

namespace QNB;

internal sealed class SourceUsageInfo
{
    public int Imports { get; init; }
    public int Operations { get; init; }
}

internal static class SourceDataService
{
    private static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QNB",
        "qnb.db");

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public static SourceUsageInfo GetUsage(Guid accountId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM Imports WHERE AccountId = $accountId),
    (SELECT COUNT(*) FROM Operations o
        INNER JOIN Imports i ON i.Id = o.ImportId
        WHERE i.AccountId = $accountId);";
        command.Parameters.AddWithValue("$accountId", accountId.ToString());
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new SourceUsageInfo();
        return new SourceUsageInfo
        {
            Imports = reader.GetInt32(0),
            Operations = reader.GetInt32(1)
        };
    }

    public static void DeleteSourceAndData(Guid accountId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteOperations = connection.CreateCommand())
        {
            deleteOperations.Transaction = transaction;
            deleteOperations.CommandText = @"
DELETE FROM Operations
WHERE ImportId IN (SELECT Id FROM Imports WHERE AccountId = $accountId);";
            deleteOperations.Parameters.AddWithValue("$accountId", accountId.ToString());
            deleteOperations.ExecuteNonQuery();
        }

        using (var deleteImports = connection.CreateCommand())
        {
            deleteImports.Transaction = transaction;
            deleteImports.CommandText = "DELETE FROM Imports WHERE AccountId = $accountId;";
            deleteImports.Parameters.AddWithValue("$accountId", accountId.ToString());
            deleteImports.ExecuteNonQuery();
        }

        using (var deleteAccount = connection.CreateCommand())
        {
            deleteAccount.Transaction = transaction;
            deleteAccount.CommandText = "DELETE FROM Accounts WHERE Id = $accountId;";
            deleteAccount.Parameters.AddWithValue("$accountId", accountId.ToString());
            deleteAccount.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static BankAccountProfile? FindDuplicateSource(
        string bankName,
        string accountName,
        string accountReference,
        DateTime? sourceDate,
        Guid? ignoreId = null)
    {
        static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant();

        var bank = Normalize(bankName);
        var name = Normalize(accountName);
        var reference = Normalize(accountReference);
        var date = sourceDate?.Date;

        foreach (var account in BankingRepository.LoadConfiguration().Accounts)
        {
            if (ignoreId.HasValue && account.Id == ignoreId.Value) continue;
            if (Normalize(account.BankName) != bank) continue;
            if (account.SourceDate?.Date != date) continue;

            var sameReference = !string.IsNullOrWhiteSpace(reference)
                && Normalize(account.AccountReference) == reference;
            var sameName = string.IsNullOrWhiteSpace(reference)
                && Normalize(account.AccountName) == name;

            if (sameReference || sameName)
                return account;
        }

        return null;
    }
}
