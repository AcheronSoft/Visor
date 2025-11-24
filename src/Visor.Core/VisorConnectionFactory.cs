using System.Data.Common;
using System.Text.RegularExpressions;
using Visor.Core.Exceptions;

namespace Visor.Core;

public abstract partial class VisorConnectionFactory : IVisorConnectionFactory, IAsyncDisposable
{
    private readonly string _connectionString;
        
    // State for the current Unit of Work transaction.
    private DbConnection? _transactionConnection;
    private DbTransaction? _currentTransaction;

    // Regex for finding password fields in a connection string.
    private static readonly Regex PasswordRegex = SearchPasswordRegex();
    
    // Using "en-US" culture as connection string keys are always in English.
    [GeneratedRegex(@"(?i)(password|pwd)\s*=\s*[^;]*", RegexOptions.None, cultureName: "en-US")]
    private static partial Regex SearchPasswordRegex();

    /// <summary>
    /// Replaces the actual password with '******' for safe logging.
    /// </summary>
    private static string SanitizeConnectionString(string connectionString)
    {
        return string.IsNullOrEmpty(connectionString) 
            ? string.Empty : 
            PasswordRegex.Replace(connectionString, "$1=******");
    }

    protected VisorConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
                
        _connectionString = connectionString;
    }

    protected abstract DbConnection CreateConnection();

    public async Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default)
    {
        // SCENARIO 1: We are inside an active transaction.
        if (_currentTransaction != null)
        {
            return new VisorDbLease(_transactionConnection!, _currentTransaction, shouldDispose: false);
        }

        // SCENARIO 2: A regular, non-transactional query.
        var connection = CreateConnection();

        // If the driver (e.g., NpgsqlDataSource) has already configured the connection, do not overwrite it.
        if (string.IsNullOrEmpty(connection.ConnectionString))
        {
            connection.ConnectionString = _connectionString;
        }

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await connection.DisposeAsync();
                
            // SECURITY: Sanitize the connection string before logging the exception.
            var safeConnectionString = SanitizeConnectionString(_connectionString);

            throw new VisorConnectionException(
                $"Failed to open connection to database '{connection.Database}'. Error: {ex.Message}", 
                safeConnectionString, 
                ex);
        }

        return new VisorDbLease(connection, null, shouldDispose: true);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transaction is already active in this scope.");

        _transactionConnection = CreateConnection();
            
        if (string.IsNullOrEmpty(_transactionConnection.ConnectionString))
        {
            _transactionConnection.ConnectionString = _connectionString;
        }
            
        await _transactionConnection.OpenAsync(cancellationToken);

        _currentTransaction = await _transactionConnection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No active transaction to commit.");

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await CleanupTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await CleanupTransactionAsync();
        }
    }

    private async Task CleanupTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        if (_transactionConnection != null)
        {
            await _transactionConnection.DisposeAsync();
            _transactionConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupTransactionAsync();
    }
}
