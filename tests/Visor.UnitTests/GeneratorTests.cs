using System.Data.Common;
using Visor.Core;

namespace Visor.UnitTests
{
    // 1. Фейковая реализация для тестов (Mock)
    public class FakeConnectionFactory : IVisorConnectionFactory
    {
        // Обновленный метод открытия соединения
        public Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Fake Connection Lease Acquired!");
            
            // Возвращаем структуру Lease. 
            // Передаем null вместо Connection, так как это Unit-тест и мы не идем в базу.
            // shouldDispose: true (имитируем, что это новое соединение)
            return Task.FromResult(new VisorDbLease(null!, null, shouldDispose: true)); 
        }

        // Заглушки для транзакций
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Fake Transaction Started");
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Fake Transaction Committed");
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Fake Transaction Rolled back");
            return Task.CompletedTask;
        }
    }

    public class GeneratorTests
    {
        [Fact]
        public void Test_DependencyInjection_Works()
        {
            // 1. Создаем зависимости
            var factory = new FakeConnectionFactory();
            
            // 2. Создаем сгенерированный класс (теперь он требует параметр!)
            // Если код не компилируется здесь -> значит генератор не обновился
            var repo = new MyFirstRepoImplementation(factory);

            // 3. Вызываем метод (я поменял имя метода в генераторе на HelloFromVisorAsync)
            // Убедись, что в генераторе ты тоже поменял сигнатуру на async Task
            // Если в интерфейсе IMyFirstRepo метод void, то добавь туда Task HelloFromVisorAsync();
            
            // *Временный хак*: Пока просто проверь, что 'new' работает.
            Assert.NotNull(repo);
        }
        
        
    }
}