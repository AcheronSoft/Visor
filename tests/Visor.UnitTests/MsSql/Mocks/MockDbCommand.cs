using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Visor.UnitTests.MsSql.Mocks;

public class MockDbCommand : DbCommand
{
    private readonly MockDbDataReader _reader = new();

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; } = new MockDbParameterCollection();
    protected override DbTransaction? DbTransaction { get; set; }
    public override bool DesignTimeVisible { get; set; }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _reader;
    public override int ExecuteNonQuery() => 0;
    public override object? ExecuteScalar() => null;
    public override void Prepare() { }
    public override void Cancel() { }
    protected override DbParameter CreateDbParameter() => new MockDbParameter();

    public void SetupData(List<User> data) => _reader.SetData(data);
}