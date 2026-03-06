using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderSystemComparison.Data;

namespace NormalizedApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Нормализованная схема (со справочниками) ===\n");
        
        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NormalizedDbContext>();
        
        // Инициализация и заполнение данных
        await InitializeDatabase(context);
        
        // Выполнение тестовых запросов
        await RunQueries(context);
    }
    
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                services.AddDbContext<NormalizedDbContext>(options =>
                    options.UseNpgsql(connectionString));
            });
    
    static async Task InitializeDatabase(NormalizedDbContext context)
    {
        Console.WriteLine("Инициализация базы данных...");
        
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // Генерация справочников
        var orderTypes = DataGenerator.GenerateOrderTypes();
        var deliveryTypes = DataGenerator.GenerateDeliveryTypes();
        var orderStatuses = DataGenerator.GenerateOrderStatuses();
        var paymentStatuses = DataGenerator.GeneratePaymentStatuses();
        var paymentTypes = DataGenerator.GeneratePaymentTypes();
        
        await context.OrderTypes.AddRangeAsync(orderTypes);
        await context.DeliveryTypes.AddRangeAsync(deliveryTypes);
        await context.OrderStatuses.AddRangeAsync(orderStatuses);
        await context.PaymentStatuses.AddRangeAsync(paymentStatuses);
        await context.PaymentTypes.AddRangeAsync(paymentTypes);
        
        await context.SaveChangesAsync();
        
        // Генерация заказов
        var orders = DataGenerator.GenerateNormalizedOrders(
            orderTypes, deliveryTypes, orderStatuses, paymentStatuses, paymentTypes, 10000);
        
        await context.Orders.AddRangeAsync(orders);
        await context.SaveChangesAsync();
        
        Console.WriteLine($"Создано {orderTypes.Count} типов заказов");
        Console.WriteLine($"Создано {deliveryTypes.Count} типов доставки");
        Console.WriteLine($"Создано {orderStatuses.Count} статусов заказа");
        Console.WriteLine($"Создано {paymentStatuses.Count} статусов оплаты");
        Console.WriteLine($"Создано {paymentTypes.Count} типов оплаты");
        Console.WriteLine($"Создано {orders.Count} заказов\n");
    }
    
    static async Task RunQueries(NormalizedDbContext context)
    {
        Console.WriteLine("Выполнение тестовых запросов...\n");
        
        var results = new List<QueryResult>();
        
        // Запрос 1: Получить все заказы с полной информацией из справочников
        results.Add(await MeasureQuery("1. Все заказы с данными справочников", async () =>
        {
            var query = await context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.DeliveryType)
                .Include(o => o.OrderStatus)
                .Include(o => o.PaymentStatus)
                .Include(o => o.PaymentType)
                .Take(100)
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 2: Фильтрация по статусу с получением данных
        results.Add(await MeasureQuery("2. Фильтр по статусу 'NEW' с данными", async () =>
        {
            var query = await context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.DeliveryType)
                .Include(o => o.OrderStatus)
                .Include(o => o.PaymentStatus)
                .Include(o => o.PaymentType)
                .Where(o => o.OrderStatus.Code == "NEW")
                .Take(100)
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 3: Сложная фильтрация по нескольким справочникам
        results.Add(await MeasureQuery("3. Сложная фильтрация (статус, тип оплаты)", async () =>
        {
            var query = await context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.DeliveryType)
                .Include(o => o.OrderStatus)
                .Include(o => o.PaymentStatus)
                .Include(o => o.PaymentType)
                .Where(o => o.OrderStatus.Code == "PROCESSING" 
                         && o.PaymentType.Code == "CARD"
                         && o.DeliveryType.Code == "COURIER")
                .Take(100)
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 4: Группировка с агрегацией
        results.Add(await MeasureQuery("4. Группировка по статусам с суммой", async () =>
        {
            var query = await context.Orders
                .GroupBy(o => o.OrderStatusId)
                .Select(g => new 
                { 
                    StatusId = g.Key, 
                    Count = g.Count(),
                    TotalSum = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 5: Поиск по номеру заказа
        var orderNumber = context.Orders.Select(o => o.OrderNumber).FirstOrDefault() ?? "";
        results.Add(await MeasureQuery("5. Поиск по номеру заказа", async () =>
        {
            var query = await context.Orders
                .Include(o => o.OrderType)
                .Include(o => o.DeliveryType)
                .Include(o => o.OrderStatus)
                .Include(o => o.PaymentStatus)
                .Include(o => o.PaymentType)
                .Where(o => o.OrderNumber == orderNumber)
                .FirstOrDefaultAsync();
            
            return query != null ? 1 : 0;
        }));
        
        // Запрос 6: Статистика по типам оплаты
        results.Add(await MeasureQuery("6. Статистика по типам оплаты", async () =>
        {
            var query = await context.Orders
                .GroupBy(o => o.PaymentTypeId)
                .Select(g => new 
                { 
                    PaymentTypeId = g.Key, 
                    Count = g.Count(),
                    AvgAmount = g.Average(o => o.TotalAmount)
                })
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Вывод результатов
        Console.WriteLine("\nРезультаты для нормализованной схемы:");
        Console.WriteLine("=" .PadRight(80, '='));
        Console.WriteLine($"{"Запрос",-50} {"Время (мс)",-15} {"Результатов",-10}");
        Console.WriteLine("=" .PadRight(80, '='));
        
        foreach (var result in results)
        {
            Console.WriteLine($"{result.QueryName,-50} {result.ElapsedMs,15:F2} {result.ResultCount,10}");
        }
        Console.WriteLine("=" .PadRight(80, '='));
        
        // Сохраняем результаты для сравнения
        await SaveResults(results, "normalized_results.json");
    }
    
    static async Task<QueryResult> MeasureQuery(string name, Func<Task<int>> queryFunc)
    {
        var sw = Stopwatch.StartNew();
        var resultCount = await queryFunc();
        sw.Stop();
        
        Console.WriteLine($"✓ {name}: {sw.ElapsedMilliseconds} мс");
        return new QueryResult { QueryName = name, ElapsedMs = sw.ElapsedMilliseconds, ResultCount = resultCount };
    }
    
    static async Task SaveResults(List<QueryResult> results, string filename)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(filename, json);
    }
    
    class QueryResult
    {
        public string QueryName { get; set; } = string.Empty;
        public double ElapsedMs { get; set; }
        public int ResultCount { get; set; }
    }
}