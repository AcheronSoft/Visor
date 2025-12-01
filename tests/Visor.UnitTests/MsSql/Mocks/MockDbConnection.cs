using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Visor.UnitTests.MsSql.Mocks;

public class MockDbConnection : DbConnection
{
    private readonly MockDbCommand _command = new();

    protected override DbCommand CreateDbCommand() => _command;
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new MockDbTransaction(this, isolationLevel);
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }
    [AllowNull]
    public override string ConnectionString { get; set; } = "Server=Fake;";
    public override string Database => "FakeDb";
    public override string DataSource => "FakeSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => ConnectionState.Open;
    
    // Helper to setup data for the reader
    public void SetupData(List<User> data) => _command.SetupData(data);
}