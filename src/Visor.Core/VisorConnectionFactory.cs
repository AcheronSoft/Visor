using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Visor.Core
{
    // Этот класс должен быть зарегистрирован как SCOPED в DI контейнере!
    public abstract class VisorConnectionFactory : IVisorConnectionFactory, IAsyncDisposable
    {
        protected readonly string _connectionString;
        
        // Состояние текущей транзакции (Unit of Work)
        private DbConnection? _transactionConnection;
        private DbTransaction? _currentTransaction;

        protected VisorConnectionFactory(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
                
            _connectionString = connectionString;
        }

        // Абстрактный метод: только наследник знает, как сделать new SqlConnection()
        protected abstract DbConnection CreateConnection();

        public async Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default)
        {
            // СЦЕНАРИЙ 1: Мы внутри транзакции
            if (_currentTransaction != null)
            {
                // Возвращаем уже открытое транзакционное соединение.
                // shouldDispose = false, потому что закрывать его будет Commit/Rollback, а не метод репозитория.
                return new VisorDbLease(_transactionConnection!, _currentTransaction, shouldDispose: false);
            }

            // СЦЕНАРИЙ 2: Обычный запрос (без транзакции)
            var connection = CreateConnection();
            
            // Вот тут мы присваиваем строку! Наследнику не нужно об этом думать.
            connection.ConnectionString = _connectionString;

            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch
            {
                // Если не смогли открыть, сразу чистим ресурсы
                await connection.DisposeAsync();
                throw; // Тут можно обернуть в VisorConnectionException
            }

            // shouldDispose = true, потому что метод репозитория попользуется и закроет (using).
            return new VisorDbLease(connection, null, shouldDispose: true);
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
                throw new InvalidOperationException("Transaction is already active in this scope.");

            // Открываем долгоживущее соединение для транзакции
            _transactionConnection = CreateConnection();
            _transactionConnection.ConnectionString = _connectionString;
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

        // Реализация IAsyncDisposable (на случай, если DI контейнер убивает фабрику до завершения транзакции)
        public async ValueTask DisposeAsync()
        {
            await CleanupTransactionAsync();
        }
    }
}