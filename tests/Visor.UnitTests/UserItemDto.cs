using System.Data;
using Visor.Abstractions;

namespace Visor.UnitTests;

[VisorTable("dbo.UserListType")] // <-- Имя типа в базе
public class UserItemDto
{
    [VisorColumn(0, SqlDbType.Int)] // <-- Порядок 0
    public int Id { get; set; }

    [VisorColumn(1, SqlDbType.NVarChar, 100)] // <-- Порядок 1, Размер 100
    public string Name { get; set; } = "";
}