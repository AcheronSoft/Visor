using System.Collections;
using System.Data.Common;

namespace Visor.UnitTests.MsSql.Mocks;

public class MockDbDataReader : DbDataReader
{
    private List<User> _data = [];
    private int _currentIndex = -1;

    public void SetData(List<User> data)
    {
        _data = data;
        _currentIndex = -1;
    }

    // --- Core Implementation ---

    public override bool Read()
    {
        _currentIndex++;
        return _currentIndex < _data.Count;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) 
        => Task.FromResult(Read());

    public override int GetOrdinal(string name)
    {
        return name switch
        {
            "Id" => 0,
            "Name" => 1,
            "IsActive" => 2,
            "ExternalId" => 3,
            _ => -1
        };
    }

    public override bool IsDBNull(int ordinal)
    {
        if (_currentIndex < 0 || _currentIndex >= _data.Count) return true;
        
        var user = _data[_currentIndex];
        return ordinal switch
        {
            3 => user.ExternalId == null,
            _ => false
        };
    }

    public override T GetFieldValue<T>(int ordinal)
    {
        if (_currentIndex < 0 || _currentIndex >= _data.Count) 
            throw new InvalidOperationException("No data present or reader is closed.");

        var user = _data[_currentIndex];
        object value = ordinal switch
        {
            0 => user.Id,
            1 => user.Name,
            2 => user.IsActive,
            3 => user.ExternalId ?? throw new InvalidCastException("Null value"),
            _ => throw new IndexOutOfRangeException($"Column ordinal {ordinal} not found")
        };

        return (T)value;
    }

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) 
        => Task.FromResult(GetFieldValue<T>(ordinal));

    // --- Implemented Proxies (Redirect to GetFieldValue) ---

    public override object this[int ordinal] => GetFieldValue<object>(ordinal);
    public override object this[string name] => GetFieldValue<object>(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) => GetFieldValue<bool>(ordinal);
    public override byte GetByte(int ordinal) => GetFieldValue<byte>(ordinal);
    public override char GetChar(int ordinal) => GetFieldValue<char>(ordinal);
    public override DateTime GetDateTime(int ordinal) => GetFieldValue<DateTime>(ordinal);
    public override decimal GetDecimal(int ordinal) => GetFieldValue<decimal>(ordinal);
    public override double GetDouble(int ordinal) => GetFieldValue<double>(ordinal);
    public override float GetFloat(int ordinal) => GetFieldValue<float>(ordinal);
    public override Guid GetGuid(int ordinal) => GetFieldValue<Guid>(ordinal);
    public override short GetInt16(int ordinal) => GetFieldValue<short>(ordinal);
    public override int GetInt32(int ordinal) => GetFieldValue<int>(ordinal);
    public override long GetInt64(int ordinal) => GetFieldValue<long>(ordinal);
    public override string GetString(int ordinal) => GetFieldValue<string>(ordinal);
    public override object GetValue(int ordinal) => GetFieldValue<object>(ordinal);

    public override string GetName(int ordinal) 
    {
        return ordinal switch
        {
            0 => "Id", 1 => "Name", 2 => "IsActive", 3 => "ExternalId", _ => ""
        };
    }

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    
    public override Type GetFieldType(int ordinal)
    {
        return ordinal switch
        {
            0 => typeof(int),
            1 => typeof(string),
            2 => typeof(bool),
            3 => typeof(Guid),
            _ => typeof(object)
        };
    }

    public override int GetValues(object[] values)
    {
        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    // --- Complex types stubbed safely ---
    
    // For unit tests, returning 0 usually implies "no bytes read", which is safer than throwing.
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;

    // --- Metadata ---

    public override int Depth => 0;
    public override int FieldCount => 4;
    public override bool HasRows => _data.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
}