using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OrderSystemComparison.Data;

namespace OrderSystemComparison.Denormalized;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Денормализованная схема (единый справочник) ===\n");
        
        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DenormalizedDbContext>();
        
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
                services.AddDbContext<DenormalizedDbContext>(options =>
                    options.UseNpgsql(connectionString));
            });
    
    static async Task InitializeDatabase(DenormalizedDbContext context)
    {
        Console.WriteLine("Инициализация базы данных...");
        
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        // Сначала создаем нормализованные справочники для генерации данных
        var orderTypes = DataGenerator.GenerateOrderTypes();
        var deliveryTypes = DataGenerator.GenerateDeliveryTypes();
        var orderStatuses = DataGenerator.GenerateOrderStatuses();
        var paymentStatuses = DataGenerator.GeneratePaymentStatuses();
        var paymentTypes = DataGenerator.GeneratePaymentTypes();
        
        // Конвертируем в денормализованные записи словаря
        var dictionaryEntries = DataGenerator.ConvertToDictionaryEntries(
            orderTypes, deliveryTypes, orderStatuses, paymentStatuses, paymentTypes);
        
        await context.DictionaryEntries.AddRangeAsync(dictionaryEntries);
        await context.SaveChangesAsync();
        
        // Генерация заказов
        var orders = DataGenerator.GenerateDenormalizedOrders(dictionaryEntries, 10000);
        
        await context.Orders.AddRangeAsync(orders);
        await context.SaveChangesAsync();
        
        Console.WriteLine($"Создано {dictionaryEntries.Count} записей в едином справочнике");
        Console.WriteLine($"Создано {orders.Count} заказов\n");
    }
    
    static async Task RunQueries(DenormalizedDbContext context)
    {
        Console.WriteLine("Выполнение тестовых запросов...\n");
        
        var results = new List<QueryResult>();
        
        // Запрос 1: Получить все заказы с полной информацией (приходится делать join со словарем для каждого поля)
        results.Add(await MeasureQuery("1. Все заказы с данными справочников", async () =>
        {
            var query = await (from o in context.Orders
                               join ot in context.DictionaryEntries on o.OrderTypeCode equals ot.Code into otGroup
                               from ot in otGroup.Where(x => x.DictionaryType == "OrderType").DefaultIfEmpty()
                               join dt in context.DictionaryEntries on o.DeliveryTypeCode equals dt.Code into dtGroup
                               from dt in dtGroup.Where(x => x.DictionaryType == "DeliveryType").DefaultIfEmpty()
                               join os in context.DictionaryEntries on o.OrderStatusCode equals os.Code into osGroup
                               from os in osGroup.Where(x => x.DictionaryType == "OrderStatus").DefaultIfEmpty()
                               join ps in context.DictionaryEntries on o.PaymentStatusCode equals ps.Code into psGroup
                               from ps in psGroup.Where(x => x.DictionaryType == "PaymentStatus").DefaultIfEmpty()
                               join pt in context.DictionaryEntries on o.PaymentTypeCode equals pt.Code into ptGroup
                               from pt in ptGroup.Where(x => x.DictionaryType == "PaymentType").DefaultIfEmpty()
                               select new { o, ot, dt, os, ps, pt })
                               .Take(100)
                               .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 2: Фильтрация по статусу с получением данных
        results.Add(await MeasureQuery("2. Фильтр по статусу 'NEW' с данными", async () =>
        {
            var query = await (from o in context.Orders
                               join ot in context.DictionaryEntries on o.OrderTypeCode equals ot.Code into otGroup
                               from ot in otGroup.Where(x => x.DictionaryType == "OrderType").DefaultIfEmpty()
                               join dt in context.DictionaryEntries on o.DeliveryTypeCode equals dt.Code into dtGroup
                               from dt in dtGroup.Where(x => x.DictionaryType == "DeliveryType").DefaultIfEmpty()
                               join os in context.DictionaryEntries on o.OrderStatusCode equals os.Code into osGroup
                               from os in osGroup.Where(x => x.DictionaryType == "OrderStatus").DefaultIfEmpty()
                               join ps in context.DictionaryEntries on o.PaymentStatusCode equals ps.Code into psGroup
                               from ps in psGroup.Where(x => x.DictionaryType == "PaymentStatus").DefaultIfEmpty()
                               join pt in context.DictionaryEntries on o.PaymentTypeCode equals pt.Code into ptGroup
                               from pt in ptGroup.Where(x => x.DictionaryType == "PaymentType").DefaultIfEmpty()
                               where o.OrderStatusCode == "NEW"
                               select new { o, ot, dt, os, ps, pt })
                               .Take(100)
                               .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 3: Сложная фильтрация по нескольким справочникам
        results.Add(await MeasureQuery("3. Сложная фильтрация (статус, тип оплаты)", async () =>
        {
            var query = await (from o in context.Orders
                               join ot in context.DictionaryEntries on o.OrderTypeCode equals ot.Code into otGroup
                               from ot in otGroup.Where(x => x.DictionaryType == "OrderType").DefaultIfEmpty()
                               join dt in context.DictionaryEntries on o.DeliveryTypeCode equals dt.Code into dtGroup
                               from dt in dtGroup.Where(x => x.DictionaryType == "DeliveryType").DefaultIfEmpty()
                               join os in context.DictionaryEntries on o.OrderStatusCode equals os.Code into osGroup
                               from os in osGroup.Where(x => x.DictionaryType == "OrderStatus").DefaultIfEmpty()
                               join ps in context.DictionaryEntries on o.PaymentStatusCode equals ps.Code into psGroup
                               from ps in psGroup.Where(x => x.DictionaryType == "PaymentStatus").DefaultIfEmpty()
                               join pt in context.DictionaryEntries on o.PaymentTypeCode equals pt.Code into ptGroup
                               from pt in ptGroup.Where(x => x.DictionaryType == "PaymentType").DefaultIfEmpty()
                               where o.OrderStatusCode == "PROCESSING" 
                                     && o.PaymentTypeCode == "CARD"
                                     && o.DeliveryTypeCode == "COURIER"
                               select new { o, ot, dt, os, ps, pt })
                               .Take(100)
                               .ToListAsync();
            
            return query.Count;
        }));
        
        // Запрос 4: Группировка с агрегацией (сложно, т.к. нет внешних ключей)
        results.Add(await MeasureQuery("4. Группировка по статусам с суммой", async () =>
        {
            var query = await context.Orders
                .GroupBy(o => o.OrderStatusCode)
                .Select(g => new 
                { 
                    StatusCode = g.Key, 
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
                .Where(o => o.OrderNumber == orderNumber)
                .FirstOrDefaultAsync();
            
            return query != null ? 1 : 0;
        }));
        
        // Запрос 6: Статистика по типам оплаты
        results.Add(await MeasureQuery("6. Статистика по типам оплаты", async () =>
        {
            var query = await context.Orders
                .GroupBy(o => o.PaymentTypeCode)
                .Select(g => new 
                { 
                    PaymentTypeCode = g.Key, 
                    Count = g.Count(),
                    AvgAmount = g.Average(o => o.TotalAmount)
                })
                .ToListAsync();
            
            return query.Count;
        }));
        
        // Вывод результатов
        Console.WriteLine("\nРезультаты для денормализованной схемы:");
        Console.WriteLine("=" .PadRight(80, '='));
        Console.WriteLine($"{"Запрос",-50} {"Время (мс)",-15} {"Результатов",-10}");
        Console.WriteLine("=" .PadRight(80, '='));
        
        foreach (var result in results)
        {
            Console.WriteLine($"{result.QueryName,-50} {result.ElapsedMs,15:F2} {result.ResultCount,10}");
        }
        Console.WriteLine("=" .PadRight(80, '='));
        
        // Сохраняем результаты для сравнения
        await SaveResults(results, "denormalized_results.json");
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
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions 
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