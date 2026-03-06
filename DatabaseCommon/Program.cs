using Npgsql;
using Bogus;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        // Connection to postgres database to create new database
        const string connectionString =
            "Host=localhost;port=5442;Username=postgres;Password=1;Database=postgres;";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Check current server encoding
        await using (var checkEncodingCmd = new NpgsqlCommand("SHOW server_encoding", conn))
        {
            var serverEncoding = await checkEncodingCmd.ExecuteScalarAsync();
            Console.WriteLine($"Server encoding: {serverEncoding}");
        }

        // Set client encoding to UTF8
        await using var setEncodingCmd = new NpgsqlCommand("SET client_encoding TO 'UTF8'", conn);
        await setEncodingCmd.ExecuteNonQueryAsync();

        // Terminate existing connections to the database if it exists
        await using (var terminateCmd = new NpgsqlCommand(@"
            SELECT pg_terminate_backend(pid) 
            FROM pg_stat_activity 
            WHERE datname = 'order_comparison_db' AND pid <> pg_backend_pid()", conn))
        {
            await terminateCmd.ExecuteNonQueryAsync();
        }

        // Drop database if exists
        await using var dropCmd = new NpgsqlCommand("DROP DATABASE IF EXISTS order_comparison_db", conn);
        await dropCmd.ExecuteNonQueryAsync();

        // Create database with explicit UTF8 encoding and template0 (which doesn't have encoding issues)
        await using var createCmd = new NpgsqlCommand(
            "CREATE DATABASE order_comparison_db ENCODING 'UTF8' TEMPLATE template0", conn);
        await createCmd.ExecuteNonQueryAsync();

        await conn.CloseAsync();

        // Connection to the new database
        var dbConnectionString =
            "Host=localhost;port=5442;Username=postgres;Password=1;Database=order_comparison_db;";
        await using var dbConn = new NpgsqlConnection(dbConnectionString);
        await dbConn.OpenAsync();

        // Set client encoding for new connection
        await using var setDbEncodingCmd = new NpgsqlCommand("SET client_encoding TO 'UTF8'", dbConn);
        await setDbEncodingCmd.ExecuteNonQueryAsync();

        // Verify the new database encoding
        await using (var checkDbEncodingCmd = new NpgsqlCommand(
            "SELECT datname, encoding, datcollate, datctype FROM pg_database WHERE datname = 'order_comparison_db'", 
            dbConn))
        {
            await using var reader = await checkDbEncodingCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine($"Database encoding: {reader.GetInt32(1)} (6 = UTF8)");
            }
        }

        // Создаем обе схемы
        await CreateNormalizedSchema(dbConn);
        await CreateDictionarySchema(dbConn);

        // Заполняем данными
        await SeedData(dbConn);

        Console.WriteLine("База данных успешно создана и заполнена!");
    }

    static async Task CreateNormalizedSchema(NpgsqlConnection conn)
    {
        // Нормализованная структура - отдельные таблицы для каждого справочника
        var createTables = @"
            CREATE TABLE IF NOT EXISTS order_types (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS delivery_types (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS order_statuses (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS payment_statuses (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS payment_types (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS currencies (
                id SERIAL PRIMARY KEY,
                code VARCHAR(3) NOT NULL UNIQUE,
                name VARCHAR(50) NOT NULL,
                symbol VARCHAR(5),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS customer_categories (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                discount_percent DECIMAL(5,2),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS shipping_companies (
                id SERIAL PRIMARY KEY,
                code VARCHAR(20) NOT NULL UNIQUE,
                name VARCHAR(100) NOT NULL,
                phone VARCHAR(20),
                rating DECIMAL(2,1),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS orders_normalized (
                id SERIAL PRIMARY KEY,
                order_number VARCHAR(20) NOT NULL UNIQUE,
                customer_name VARCHAR(100) NOT NULL,
                customer_email VARCHAR(100),
                order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                total_amount DECIMAL(10,2) NOT NULL,
               
                order_type_id INTEGER REFERENCES order_types(id),
                delivery_type_id INTEGER REFERENCES delivery_types(id),
                order_status_id INTEGER REFERENCES order_statuses(id),
                payment_status_id INTEGER REFERENCES payment_statuses(id),
                payment_type_id INTEGER REFERENCES payment_types(id),
                currency_id INTEGER REFERENCES currencies(id),
                customer_category_id INTEGER REFERENCES customer_categories(id),
                shipping_company_id INTEGER REFERENCES shipping_companies(id),
                
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_orders_normalized_order_type ON orders_normalized(order_type_id);
            CREATE INDEX IF NOT EXISTS idx_orders_normalized_status ON orders_normalized(order_status_id);
            CREATE INDEX IF NOT EXISTS idx_orders_normalized_payment_type ON orders_normalized(payment_type_id);
            CREATE INDEX IF NOT EXISTS idx_orders_normalized_customer_category ON orders_normalized(customer_category_id);
            CREATE INDEX IF NOT EXISTS idx_orders_normalized_order_date ON orders_normalized(order_date);
        ";

        await using var cmd = new NpgsqlCommand(createTables, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task CreateDictionarySchema(NpgsqlConnection conn)
    {
        // ПЛОХОЙ ПОДХОД: единый справочник для всех типов данных
        var createTables = @"
            CREATE TABLE IF NOT EXISTS dictionary (
                id SERIAL PRIMARY KEY,
                dictionary_type VARCHAR(50) NOT NULL,
                code VARCHAR(50) NOT NULL,
                value VARCHAR(255) NOT NULL,
                additional_data JSONB,
                is_active BOOLEAN DEFAULT true,
                sort_order INTEGER,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(dictionary_type, code)
            );

            CREATE INDEX IF NOT EXISTS idx_dictionary_type ON dictionary(dictionary_type);
            CREATE INDEX IF NOT EXISTS idx_dictionary_code ON dictionary(code);
            CREATE INDEX IF NOT EXISTS idx_dictionary_type_code ON dictionary(dictionary_type, code);

            CREATE TABLE IF NOT EXISTS orders_dictionary (
                id SERIAL PRIMARY KEY,
                order_number VARCHAR(20) NOT NULL UNIQUE,
                customer_name VARCHAR(100) NOT NULL,
                customer_email VARCHAR(100),
                order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                total_amount DECIMAL(10,2) NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS order_attributes (
                id SERIAL PRIMARY KEY,
                order_id INTEGER REFERENCES orders_dictionary(id) ON DELETE CASCADE,
                dictionary_id INTEGER REFERENCES dictionary(id),
                attribute_value TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(order_id, dictionary_id)
            );

            CREATE INDEX IF NOT EXISTS idx_order_attributes_order ON order_attributes(order_id);
            CREATE INDEX IF NOT EXISTS idx_order_attributes_dictionary ON order_attributes(dictionary_id);

            CREATE TABLE IF NOT EXISTS order_metrics (
                id SERIAL PRIMARY KEY,
                order_id INTEGER REFERENCES orders_dictionary(id) ON DELETE CASCADE,
                metric_code VARCHAR(50) NOT NULL,
                metric_value DECIMAL(10,2),
                metric_unit VARCHAR(10),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_order_metrics_order ON order_metrics(order_id);
        ";

        await using var cmd = new NpgsqlCommand(createTables, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task SeedData(NpgsqlConnection conn)
    {
        var faker = new Faker();

        // Заполняем нормализованные справочники
        await SeedNormalizedDictionaries(conn);

        // Заполняем единый словарь (плохой подход)
        await SeedDictionary(conn);

        // Генерируем заказы
        const int orderCount = 5000;

        Console.WriteLine($"Генерация {orderCount} заказов...");

        // Заполняем нормализованные заказы
        await SeedNormalizedOrders(conn, orderCount);

        // Заполняем заказы со словарем
        await SeedDictionaryOrders(conn, orderCount);

        Console.WriteLine("Все данные успешно сгенерированы!");
    }

    static async Task SeedNormalizedDictionaries(NpgsqlConnection conn)
    {
        var insertDictionaries = @"
            INSERT INTO order_types (code, name, description) VALUES 
                ('RETAIL', 'Розничный', 'Розничная продажа'),
                ('WHOLESALE', 'Оптовый', 'Оптовая продажа'),
                ('ONLINE', 'Онлайн', 'Заказ через интернет'),
                ('INSTORE', 'В магазине', 'Оформлен в магазине'),
                ('PHONE', 'По телефону', 'Заказ по телефону')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO delivery_types (code, name, description) VALUES 
                ('COURIER', 'Курьер', 'Курьерская доставка'),
                ('PICKUP', 'Самовывоз', 'Самовывоз из магазина'),
                ('POST', 'Почта', 'Почтовая доставка'),
                ('EXPRESS', 'Экспресс', 'Экспресс-доставка'),
                ('INTERNATIONAL', 'Международная', 'Международная доставка')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO order_statuses (code, name, description) VALUES 
                ('PENDING', 'Ожидание', 'Ожидает обработки'),
                ('PROCESSING', 'В обработке', 'Заказ обрабатывается'),
                ('SHIPPED', 'Отправлен', 'Заказ отправлен'),
                ('DELIVERED', 'Доставлен', 'Заказ доставлен'),
                ('CANCELLED', 'Отменен', 'Заказ отменен')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO payment_statuses (code, name, description) VALUES 
                ('PENDING', 'Ожидание', 'Ожидает оплаты'),
                ('PAID', 'Оплачен', 'Оплачен полностью'),
                ('PARTIAL', 'Частично', 'Частично оплачен'),
                ('FAILED', 'Ошибка', 'Ошибка оплаты'),
                ('REFUNDED', 'Возврат', 'Возврат средств')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO payment_types (code, name, description) VALUES 
                ('CARD', 'Карта', 'Банковская карта'),
                ('CASH', 'Наличные', 'Наличный расчет'),
                ('TRANSFER', 'Перевод', 'Банковский перевод'),
                ('PAYPAL', 'PayPal', 'PayPal'),
                ('CRYPTO', 'Криптовалюта', 'Оплата криптовалютой')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO currencies (code, name, symbol) VALUES 
                ('USD', 'US Dollar', '$'),
                ('EUR', 'Euro', '€'),
                ('RUB', 'Russian Ruble', '₽'),
                ('GBP', 'British Pound', '£'),
                ('JPY', 'Japanese Yen', '¥')
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO customer_categories (code, name, discount_percent) VALUES 
                ('REGULAR', 'Обычный', 0),
                ('SILVER', 'Серебряный', 5),
                ('GOLD', 'Золотой', 10),
                ('PLATINUM', 'Платиновый', 15),
                ('VIP', 'VIP', 20)
            ON CONFLICT (code) DO NOTHING;

            INSERT INTO shipping_companies (code, name, phone, rating) VALUES 
                ('DHL', 'DHL Express', '+1-800-225-5345', 4.8),
                ('FEDEX', 'FedEx', '+1-800-463-3339', 4.7),
                ('UPS', 'UPS', '+1-800-742-5877', 4.6),
                ('USPS', 'USPS', '+1-800-275-8777', 4.2),
                ('DPD', 'DPD', '+44-247-666-0500', 4.4)
            ON CONFLICT (code) DO NOTHING;
        ";

        await using var cmd = new NpgsqlCommand(insertDictionaries, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task SeedDictionary(NpgsqlConnection conn)
    {
        var insertDictionary = @"
            INSERT INTO dictionary (dictionary_type, code, value, sort_order) VALUES
                ('order_type', 'RETAIL', 'Розничный', 1),
                ('order_type', 'WHOLESALE', 'Оптовый', 2),
                ('order_type', 'ONLINE', 'Онлайн', 3),
                ('order_type', 'INSTORE', 'В магазине', 4),
                ('order_type', 'PHONE', 'По телефону', 5)
            ON CONFLICT (dictionary_type, code) DO NOTHING;

          -- Delivery Types
            INSERT INTO dictionary (dictionary_type, code, value, sort_order) VALUES
                ('delivery_type', 'COURIER', 'Курьер', 1),
                ('delivery_type', 'PICKUP', 'Самовывоз', 2),
                ('delivery_type', 'POST', 'Почта', 3),
                ('delivery_type', 'EXPRESS', 'Экспресс', 4),
                ('delivery_type', 'INTERNATIONAL', 'Международная', 5);

            -- Order Statuses
            INSERT INTO dictionary (dictionary_type, code, value, sort_order) VALUES
                ('order_status', 'PENDING', 'Ожидание', 1),
                ('order_status', 'PROCESSING', 'В обработке', 2),
                ('order_status', 'SHIPPED', 'Отправлен', 3),
                ('order_status', 'DELIVERED', 'Доставлен', 4),
                ('order_status', 'CANCELLED', 'Отменен', 5);

            -- Payment Statuses
            INSERT INTO dictionary (dictionary_type, code, value, additional_data) VALUES
                ('payment_status', 'PENDING', 'Ожидание', '{\""color\"": \""yellow\""}'),
                ('payment_status', 'PAID', 'Оплачен', '{\""color\"": \""green\""}'),
                ('payment_status', 'PARTIAL', 'Частично', '{\""color\"": \""blue\""}'),
                ('payment_status', 'FAILED', 'Ошибка', '{\""color\"": \""red\""}'),
                ('payment_status', 'REFUNDED', 'Возврат', '{\""color\"": \""orange\""}');

            -- Payment Types
            INSERT INTO dictionary (dictionary_type, code, value) VALUES
                ('payment_type', 'CARD', 'Карта'),
                ('payment_type', 'CASH', 'Наличные'),
                ('payment_type', 'TRANSFER', 'Перевод'),
                ('payment_type', 'PAYPAL', 'PayPal'),
                ('payment_type', 'CRYPTO', 'Криптовалюта');

          -- Currencies - Using ASCII only in JSON
INSERT INTO dictionary (dictionary_type, code, value, additional_data) VALUES
    ('currency', 'USD', 'US Dollar', '{""symbol"": ""USD""}'),
    ('currency', 'EUR', 'Euro', '{""symbol"": ""EUR""}'),
    ('currency', 'RUB', 'Russian Ruble', '{""symbol"": ""RUB""}'),
    ('currency', 'GBP', 'British Pound', '{""symbol"": ""GBP""}'),
    ('currency', 'JPY', 'Japanese Yen', '{""symbol"": ""JPY""}'),

-- Customer Categories
INSERT INTO dictionary (dictionary_type, code, value, additional_data) VALUES
    ('customer_category', 'REGULAR', 'Обычный', '{""discount"": 0}'),
    ('customer_category', 'SILVER', 'Серебряный', '{""discount"": 5}'),
    ('customer_category', 'GOLD', 'Золотой', '{""discount"": 10}'),
    ('customer_category', 'PLATINUM', 'Платиновый', '{""discount"": 15}'),
    ('customer_category', 'VIP', 'VIP', '{""discount"": 20}'),

-- Shipping Companies
INSERT INTO dictionary (dictionary_type, code, value, additional_data) VALUES
    ('shipping_company', 'DHL', 'DHL Express', '{""phone"": ""+1-900-225-5345"", ""rating"": 4.8}'),
    ('shipping_company', 'FEDEX', 'FedEx', '{""phone"": ""+1-800-215-5345"", ""rating"": 4.7}'),
            ('shipping_company', 'UPS', 'UPS',  '{""phone"": ""+2-800-225-5345"", ""rating"": 4.1}'),
        ('shipping_company', 'USPS', 'USPS',  '{""phone"": ""+1-830-225-5345"", ""rating"": 4.6}'),
        ('shipping_company', 'DPD', 'DPD',  '{""phone"": ""+1-100-225-5345"", ""rating"": 4.2}')";

        await using var cmd = new NpgsqlCommand(insertDictionary, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task SeedNormalizedOrders(NpgsqlConnection conn, int count)
    {
        var faker = new Faker();

        for (int i = 0; i < count; i += 1000)
        {
            var batchSize = Math.Min(1000, count - i);
            var values = new List<string>();

            for (int j = 0; j < batchSize; j++)
            {
                values.Add($@"(
                    'ORD-NORM-{(i + j + 1):D5}',
                    '{faker.Person.FullName.Replace("'", "''")}',
                    '{faker.Internet.Email()}',
                    TIMESTAMP '2024-01-01' + INTERVAL '{faker.Random.Int(0, 365)} days',
                    {faker.Random.Decimal(10, 5000):F2},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)},
                    {faker.Random.Int(1, 5)}
                )");
            }

            var sql = $@"
                INSERT INTO orders_normalized (
                    order_number, customer_name, customer_email, order_date, total_amount,
                    order_type_id, delivery_type_id, order_status_id, 
                    payment_status_id, payment_type_id, currency_id,
                    customer_category_id, shipping_company_id
                ) VALUES 
                {string.Join(",\n", values)}";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    static async Task SeedDictionaryOrders(NpgsqlConnection conn, int count)
    {
        var faker = new Faker();

        for (int i = 0; i < count; i += 500)
        {
            var batchSize = Math.Min(500, count - i);

            for (int j = 0; j < batchSize; j++)
            {
                var orderNumber = $"ORD-DICT-{(i + j + 1):D5}";
                var customerName = faker.Person.FullName.Replace("'", "''");
                var email = faker.Internet.Email();
                var orderDate = $"TIMESTAMP '2024-01-01' + INTERVAL '{faker.Random.Int(0, 365)} days'";
                var amount = faker.Random.Decimal(10, 5000);

                var insertOrderSql = $@"
                    INSERT INTO orders_dictionary (
                        order_number, customer_name, customer_email, order_date, total_amount
                    ) VALUES (
                        '{orderNumber}',
                        '{customerName}',
                        '{email}',
                        {orderDate},
                        {amount:F2}
                    ) RETURNING id";

                int orderId;
                await using (var cmd = new NpgsqlCommand(insertOrderSql, conn))
                {
                    orderId = (int)(await cmd.ExecuteScalarAsync());
                }

                var attributeValues = new List<string>();
                attributeValues.Add($"({orderId}, {faker.Random.Int(1, 5)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(6, 10)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(11, 15)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(16, 20)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(21, 25)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(26, 30)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(31, 35)}, NULL)");
                attributeValues.Add($"({orderId}, {faker.Random.Int(36, 40)}, NULL)");

                var insertAttributesSql = $@"
                    INSERT INTO order_attributes (order_id, dictionary_id, attribute_value)
                    VALUES {string.Join(",\n", attributeValues)}";

                await using var attrCmd = new NpgsqlCommand(insertAttributesSql, conn);
                await attrCmd.ExecuteNonQueryAsync();

                if (faker.Random.Bool(0.3f))
                {
                    var metrics = new List<string>();
                    if (faker.Random.Bool())
                        metrics.Add($"({orderId}, 'discount', {faker.Random.Decimal(0, 20):F2}, 'percent')");
                    if (faker.Random.Bool())
                        metrics.Add($"({orderId}, 'weight', {faker.Random.Decimal(1, 50):F2}, 'kg')");

                    if (metrics.Any())
                    {
                        var insertMetricsSql = $@"
                            INSERT INTO order_metrics (order_id, metric_code, metric_value, metric_unit)
                            VALUES {string.Join(",\n", metrics)}";

                        await using var metricCmd = new NpgsqlCommand(insertMetricsSql, conn);
                        await metricCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}