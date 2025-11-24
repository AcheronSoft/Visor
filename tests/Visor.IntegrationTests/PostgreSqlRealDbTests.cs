using System.Data;
using Visor.Abstractions;
using Visor.PostgreSql; // Bootstrapper будет здесь
using Xunit;
using Npgsql;

namespace Visor.IntegrationTests
{
    // Указываем провайдер Postgres!
    [Visor(VisorProvider.PostgreSql)]
    public interface IPgUserRepo
    {
        [Endpoint("sp_get_count")]
        Task<int> GetCount();

        [Endpoint("sp_import_users")]
        Task ImportUsers(List<PgUserItem> users);
    }

    // Указываем имя типа в Postgres
    [VisorTable("user_list_type")]
    public class PgUserItem
    {
        // В Postgres порядок не так критичен при MapComposite, если имена совпадают, 
        // но для VisorColumn мы оставляем порядок для единообразия
        [VisorColumn(0, SqlDbType.Int, Name = "id")]
        public int Id { get; set; }

        [VisorColumn(1, System.Data.SqlDbType.NVarChar)]
        public string name { get; set; } = string.Empty;
    }

    public class PostgreSqlRealDbTests
    {
        private const string ConnString = "Host=localhost;Port=5432;Database=VisorTestDb;Username=postgres;Password=VisorStrongPass123!";

        [Fact]
        public async Task Pg_FullFlow_Test()
        {
            // 1. Настройка маппинга (Вот он, наш сгенерированный метод!)
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnString);
            dataSourceBuilder.UseVisor(); // <--- МАГИЯ ТУТ
            await using var dataSource = dataSourceBuilder.Build();

            // 2. Создаем фабрику (в Postgres мы используем DataSource вместо просто строки, если хотим маппинг)
            // Но наша фабрика пока принимает строку.
            // ХАК: Npgsql 7+ рекомендует DataSource. 
            // Давай пока проверим, сработает ли "Global Type Mapper" (устаревший) или нам надо обновить фабрику.
            // Для теста используем простой путь: передадим строку, но перед этим дернем MapComposite глобально (если это старый Npgsql)
            // ИЛИ (Правильно): Обновим Visor.PostgreSql чтобы он мог принимать DataSource.
            
            // Пока попробуем простой вариант (VisorConnectionFactory создает new NpgsqlConnection).
            // Чтобы маппинг заработал с new NpgsqlConnection(), нужно либо использовать Global Mapping (Obsolete),
            // либо передавать Connection, созданный из DataSource.
            
            // Давай обновим PostgreSqlConnectionFactory, чтобы она принимала DataSource?
            // Это лучшее решение.
        }
    }
}