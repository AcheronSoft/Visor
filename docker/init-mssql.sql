CREATE DATABASE VisorTestDb;
GO
USE VisorTestDb;
GO

-- 1. Таблица
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    IsActive BIT DEFAULT 1,
    ExternalId UNIQUEIDENTIFIER NULL
);
GO

-- 2. TVP Тип (Важно: порядок полей должен совпадать с VisorColumn!)
CREATE TYPE dbo.UserListType AS TABLE (
    Id INT,
    Name NVARCHAR(100)
);
GO

-- 3. Процедуры

CREATE OR ALTER PROCEDURE sp_GetCount
AS
BEGIN
    SELECT COUNT(*) FROM Users;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetUserById
    @id INT
AS
BEGIN
    SELECT Id, Name, IsActive, ExternalId FROM Users WHERE Id = @id;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetAllUsers
    @onlyActive BIT
AS
BEGIN
    SELECT Id, Name, IsActive, ExternalId 
    FROM Users 
    WHERE (@onlyActive = 0 OR IsActive = 1);
END;
GO

CREATE OR ALTER PROCEDURE sp_ImportUsers
    @users dbo.UserListType READONLY
AS
BEGIN
    -- Вставка из TVP
    INSERT INTO Users (Name, IsActive, ExternalId)
    SELECT Name, 1, NEWID()
    FROM @users;
END;
GO

-- Добавим пару юзеров для теста
INSERT INTO Users (Name) VALUES ('Test User 1'), ('Test User 2');
GO