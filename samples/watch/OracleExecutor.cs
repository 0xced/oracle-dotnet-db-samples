using Oracle.ManagedDataAccess.Client;

public sealed class OracleExecutor : IDisposable
{
    private readonly OracleConnection _connection;
    private readonly OracleConnectionStringBuilder _connectionString;

    public OracleExecutor(string connectionString, bool sysDba)
    {
        _connectionString = new OracleConnectionStringBuilder(connectionString);
        if (sysDba)
        {
            _connectionString.UserID = "SYS";
            _connectionString.DBAPrivilege = "SYSDBA";
        }
        _connection = new OracleConnection(_connectionString.ConnectionString);
        _connection.UseClientInitiatedCQN = true;
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public string UserId => _connectionString.UserID;

    public void ExecuteNonQuery(string sql)
    {
        using var command = new OracleCommand(sql, _connection);
        Console.Write($"▶️ {sql}");
        command.ExecuteNonQuery();
        Console.WriteLine(" ✅ ");
    }

    public void Watch(string sql, OnChangeEventHandler onChange)
    {
        var watchCommand = new OracleCommand(sql, _connection);

        var dependency = new OracleDependency(cmd: watchCommand, isNotifiedOnce: true, timeout: 300, isPersistent: false);
        dependency.OnChange += (sender, args) =>
        {
            watchCommand.Dispose();
            onChange(sender, args);
        };

        Console.Write($"👁️ {sql}");
        using var reader = watchCommand.ExecuteReader();
        while (reader.Read())
        {
        }
        Console.WriteLine(" ✅ ");
    }
}