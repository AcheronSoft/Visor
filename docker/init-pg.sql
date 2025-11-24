-- 1. Создаем таблицу
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    external_id UUID
);

-- 2. Создаем Композитный Тип (аналог TVP)
-- Важно: имена полей должны совпадать с C# (или маппинг должен быть настроен, но Npgsql обычно требует совпадения)
CREATE TYPE user_list_type AS (
    id INT,
    name TEXT
    -- Порядок важен!
);

-- 3. Функция (Stored Procedure)
-- Принимает массив композитных типов
CREATE OR REPLACE FUNCTION sp_import_users(users user_list_type[]) 
RETURNS VOID AS $$
BEGIN
    INSERT INTO users (name, is_active, external_id)
    SELECT u.name, TRUE, gen_random_uuid()
    FROM unnest(users) AS u; -- Разворачиваем массив
END;
$$ LANGUAGE plpgsql;

-- 4. Функция GetCount
CREATE OR REPLACE FUNCTION sp_get_count() 
RETURNS INT AS $$
BEGIN
    RETURN (SELECT COUNT(*) FROM users);
END;
$$ LANGUAGE plpgsql;