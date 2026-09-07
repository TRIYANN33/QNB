using Microsoft.Data.Sqlite;
using System.Globalization;

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
    private static string DatabasePath => Path.Combine(Root, "qnb.db");
    private static string ConnectionString => $"Data Source={DatabasePath}";

    static BankingRepository()
    {
        InitializeDatabase();
    }

    private static void InitializeDatabase()
    {
        Directory.CreateDirectory(Root);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Accounts (
    Id TEXT PRIMARY KEY,
    BankName TEXT NOT NULL,
    AccountName TEXT NOT NULL,
    AccountReference TEXT NOT NULL,
    Holder TEXT NOT NULL,
    Type INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Imports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SourceFile TEXT NOT NULL,
    AccountId TEXT NULL,
    BankName TEXT NOT NULL,
    AccountDisplayName TEXT NOT NULL,
    AccountReference TEXT NOT NULL,
    AccountHolder TEXT NOT NULL,
    AccountType INTEGER NOT NULL,
    BalanceDate TEXT NULL,
    Balance REAL NULL,
    Currency TEXT NOT NULL,
    ImportedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Operations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ImportId INTEGER NOT NULL,
    OperationDate TEXT NOT NULL,
    Nature TEXT NOT NULL,
    Debit REAL NOT NULL,
    Credit REAL NOT NULL,
    Currency TEXT NOT NULL,
    ValueDate TEXT NULL,
    InterbankLabel TEXT NOT NULL,
    Details TEXT NOT NULL,
    IsDeferredCardSummary INTEGER NOT NULL,
    FOREIGN KEY (ImportId) REFERENCES Imports(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Operations_ImportId ON Operations(ImportId);
CREATE INDEX IF NOT EXISTS IX_Imports_AccountId ON Imports(AccountId);";
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public static BankingConfiguration LoadConfiguration()
    {
        var configuration = new BankingConfiguration();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, BankName, AccountName, AccountReference, Holder, Type FROM Accounts ORDER BY BankName, AccountName";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            configuration.Accounts.Add(new BankAccountProfile
            {
                Id = Guid.Parse(reader.GetString(0)),
                BankName = reader.GetString(1),
                AccountName = reader.GetString(2),
                AccountReference = reader.GetString(3),
                Holder = reader.GetString(4),
                Type = (BankAccountType)reader.GetInt32(5)
            });
        }
        return configuration;
    }

    public static void SaveConfiguration(BankingConfiguration configuration)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Accounts";
            delete.ExecuteNonQuery();
        }

        foreach (var account in configuration.Accounts)
            SaveAccountInternal(connection, transaction, account);

        transaction.Commit();
    }

    public static BankAccountProfile SaveAccount(BankAccountProfile account)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        SaveAccountInternal(connection, transaction, account);
        transaction.Commit();
        return account;
    }

    private static void SaveAccountInternal(SqliteConnection connection, SqliteTransaction transaction, BankAccountProfile account)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO Accounts (Id, BankName, AccountName, AccountReference, Holder, Type)
VALUES ($id, $bank, $name, $reference, $holder, $type)
ON CONFLICT(Id) DO UPDATE SET
    BankName = excluded.BankName,
    AccountName = excluded.AccountName,
    AccountReference = excluded.AccountReference,
    Holder = excluded.Holder,
    Type = excluded.Type;";
        command.Parameters.AddWithValue("$id", account.Id.ToString());
        command.Parameters.AddWithValue("$bank", account.BankName);
        command.Parameters.AddWithValue("$name", account.AccountName);
        command.Parameters.AddWithValue("$reference", account.AccountReference);
        command.Parameters.AddWithValue("$holder", account.Holder);
        command.Parameters.AddWithValue("$type", (int)account.Type);
        command.ExecuteNonQuery();
    }

    public static string SaveImport(BankImportResult result)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var insertImport = connection.CreateCommand();
        insertImport.Transaction = transaction;
        insertImport.CommandText = @"
INSERT INTO Imports (SourceFile, AccountId, BankName, AccountDisplayName, AccountReference, AccountHolder, AccountType, BalanceDate, Balance, Currency, ImportedAt)
VALUES ($source, $accountId, $bank, $display, $reference, $holder, $type, $balanceDate, $balance, $currency, $importedAt);
SELECT last_insert_rowid();";
        insertImport.Parameters.AddWithValue("$source", result.SourceFile);
        insertImport.Parameters.AddWithValue("$accountId", (object?)result.AccountId?.ToString() ?? DBNull.Value);
        insertImport.Parameters.AddWithValue("$bank", result.BankName);
        insertImport.Parameters.AddWithValue("$display", result.AccountDisplayName);
        insertImport.Parameters.AddWithValue("$reference", result.AccountReference);
        insertImport.Parameters.AddWithValue("$holder", result.AccountHolder);
        insertImport.Parameters.AddWithValue("$type", (int)result.AccountType);
        insertImport.Parameters.AddWithValue("$balanceDate", result.BalanceDate?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        insertImport.Parameters.AddWithValue("$balance", result.Balance.HasValue ? result.Balance.Value : (object)DBNull.Value);
        insertImport.Parameters.AddWithValue("$currency", result.Currency);
        insertImport.Parameters.AddWithValue("$importedAt", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));

        var importId = (long)(insertImport.ExecuteScalar() ?? 0L);

        foreach (var operation in result.Operations)
        {
            using var insertOperation = connection.CreateCommand();
            insertOperation.Transaction = transaction;
            insertOperation.CommandText = @"
INSERT INTO Operations (ImportId, OperationDate, Nature, Debit, Credit, Currency, ValueDate, InterbankLabel, Details, IsDeferredCardSummary)
VALUES ($importId, $date, $nature, $debit, $credit, $currency, $valueDate, $label, $details, $deferred);";
            insertOperation.Parameters.AddWithValue("$importId", importId);
            insertOperation.Parameters.AddWithValue("$date", operation.Date.ToString("O", CultureInfo.InvariantCulture));
            insertOperation.Parameters.AddWithValue("$nature", operation.Nature);
            insertOperation.Parameters.AddWithValue("$debit", operation.Debit);
            insertOperation.Parameters.AddWithValue("$credit", operation.Credit);
            insertOperation.Parameters.AddWithValue("$currency", operation.Currency);
            insertOperation.Parameters.AddWithValue("$valueDate", operation.ValueDate?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
            insertOperation.Parameters.AddWithValue("$label", operation.InterbankLabel);
            insertOperation.Parameters.AddWithValue("$details", operation.Details);
            insertOperation.Parameters.AddWithValue("$deferred", operation.IsDeferredCardSummary ? 1 : 0);
            insertOperation.ExecuteNonQuery();
        }

        transaction.Commit();
        return $"sqlite://qnb.db/import/{importId}";
    }

    public static List<BankImportResult> LoadImports()
    {
        var results = new List<BankImportResult>();
        using var connection = OpenConnection();
        using var importsCommand = connection.CreateCommand();
        importsCommand.CommandText = @"
SELECT Id, SourceFile, AccountId, BankName, AccountDisplayName, AccountReference, AccountHolder, AccountType, BalanceDate, Balance, Currency
FROM Imports ORDER BY Id;";

        using var reader = importsCommand.ExecuteReader();
        while (reader.Read())
        {
            var importId = reader.GetInt64(0);
            var result = new BankImportResult
            {
                SourceFile = reader.GetString(1),
                AccountId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                BankName = reader.GetString(3),
                AccountDisplayName = reader.GetString(4),
                AccountReference = reader.GetString(5),
                AccountHolder = reader.GetString(6),
                AccountType = (BankAccountType)reader.GetInt32(7),
                BalanceDate = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Balance = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetDouble(9), CultureInfo.InvariantCulture),
                Currency = reader.GetString(10)
            };

            using var operationsCommand = connection.CreateCommand();
            operationsCommand.CommandText = @"
SELECT OperationDate, Nature, Debit, Credit, Currency, ValueDate, InterbankLabel, Details, IsDeferredCardSummary
FROM Operations WHERE ImportId = $importId ORDER BY Id;";
            operationsCommand.Parameters.AddWithValue("$importId", importId);
            using var operationsReader = operationsCommand.ExecuteReader();
            while (operationsReader.Read())
            {
                result.Operations.Add(new BankOperation
                {
                    Date = DateTime.Parse(operationsReader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    Nature = operationsReader.GetString(1),
                    Debit = Convert.ToDecimal(operationsReader.GetDouble(2), CultureInfo.InvariantCulture),
                    Credit = Convert.ToDecimal(operationsReader.GetDouble(3), CultureInfo.InvariantCulture),
                    Currency = operationsReader.GetString(4),
                    ValueDate = operationsReader.IsDBNull(5) ? null : DateTime.Parse(operationsReader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    InterbankLabel = operationsReader.GetString(6),
                    Details = operationsReader.GetString(7),
                    IsDeferredCardSummary = operationsReader.GetInt32(8) == 1
                });
            }

            results.Add(result);
        }

        return results;
    }

    public static DashboardBankingStats GetDashboardStats()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM Imports),
    (SELECT COUNT(*) FROM Operations),
    (SELECT COUNT(*) FROM Accounts),
    COALESCE((SELECT SUM(ABS(Credit + Debit)) FROM Operations WHERE IsDeferredCardSummary = 1), 0);";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new DashboardBankingStats();

        return new DashboardBankingStats
        {
            Documents = reader.GetInt32(0),
            Operations = reader.GetInt32(1),
            Accounts = reader.GetInt32(2),
            DeferredCardAmount = Convert.ToDecimal(reader.GetDouble(3), CultureInfo.InvariantCulture)
        };
    }
}
