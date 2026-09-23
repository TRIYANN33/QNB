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
internal sealed class OperationClassification { public long OperationId { get; set; } public string Type { get; set; } = string.Empty; public string SubType { get; set; } = string.Empty; public string Mode { get; set; } = string.Empty; }
internal sealed class ClassificationRule { public long Id { get; set; } public string ContainsText { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; public string SubType { get; set; } = string.Empty; public int Priority { get; set; } = 100; public string CellColor { get; set; } = ""; public bool Enabled { get; set; } = true; }
internal sealed class DashboardBankingStats { public int Documents { get; set; } public int Operations { get; set; } public int Accounts { get; set; } public decimal DeferredCardAmount { get; set; } }

internal static class BankingRepository
{
    private const string ResetVersion = "2026-09-07-clean-3";
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QNB");
    private static string DatabasePath => Path.Combine(Root, "qnb.db");
    private static string ConnectionString => $"Data Source={DatabasePath}";
    static BankingRepository() => InitializeDatabase();

    public static string DatabaseFilePath => DatabasePath;

    public static void BackupDatabase(string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? Root);
        using var source=OpenConnection();
        using var destination=new SqliteConnection($"Data Source={destinationPath}");
        destination.Open();
        source.BackupDatabase(destination);
    }

    public static void RestoreDatabase(string sourcePath)
    {
        if(!File.Exists(sourcePath))throw new FileNotFoundException("Fichier de sauvegarde introuvable.",sourcePath);
        using var source=new SqliteConnection($"Data Source={sourcePath}");
        source.Open();
        using var check=source.CreateCommand();check.CommandText="PRAGMA integrity_check;";
        if(!string.Equals(Convert.ToString(check.ExecuteScalar(),CultureInfo.InvariantCulture),"ok",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La sauvegarde SQLite est endommagée.");
        using var destination=OpenConnection();
        source.BackupDatabase(destination);
    }

    private static void InitializeDatabase()
    {
        Directory.CreateDirectory(Root);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS Accounts (Id TEXT PRIMARY KEY,BankName TEXT NOT NULL,AccountName TEXT NOT NULL,AccountReference TEXT NOT NULL,Holder TEXT NOT NULL,Type INTEGER NOT NULL,SourceDate TEXT NULL);
CREATE TABLE IF NOT EXISTS Imports (Id INTEGER PRIMARY KEY AUTOINCREMENT,SourceFile TEXT NOT NULL,AccountId TEXT NULL,BankName TEXT NOT NULL,AccountDisplayName TEXT NOT NULL,AccountReference TEXT NOT NULL,AccountHolder TEXT NOT NULL,AccountType INTEGER NOT NULL,BalanceDate TEXT NULL,Balance REAL NULL,Currency TEXT NOT NULL,ImportedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Operations (Id INTEGER PRIMARY KEY AUTOINCREMENT,ImportId INTEGER NOT NULL,OperationDate TEXT NOT NULL,Nature TEXT NOT NULL,Debit REAL NOT NULL,Credit REAL NOT NULL,Currency TEXT NOT NULL,ValueDate TEXT NULL,InterbankLabel TEXT NOT NULL,Details TEXT NOT NULL,IsDeferredCardSummary INTEGER NOT NULL,FOREIGN KEY (ImportId) REFERENCES Imports(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS Meta (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS DuplicateExclusions (OperationId1 INTEGER NOT NULL,OperationId2 INTEGER NOT NULL,ExcludedAt TEXT NOT NULL,PRIMARY KEY(OperationId1,OperationId2));
CREATE TABLE IF NOT EXISTS ClassificationRules (Id INTEGER PRIMARY KEY AUTOINCREMENT,ContainsText TEXT NOT NULL,Type TEXT NOT NULL,SubType TEXT NOT NULL,Priority INTEGER NOT NULL DEFAULT 100,Enabled INTEGER NOT NULL DEFAULT 1);
CREATE INDEX IF NOT EXISTS IX_Operations_ImportId ON Operations(ImportId); CREATE INDEX IF NOT EXISTS IX_Imports_AccountId ON Imports(AccountId);";
        command.ExecuteNonQuery();
        EnsureSourceDateColumn(connection);
        EnsureOperationClassificationColumns(connection);
        EnsureClassificationRuleColorColumn(connection);
        EnsureDefaultClassificationRules(connection);
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

    private static void EnsureOperationClassificationColumns(SqliteConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(Operations);";
            using var reader = info.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }
        foreach (var definition in new[] { "OperationType TEXT NOT NULL DEFAULT ''", "OperationSubType TEXT NOT NULL DEFAULT ''", "ClassificationMode TEXT NOT NULL DEFAULT ''" })
        {
            var name = definition.Split(' ')[0];
            if (columns.Contains(name)) continue;
            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE Operations ADD COLUMN {definition};";
            alter.ExecuteNonQuery();
        }
    }

    private static void EnsureClassificationRuleColorColumn(SqliteConnection connection)
    {
        using var info=connection.CreateCommand();info.CommandText="PRAGMA table_info(ClassificationRules);";using var r=info.ExecuteReader();var exists=false;while(r.Read())if(string.Equals(r.GetString(1),"CellColor",StringComparison.OrdinalIgnoreCase)){exists=true;break;}r.Close();
        if(!exists){using var alter=connection.CreateCommand();alter.CommandText="ALTER TABLE ClassificationRules ADD COLUMN CellColor TEXT NOT NULL DEFAULT '';";alter.ExecuteNonQuery();}
    }

    private static void EnsureDefaultClassificationRules(SqliteConnection connection)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM ClassificationRules";
        if (Convert.ToInt64(count.ExecuteScalar(), CultureInfo.InvariantCulture) > 0) return;
        var defaults = new (string Text,string Type,string SubType,int Priority)[]
        {
            ("AUCHAN","Alimentaire","Supermarché",10),
            ("BOULANGERIE","Alimentaire","Boulangerie",20),
            ("PRIMEUR ORANGE","Alimentaire","Primeur",30),
            ("BOUCHERIE JO","Alimentaire","Boucherie",40),
            ("ME PAUL","Réglement Pro","Cabinet Medicale",50),
            ("URSSAF","Charge Pro","URSSAF",60),
            ("IMPÔT","Charge Pers","IMPOT",70),
            ("IMPOT","Charge Pers","IMPOT",71),
            ("ASURANCE ALIANZ JO","Assurance","Assurance",80),
            ("ASSURANCE ALLIANZ JO","Assurance","Assurance",81),
            ("CIPAV","RETRAITE","CIPAV",90)
        };
        foreach (var rule in defaults)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO ClassificationRules(ContainsText,Type,SubType,Priority,Enabled) VALUES($text,$type,$sub,$priority,1)";
            cmd.Parameters.AddWithValue("$text",rule.Text); cmd.Parameters.AddWithValue("$type",rule.Type); cmd.Parameters.AddWithValue("$sub",rule.SubType); cmd.Parameters.AddWithValue("$priority",rule.Priority); cmd.ExecuteNonQuery();
        }
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
    public static List<BankImportResult> LoadImports(){var results=new List<(long Id,BankImportResult Result)>();using var c=OpenConnection();using(var cmd=c.CreateCommand()){cmd.CommandText="SELECT Id,SourceFile,AccountId,BankName,AccountDisplayName,AccountReference,AccountHolder,AccountType,BalanceDate,Balance,Currency FROM Imports ORDER BY Id";using var r=cmd.ExecuteReader();while(r.Read()){var x=new BankImportResult{SourceFile=r.GetString(1),AccountId=r.IsDBNull(2)?null:Guid.Parse(r.GetString(2)),BankName=r.GetString(3),AccountDisplayName=r.GetString(4),AccountReference=r.GetString(5),AccountHolder=r.GetString(6),AccountType=(BankAccountType)r.GetInt32(7),BalanceDate=r.IsDBNull(8)?null:DateTime.Parse(r.GetString(8),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Balance=r.IsDBNull(9)?null:Convert.ToDecimal(r.GetDouble(9),CultureInfo.InvariantCulture),Currency=r.GetString(10)};results.Add((r.GetInt64(0),x));}}foreach(var item in results){using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,OperationDate,Nature,Debit,Credit,Currency,ValueDate,InterbankLabel,Details,IsDeferredCardSummary FROM Operations WHERE ImportId=$id ORDER BY Id";cmd.Parameters.AddWithValue("$id",item.Id);using var r=cmd.ExecuteReader();while(r.Read())item.Result.Operations.Add(new BankOperation{Id=r.GetInt64(0),Date=DateTime.Parse(r.GetString(1),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Nature=r.GetString(2),Debit=Convert.ToDecimal(r.GetDouble(3),CultureInfo.InvariantCulture),Credit=Convert.ToDecimal(r.GetDouble(4),CultureInfo.InvariantCulture),Currency=r.GetString(5),ValueDate=r.IsDBNull(6)?null:DateTime.Parse(r.GetString(6),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),InterbankLabel=r.GetString(7),Details=r.GetString(8),IsDeferredCardSummary=r.GetInt32(9)==1});}return results.Select(x=>x.Result).ToList();}
    public static void AddManualOperation(BankImportResult target, BankOperation operation)
    {
        using var c=OpenConnection();
        using var find=c.CreateCommand();
        find.CommandText=@"SELECT Id FROM Imports WHERE BankName=$bank AND AccountReference=$reference AND AccountDisplayName=$display ORDER BY Id DESC LIMIT 1";
        find.Parameters.AddWithValue("$bank",target.BankName);find.Parameters.AddWithValue("$reference",target.AccountReference);find.Parameters.AddWithValue("$display",target.AccountDisplayName);
        var value=find.ExecuteScalar();if(value is null)throw new InvalidOperationException("Compte bancaire introuvable.");
        var importId=Convert.ToInt64(value,CultureInfo.InvariantCulture);
        using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO Operations (ImportId,OperationDate,Nature,Debit,Credit,Currency,ValueDate,InterbankLabel,Details,IsDeferredCardSummary) VALUES ($importId,$date,$nature,$debit,$credit,$currency,$valueDate,$label,$details,$deferred)";
        cmd.Parameters.AddWithValue("$importId",importId);cmd.Parameters.AddWithValue("$date",operation.Date.ToString("O",CultureInfo.InvariantCulture));cmd.Parameters.AddWithValue("$nature",operation.Nature??string.Empty);cmd.Parameters.AddWithValue("$debit",operation.Debit);cmd.Parameters.AddWithValue("$credit",operation.Credit);cmd.Parameters.AddWithValue("$currency",operation.Currency??"EUR");cmd.Parameters.AddWithValue("$valueDate",operation.ValueDate.HasValue?operation.ValueDate.Value.ToString("O",CultureInfo.InvariantCulture):DBNull.Value);cmd.Parameters.AddWithValue("$label",operation.InterbankLabel??string.Empty);cmd.Parameters.AddWithValue("$details",operation.Details??string.Empty);cmd.Parameters.AddWithValue("$deferred",operation.IsDeferredCardSummary?1:0);cmd.ExecuteNonQuery();
    }

    public static void DeleteOperations(IEnumerable<long> operationIds)
    {
        var ids = operationIds.Distinct().ToList();
        if (ids.Count == 0) return;
        using var c = OpenConnection();
        using var t = c.BeginTransaction();
        foreach (var id in ids)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = t;
            cmd.CommandText = "DELETE FROM Operations WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        // Supprimer les imports devenus vides, sans supprimer la source/compte.
        using (var cleanup = c.CreateCommand())
        {
            cleanup.Transaction = t;
            cleanup.CommandText = "DELETE FROM Imports WHERE NOT EXISTS (SELECT 1 FROM Operations o WHERE o.ImportId=Imports.Id)";
            cleanup.ExecuteNonQuery();
        }
        t.Commit();
    }

    public static HashSet<string> LoadDuplicateExclusions(){var set=new HashSet<string>();using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT OperationId1,OperationId2 FROM DuplicateExclusions";using var r=cmd.ExecuteReader();while(r.Read())set.Add($"{r.GetInt64(0)}:{r.GetInt64(1)}");return set;}
    public static void ExcludeDuplicatePair(long a,long b){var x=Math.Min(a,b);var y=Math.Max(a,b);using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="INSERT OR IGNORE INTO DuplicateExclusions(OperationId1,OperationId2,ExcludedAt) VALUES($a,$b,$at)";cmd.Parameters.AddWithValue("$a",x);cmd.Parameters.AddWithValue("$b",y);cmd.Parameters.AddWithValue("$at",DateTime.Now.ToString("O",CultureInfo.InvariantCulture));cmd.ExecuteNonQuery();}
    public static void ClearDuplicateExclusions(){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM DuplicateExclusions";cmd.ExecuteNonQuery();}
    public static List<DuplicateOperation> LoadOperationsForDuplicateAnalysis(){var list=new List<DuplicateOperation>();using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT o.Id,i.BankName,i.AccountDisplayName,o.OperationDate,o.ValueDate,o.Debit,o.Credit,o.Currency,o.Nature,o.InterbankLabel,o.Details FROM Operations o JOIN Imports i ON i.Id=o.ImportId ORDER BY o.OperationDate,o.Id";using var r=cmd.ExecuteReader();while(r.Read())list.Add(new DuplicateOperation{Id=r.GetInt64(0),Bank=r.GetString(1),Account=r.GetString(2),Date=DateTime.Parse(r.GetString(3),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),ValueDate=r.IsDBNull(4)?null:DateTime.Parse(r.GetString(4),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Amount=Convert.ToDecimal(r.GetDouble(6),CultureInfo.InvariantCulture)+Convert.ToDecimal(r.GetDouble(5),CultureInfo.InvariantCulture),Currency=r.GetString(7),Nature=r.GetString(8),Label=r.GetString(9),Details=r.GetString(10)});return list;}
    public static Dictionary<long,OperationClassification> LoadOperationClassifications()
    {
        var result=new Dictionary<long,OperationClassification>(); using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT Id,OperationType,OperationSubType,ClassificationMode FROM Operations";
        using var r=cmd.ExecuteReader(); while(r.Read()) result[r.GetInt64(0)]=new OperationClassification{OperationId=r.GetInt64(0),Type=r.GetString(1),SubType=r.GetString(2),Mode=r.GetString(3)}; return result;
    }
    public static void SetOperationClassification(long operationId,string type,string subType,string mode)
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand(); cmd.CommandText="UPDATE Operations SET OperationType=$type,OperationSubType=$sub,ClassificationMode=$mode WHERE Id=$id";
        cmd.Parameters.AddWithValue("$type",type.Trim());cmd.Parameters.AddWithValue("$sub",subType.Trim());cmd.Parameters.AddWithValue("$mode",mode.Trim());cmd.Parameters.AddWithValue("$id",operationId);cmd.ExecuteNonQuery();
    }
    public static List<ClassificationRule> LoadClassificationRules()
    {
        var list=new List<ClassificationRule>();using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,ContainsText,Type,SubType,Priority,Enabled,CellColor FROM ClassificationRules ORDER BY Priority,Id";
        using var r=cmd.ExecuteReader();while(r.Read())list.Add(new ClassificationRule{Id=r.GetInt64(0),ContainsText=r.GetString(1),Type=r.GetString(2),SubType=r.GetString(3),Priority=r.GetInt32(4),Enabled=r.GetInt32(5)==1,CellColor=r.IsDBNull(6)?"":r.GetString(6)});return list;
    }
    public static void SaveClassificationRule(ClassificationRule rule)
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();
        if(rule.Id==0){cmd.CommandText="INSERT INTO ClassificationRules(ContainsText,Type,SubType,Priority,Enabled,CellColor) VALUES($text,$type,$sub,$priority,$enabled,$color)";}else{cmd.CommandText="UPDATE ClassificationRules SET ContainsText=$text,Type=$type,SubType=$sub,Priority=$priority,Enabled=$enabled,CellColor=$color WHERE Id=$id";cmd.Parameters.AddWithValue("$id",rule.Id);}
        cmd.Parameters.AddWithValue("$text",rule.ContainsText.Trim());cmd.Parameters.AddWithValue("$type",rule.Type.Trim());cmd.Parameters.AddWithValue("$sub",rule.SubType.Trim());cmd.Parameters.AddWithValue("$priority",rule.Priority);cmd.Parameters.AddWithValue("$enabled",rule.Enabled?1:0);cmd.Parameters.AddWithValue("$color",rule.CellColor??"");cmd.ExecuteNonQuery();
    }
    public static int DeleteClassificationRule(long id)
    {
        using var c=OpenConnection();using var t=c.BeginTransaction();
        string type="",subType="";
        using(var read=c.CreateCommand()){read.Transaction=t;read.CommandText="SELECT Type,SubType FROM ClassificationRules WHERE Id=$id";read.Parameters.AddWithValue("$id",id);using var r=read.ExecuteReader();if(r.Read()){type=r.GetString(0);subType=r.GetString(1);}}
        var cleared=0;
        if(!string.IsNullOrWhiteSpace(type))
        {
            using var clear=c.CreateCommand();clear.Transaction=t;
            clear.CommandText="UPDATE Operations SET OperationType='',OperationSubType='',ClassificationMode='' WHERE OperationType=$type AND OperationSubType=$sub AND ClassificationMode IN ('Auto','Règle')";
            clear.Parameters.AddWithValue("$type",type);clear.Parameters.AddWithValue("$sub",subType);cleared=clear.ExecuteNonQuery();
        }
        using(var cmd=c.CreateCommand()){cmd.Transaction=t;cmd.CommandText="DELETE FROM ClassificationRules WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();}
        t.Commit();return cleared;
    }
    public static int ApplyAutomaticClassification(bool overwriteManual=false)
    {
        var rules=LoadClassificationRules().Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.ContainsText)).OrderBy(x=>x.Priority).ThenBy(x=>x.Id).ToList();
        using var c=OpenConnection();var changed=0;using var read=c.CreateCommand();read.CommandText="SELECT Id,Nature,InterbankLabel,Details,ClassificationMode FROM Operations";using var r=read.ExecuteReader();var updates=new List<(long Id,string Type,string Sub)>();
        while(r.Read())
        {
            var mode=r.GetString(4);if(!overwriteManual&&string.Equals(mode,"Manuel",StringComparison.OrdinalIgnoreCase))continue;
            var text=$"{r.GetString(1)} {r.GetString(2)} {r.GetString(3)}";
            var rule=rules.FirstOrDefault(x=>text.Contains(x.ContainsText,StringComparison.CurrentCultureIgnoreCase));if(rule is not null)updates.Add((r.GetInt64(0),rule.Type,rule.SubType));
        }
        r.Close();foreach(var u in updates){using var cmd=c.CreateCommand();cmd.CommandText="UPDATE Operations SET OperationType=$type,OperationSubType=$sub,ClassificationMode='Auto' WHERE Id=$id";cmd.Parameters.AddWithValue("$type",u.Type);cmd.Parameters.AddWithValue("$sub",u.Sub);cmd.Parameters.AddWithValue("$id",u.Id);changed+=cmd.ExecuteNonQuery();}return changed;
    }

    public static DashboardBankingStats GetDashboardStats(){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT (SELECT COUNT(*) FROM Imports),(SELECT COUNT(*) FROM Operations),(SELECT COUNT(*) FROM Accounts),COALESCE((SELECT SUM(ABS(Credit+Debit)) FROM Operations WHERE IsDeferredCardSummary=1),0);";using var r=cmd.ExecuteReader();if(!r.Read())return new DashboardBankingStats();return new DashboardBankingStats{Documents=r.GetInt32(0),Operations=r.GetInt32(1),Accounts=r.GetInt32(2),DeferredCardAmount=Convert.ToDecimal(r.GetDouble(3),CultureInfo.InvariantCulture)};}
}
