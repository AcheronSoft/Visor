using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Visor.Abstractions;
using Visor.Core;
using Visor.SqlServer;
using Xunit;

namespace Visor.IntegrationTests
{
    // 1. Определяем интерфейс и DTO прямо здесь (или используем общие)
    [Visor]
    public interface IRealUserRepo
    {
        [Endpoint("sp_GetCount")]
        Task<int> GetCount();

        [Endpoint("sp_ImportUsers")]
        Task ImportUsers(List<UserItemDto> users);

        [Endpoint("sp_GetAllUsers")]
        Task<List<UserDto>> GetAll(bool onlyActive);
    }

    [VisorTable("dbo.UserListType")]
    public class UserItemDto
    {
        [VisorColumn(0, System.Data.SqlDbType.Int)]
        public int Id { get; set; }

        [VisorColumn(1, System.Data.SqlDbType.NVarChar, 100)]
        public string Name { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // 2. Сам тест
    public class RealDbTests
    {
        private const string ConnString = "Server=localhost,1433;Database=VisorTestDb;User Id=sa;Password=VisorStrongPass123!;TrustServerCertificate=True;";

        [Fact]
        public async Task FullFlow_Test()
        {
            // A. Подготовка
            IVisorConnectionFactory factory = new SqlServerConnectionFactory(ConnString);
            var repo = new RealUserRepoImplementation(factory); // Этот класс сгенерировал Visor!

            // B. Проверка чтения (Scalar)
            var countBefore = await repo.GetCount();
            Assert.True(countBefore >= 0);

            // C. Проверка вставки (TVP Streaming!)
            var newUsers = new List<UserItemDto>
            {
                new() { Name = "Visor User 1" },
                new() { Name = "Visor User 2" },
                new() { Name = "Visor User 3" } // Id не важен, он IDENTITY в базе
            };

            await repo.ImportUsers(newUsers);

            // D. Проверка результата (List Mapping)
            var countAfter = await repo.GetCount();
            Assert.Equal(countBefore + 3, countAfter);

            var allUsers = await repo.GetAll(false);
            Assert.Contains(allUsers, u => u.Name == "Visor User 1");
        }
    }
}