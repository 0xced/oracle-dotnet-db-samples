using Testcontainers.Oracle;

try
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, _) => cts.Cancel();
    return await RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 70;
}

async Task<int> RunAsync(CancellationToken cancellationToken)
{
    await using var container = new OracleBuilder().WithImage("gvenzl/oracle-free:23-slim-faststart").Build();
    await container.StartAsync(cancellationToken);
    var connectionString = container.GetConnectionString();

    var executor = new OracleExecutor(connectionString, sysDba: false);
    var sysDbaExecutor = new OracleExecutor(connectionString, sysDba: true);
    await sysDbaExecutor.ExecuteNonQueryAsync($"grant change notification to {executor.UserId}", cancellationToken);

    await executor.ExecuteNonQueryAsync("create table dept (deptno number(2,0), dname varchar2(14), loc varchar2(13), constraint pk_dept primary key (deptno))", cancellationToken);

    try
    {
        var eventArgs = await executor.WatchAsync("select deptno from dept", onRegisteredAsync: async () =>
        {
            await executor.ExecuteNonQueryAsync("insert into dept (deptno, dname, loc) values(10, 'Accounting', 'New York')", cancellationToken);
        }, timeout: TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
        Console.WriteLine($"🪄 {eventArgs.Info} detected on {string.Join(',', eventArgs.ResourceNames)}");
        return 0;
    }
    catch (TimeoutException)
    {
        Console.WriteLine("💥 change went undetected");
        return 69;
    }
}
