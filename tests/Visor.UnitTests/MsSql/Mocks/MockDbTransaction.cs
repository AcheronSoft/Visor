using System.Data;
using System.Data.Common;

namespace Visor.UnitTests.MsSql.Mocks;

public class MockDbTransaction(DbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public override void Commit() { }
    public override void Rollback() { }
    protected override DbConnection DbConnection { get; } = connection;
    public override IsolationLevel IsolationLevel { get; } = isolationLevel;
}