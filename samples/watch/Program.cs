using Testcontainers.Oracle;

try
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, _) => cts.Cancel();

    await using var container = new OracleBuilder().WithImage("gvenzl/oracle-free:23-slim-faststart").Build();
    await container.StartAsync(cts.Token);
    var connectionString = container.GetConnectionString();

    var executor = new OracleExecutor(connectionString, sysDba: false);
    var sysDbaExecutor = new OracleExecutor(connectionString, sysDba: true);
    await sysDbaExecutor.ExecuteNonQueryAsync($"grant change notification to {executor.UserId}", cts.Token);
    await executor.ExecuteNonQueryAsync("create table dept (deptno number(2,0), dname varchar2(14), loc varchar2(13), constraint pk_dept primary key (deptno))", cts.Token);

    var completedTask = await Task.WhenAny(WatchAsync(executor, cts.Token), ControlAsync(executor, cts.Token));
    await completedTask;

    return 0;
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

async Task ControlAsync(OracleExecutor executor, CancellationToken cancellationToken)
{
    var i = 1;
    Console.WriteLine("******************************************");
    Console.WriteLine("* Press `i` to insert a row, `q` to quit *");
    Console.WriteLine("******************************************");
    while (!cancellationToken.IsCancellationRequested)
    {
        var keyInfo = Console.ReadKey();
        Console.WriteLine();

        if (keyInfo.Key == ConsoleKey.I)
        {
            await executor.ExecuteNonQueryAsync($"insert into dept (deptno, dname, loc) values({i++}, 'Accounting', 'New York')", cancellationToken);
        }

        if (keyInfo.Key == ConsoleKey.Q)
        {
            break;
        }
    }
}

async Task WatchAsync(OracleExecutor executor, CancellationToken cancellationToken)
{
    await foreach (var eventArgs in executor.WatchAsync("select deptno from dept", timeout: TimeSpan.FromSeconds(20), cancellationToken))
    {
        Console.WriteLine($"🪄 {eventArgs.Info} detected on {string.Join(',', eventArgs.ResourceNames)}");
    }
}
