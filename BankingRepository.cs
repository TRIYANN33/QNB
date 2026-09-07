using Microsoft.Data.Sqlite;
using System.Globalization;

namespace QNB;

internal enum BankAccountType { Courant, Epargne, CarteDifferee, Autre }
internal sealed class BankAccountProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountReference { get; set; } = string.Empty;
    public string Holder { get; set; } = string.Empty;
    public BankAccountType Type { get; set; } = BankAccountType.Courant;
    public DateTime? SourceDate { get; set; }
    public string DisplayName => SourceDate.HasValue
        ? $"{BankName} — {AccountName} — {SourceDate.Value:dd/MM/yyyy}"
        : $"{BankName} — {AccountName}";
}
internal sealed class BankingConfiguration { public List<BankAccountProfile> Accounts { get; set; } = new(); }
internal sealed class DashboardBankingStats { public int Documents { get; set; } public int Operations { get; set; } public int Accounts { get; set; } public decimal DeferredCardAmount { get; set; } }

internal static class BankingRepository
{
    private const string ResetVersion = "2026-09-07-clean-3";
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB");
    private static string DatabasePath => Path.Combine(Root, "qnb.db");
    private static string ConnectionString => $"Data Source={DatabasePath}";
    static BankingRepository() => InitializeDatabase();

    private static void InitializeDatabase()
    {
        Directory.CreateDirectory(Root);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS Accounts (Id TEXT PRIMARY KEY,BankName TEXT NOT NULL,AccountName TEXT NOT NULL,AccountReference TEXT NOT NULL,Holder TEXT NOT NULL,Type INTEGER NOT NULL,SourceDate TEXT NULL);
CREATE TABLE IF NOT EXISTS Imports (Id INTEGER PRIMARY KEY AUTOINCREMENT,SourceFile TEXT NOT NULL,AccountId TEXT NULL,BankName TEXT NOT NULL,AccountDisplayName TEXT NOT NULL,AccountReference TEXT NOT NULL,AccountHolder TEXT NOT NULL,AccountType INTEGER NOT NULL,BalanceDate TEXT NULL,Balance REAL NULL,Currency TEXT NOT NULL,ImportedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Operations (Id INTEGER PRIMARY KEY AUTOINCREMENT,ImportId INTEGER NOT NULL,OperationDate TEXT NOT NULL,Nature TEXT NOT NULL,Debit REAL NOT NULL,Credit REAL NOT NULL,Currency TEXT NOT NULL,ValueDate TEXT NULL,InterbankLabel TEXT NOT NULL,Details TEXT NOT NULL,IsDeferredCardSummary INTEGER NOT NULL,FOREIGN KEY (ImportId) REFERENCES Imports(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS Meta (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Operations_ImportId ON Operations(ImportId); CREATE INDEX IF NOT EXISTS IX_Imports_AccountId ON Imports(AccountId);";
        command.ExecuteNonQuery();
        EnsureSourceDateColumn(connection);
        ApplyOneTimeReset(connection);
    }

    private static void EnsureSourceDateColumn(SqliteConnection connection)
    {
        using var info = connection.CreateCommand();
        info.CommandText = "PRAGMA table_info(Accounts);";
        using var reader = info.ExecuteReader();
        var exists = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "SourceDate", StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
        reader.Close();
        if (exists) return;
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE Accounts ADD COLUMN SourceDate TEXT NULL;";
        alter.ExecuteNonQuery();
    }

    private static void ApplyOneTimeReset(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT Value FROM Meta WHERE Key='ResetVersion' LIMIT 1";
        var current = check.ExecuteScalar()?.ToString();
        if (string.Equals(current, ResetVersion, StringComparison.Ordinal)) return;
        using var transaction = connection.BeginTransaction();
        using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = @"DELETE FROM Operations; DELETE FROM Imports; DELETE FROM Accounts; DELETE FROM sqlite_sequence WHERE name IN ('Operations','Imports');"; clear.ExecuteNonQuery(); }
        using (var marker = connection.CreateCommand()) { marker.Transaction = transaction; marker.CommandText = "INSERT INTO Meta(Key,Value) VALUES('ResetVersion',$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value"; marker.Parameters.AddWithValue("$value", ResetVersion); marker.ExecuteNonQuery(); }
        transaction.Commit();
    }

    private static SqliteConnection OpenConnection() { var c = new SqliteConnection(ConnectionString); c.Open(); using var p = c.CreateCommand(); p.CommandText = "PRAGMA foreign_keys = ON;"; p.ExecuteNonQuery(); return c; }
    public static BankingConfiguration LoadConfiguration()
    {
        var cfg = new BankingConfiguration();
        using var c = OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id,BankName,AccountName,AccountReference,Holder,Type,SourceDate FROM Accounts ORDER BY BankName,AccountName,SourceDate";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            cfg.Accounts.Add(new BankAccountProfile
            {
                Id = Guid.Parse(r.GetString(0)), BankName = r.GetString(1), AccountName = r.GetString(2), AccountReference = r.GetString(3), Holder = r.GetString(4), Type = (BankAccountType)r.GetInt32(5),
                SourceDate = r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        return cfg;
    }
    public static void SaveConfiguration(BankingConfiguration configuration){using var c=OpenConnection();using var t=c.BeginTransaction();using(var d=c.CreateCommand()){d.Transaction=t;d.CommandText="DELETE FROM Accounts";d.ExecuteNonQuery();}foreach(var a in configuration.Accounts)SaveAccountInternal(c,t,a);t.Commit();}
    public static BankAccountProfile SaveAccount(BankAccountProfile account){using var c=OpenConnection();using var t=c.BeginTransaction();SaveAccountInternal(c,t,account);t.Commit();return account;}
    private static void SaveAccountInternal(SqliteConnection c,SqliteTransaction t,BankAccountProfile a)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText=@"INSERT INTO Accounts (Id,BankName,AccountName,AccountReference,Holder,Type,SourceDate) VALUES ($id,$bank,$name,$reference,$holder,$type,$sourceDate) ON CONFLICT(Id) DO UPDATE SET BankName=excluded.BankName,AccountName=excluded.AccountName,AccountReference=excluded.AccountReference,Holder=excluded.Holder,Type=excluded.Type,SourceDate=excluded.SourceDate;";
        cmd.Parameters.AddWithValue("$id",a.Id.ToString());cmd.Parameters.AddWithValue("$bank",a.BankName);cmd.Parameters.AddWithValue("$name",a.AccountName);cmd.Parameters.AddWithValue("$reference",a.AccountReference);cmd.Parameters.AddWithValue("$holder",a.Holder);cmd.Parameters.AddWithValue("$type",(int)a.Type);cmd.Parameters.AddWithValue("$sourceDate",a.SourceDate.HasValue?a.SourceDate.Value.Date.ToString("O",CultureInfo.InvariantCulture):DBNull.Value);cmd.ExecuteNonQuery();
    }
    public static string SaveImport(BankImportResult result){using var c=OpenConnection();using var t=c.BeginTransaction();using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText=@"INSERT INTO Imports (SourceFile,AccountId,BankName,AccountDisplayName,AccountReference,AccountHolder,AccountType,BalanceDate,Balance,Currency,ImportedAt) VALUES ($source,$accountId,$bank,$display,$reference,$holder,$type,$balanceDate,$balance,$currency,$importedAt); SELECT last_insert_rowid();";cmd.Parameters.AddWithValue("$source",result.SourceFile);cmd.Parameters.AddWithValue("$accountId",(object?)result.AccountId?.ToString()??DBNull.Value);cmd.Parameters.AddWithValue("$bank",result.BankName);cmd.Parameters.AddWithValue("$display",result.AccountDisplayName);cmd.Parameters.AddWithValue("$reference",result.AccountReference);cmd.Parameters.AddWithValue("$holder",result.AccountHolder);cmd.Parameters.AddWithValue("$type",(int)result.AccountType);cmd.Parameters.AddWithValue("$balanceDate",result.BalanceDate.HasValue?result.BalanceDate.Value.ToString("O",CultureInfo.InvariantCulture):DBNull.Value);cmd.Parameters.AddWithValue("$balance",result.Balance.HasValue?result.Balance.Value:DBNull.Value);cmd.Parameters.AddWithValue("$currency",result.Currency);cmd.Parameters.AddWithValue("$importedAt",DateTime.Now.ToString("O",CultureInfo.InvariantCulture));var importId=Convert.ToInt64(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);foreach(var o in result.Operations){using var op=c.CreateCommand();op.Transaction=t;op.CommandText=@"INSERT INTO Operations (ImportId,OperationDate,Nature,Debit,Credit,Currency,ValueDate,InterbankLabel,Details,IsDeferredCardSummary) VALUES ($importId,$date,$nature,$debit,$credit,$currency,$valueDate,$label,$details,$deferred);";op.Parameters.AddWithValue("$importId",importId);op.Parameters.AddWithValue("$date",o.Date.ToString("O",CultureInfo.InvariantCulture));op.Parameters.AddWithValue("$nature",o.Nature);op.Parameters.AddWithValue("$debit",o.Debit);op.Parameters.AddWithValue("$credit",o.Credit);op.Parameters.AddWithValue("$currency",o.Currency);op.Parameters.AddWithValue("$valueDate",o.ValueDate.HasValue?o.ValueDate.Value.ToString("O",CultureInfo.InvariantCulture):DBNull.Value);op.Parameters.AddWithValue("$label",o.InterbankLabel);op.Parameters.AddWithValue("$details",o.Details);op.Parameters.AddWithValue("$deferred",o.IsDeferredCardSummary?1:0);op.ExecuteNonQuery();}t.Commit();return $"sqlite://qnb.db/import/{importId}";}
    public static List<BankImportResult> LoadImports(){var results=new List<(long Id,BankImportResult Result)>();using var c=OpenConnection();using(var cmd=c.CreateCommand()){cmd.CommandText="SELECT Id,SourceFile,AccountId,BankName,AccountDisplayName,AccountReference,AccountHolder,AccountType,BalanceDate,Balance,Currency FROM Imports ORDER BY Id";using var r=cmd.ExecuteReader();while(r.Read()){var x=new BankImportResult{SourceFile=r.GetString(1),AccountId=r.IsDBNull(2)?null:Guid.Parse(r.GetString(2)),BankName=r.GetString(3),AccountDisplayName=r.GetString(4),AccountReference=r.GetString(5),AccountHolder=r.GetString(6),AccountType=(BankAccountType)r.GetInt32(7),BalanceDate=r.IsDBNull(8)?null:DateTime.Parse(r.GetString(8),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Balance=r.IsDBNull(9)?null:Convert.ToDecimal(r.GetDouble(9),CultureInfo.InvariantCulture),Currency=r.GetString(10)};results.Add((r.GetInt64(0),x));}}foreach(var item in results){using var cmd=c.CreateCommand();cmd.CommandText="SELECT OperationDate,Nature,Debit,Credit,Currency,ValueDate,InterbankLabel,Details,IsDeferredCardSummary FROM Operations WHERE ImportId=$id ORDER BY Id";cmd.Parameters.AddWithValue("$id",item.Id);using var r=cmd.ExecuteReader();while(r.Read())item.Result.Operations.Add(new BankOperation{Date=DateTime.Parse(r.GetString(0),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Nature=r.GetString(1),Debit=Convert.ToDecimal(r.GetDouble(2),CultureInfo.InvariantCulture),Credit=Convert.ToDecimal(r.GetDouble(3),CultureInfo.InvariantCulture),Currency=r.GetString(4),ValueDate=r.IsDBNull(5)?null:DateTime.Parse(r.GetString(5),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),InterbankLabel=r.GetString(6),Details=r.GetString(7),IsDeferredCardSummary=r.GetInt32(8)==1});}return results.Select(x=>x.Result).ToList();}
    public static List<DuplicateOperation> LoadOperationsForDuplicateAnalysis(){var list=new List<DuplicateOperation>();using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT o.Id,i.BankName,i.AccountDisplayName,o.OperationDate,o.ValueDate,o.Debit,o.Credit,o.Currency,o.Nature,o.InterbankLabel,o.Details FROM Operations o JOIN Imports i ON i.Id=o.ImportId ORDER BY o.OperationDate,o.Id";using var r=cmd.ExecuteReader();while(r.Read())list.Add(new DuplicateOperation{Id=r.GetInt64(0),Bank=r.GetString(1),Account=r.GetString(2),Date=DateTime.Parse(r.GetString(3),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),ValueDate=r.IsDBNull(4)?null:DateTime.Parse(r.GetString(4),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Amount=Convert.ToDecimal(r.GetDouble(6),CultureInfo.InvariantCulture)+Convert.ToDecimal(r.GetDouble(5),CultureInfo.InvariantCulture),Currency=r.GetString(7),Nature=r.GetString(8),Label=r.GetString(9),Details=r.GetString(10)});return list;}
    public static DashboardBankingStats GetDashboardStats(){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT (SELECT COUNT(*) FROM Imports),(SELECT COUNT(*) FROM Operations),(SELECT COUNT(*) FROM Accounts),COALESCE((SELECT SUM(ABS(Credit+Debit)) FROM Operations WHERE IsDeferredCardSummary=1),0);";using var r=cmd.ExecuteReader();if(!r.Read())return new DashboardBankingStats();return new DashboardBankingStats{Documents=r.GetInt32(0),Operations=r.GetInt32(1),Accounts=r.GetInt32(2),DeferredCardAmount=Convert.ToDecimal(r.GetDouble(3),CultureInfo.InvariantCulture)};}
}
