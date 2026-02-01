using System.Runtime.CompilerServices;
using Oracle.ManagedDataAccess.Client;

public sealed class OracleExecutor
{
    static OracleExecutor()
    {
        OracleConfiguration.TraceLevel = 7;
        OracleConfiguration.TraceFileLocation = GetTraceFileLocation();
    }

    private static string GetTraceFileLocation([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "bin");

    private readonly OracleConnectionStringBuilder _connectionString;

    public OracleExecutor(string connectionString, bool sysDba)
    {
        _connectionString = new OracleConnectionStringBuilder(connectionString);
        if (sysDba)
        {
            _connectionString.UserID = "SYS";
            _connectionString.DBAPrivilege = "SYSDBA";
        }
    }

    public string UserId => _connectionString.UserID;

    public async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OracleCommand(sql, connection);
        Console.Write($"▶️ {sql}");
        await command.ExecuteNonQueryAsync(cancellationToken);
        Console.WriteLine(" ✅ ");
    }

    public async Task<OracleNotificationEventArgs> WatchAsync(string sql, Func<Task> onRegisteredAsync, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var watchCompletionSource = new TaskCompletionSource<OracleNotificationEventArgs>();
        cancellationToken.Register(() => watchCompletionSource.TrySetCanceled());

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var watchCommand = new OracleCommand(sql, connection);

        var dependencyTimeout = Convert.ToInt32(timeout.Add(TimeSpan.FromSeconds(10)).TotalSeconds);
        var dependency = new OracleDependency(cmd: watchCommand, isNotifiedOnce: true, timeout: dependencyTimeout, isPersistent: false);
        dependency.OnChange += (_, args) =>
        {
            watchCommand.Dispose();
            watchCompletionSource.SetResult(args);
        };

        Console.Write($"👁️ {sql}");
        await using var reader = await watchCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
        }
        Console.WriteLine(" ✅ ");

        await PrintNotificationRegistrationsAsync(cancellationToken);

        await onRegisteredAsync();

        return await watchCompletionSource.Task.WaitAsync(timeout, cancellationToken);
    }

    private async Task<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new OracleConnection(_connectionString.ConnectionString) { UseClientInitiatedCQN = true };
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task PrintNotificationRegistrationsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new OracleCommand("SELECT REGID, TABLE_NAME FROM USER_CHANGE_NOTIFICATION_REGS", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hasRegistration = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            Console.WriteLine($"🔔 Registration {reader.GetValue(0)} on {reader.GetValue(1)}");
            hasRegistration = true;
        }

        if (!hasRegistration)
        {
            Console.WriteLine("🔕 No registrations found.");
        }
    }
}