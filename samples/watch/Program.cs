using Testcontainers.Oracle;

var resetEvent = new ManualResetEventSlim(false);

await using var container = new OracleBuilder().WithImage("gvenzl/oracle-free:23-slim-faststart").Build();
await container.StartAsync();
var connectionString = container.GetConnectionString();

using var executor = new OracleExecutor(connectionString, sysDba: false);
using (var sysDbaExecutor = new OracleExecutor(connectionString, sysDba: true))
{
    sysDbaExecutor.ExecuteNonQuery($"grant change notification to {executor.UserId}");
}

executor.ExecuteNonQuery("create table dept (deptno number(2,0), dname varchar2(14), loc varchar2(13), constraint pk_dept primary key (deptno))");
executor.Watch("select deptno from dept", onChange: (_, eventArgs) =>
{
    Console.WriteLine($"🪄 {eventArgs.Info} detected on {string.Join(',', eventArgs.ResourceNames)}");
    resetEvent.Set();
});
executor.ExecuteNonQuery("insert into dept (deptno, dname, loc) values(10, 'Accounting', 'New York')");

var success = resetEvent.Wait(TimeSpan.FromSeconds(20));
if (!success)
{
    Console.WriteLine("💥 change went undetected");
}

var (stdout, _) = await container.GetLogsAsync();
Console.Write(stdout);

return success ? 0 : 1;
