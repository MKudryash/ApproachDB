using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderSystemComparison.Data;

namespace PerformanceComparison;

public class DenormalizedManyToManyBenchmarkService
{
    public async Task<BenchmarkResult> BenchmarkQuery(string name, int iterations, Func<Task<int>> queryFunc)
    {
        var times = new List<double>();
        int resultCount = 0;
        
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            resultCount = await queryFunc();
            sw.Stop();
            
            times.Add(sw.Elapsed.TotalMilliseconds);
            await Task.Delay(100);
        }
        
        return new BenchmarkResult
        {
            QueryName = name,
            AverageMs = times.Average(),
            MinMs = times.Min(),
            MaxMs = times.Max(),
            StdDevMs = CalculateStdDev(times),
            ResultCount = resultCount,
            Iterations = iterations
        };
    }
    
    private double CalculateStdDev(List<double> values)
    {
        var avg = values.Average();
        var sum = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sum / values.Count);
    }
    
    // Запрос 1: Получить все заказы с полной информацией (требуется 5 JOIN)
    public async Task<int> Query1(DenormalizedManyToManyDbContext context)
    {
        var result = await (from o in context.Orders
                           join ov in context.OrderDictionaryValues on o.Id equals ov.OrderId
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == "OrderType"
                           select new { o, de })
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    // Запрос 2: Получить все заказы со всеми атрибутами (сложный)
    public async Task<int> QueryFullOrders(DenormalizedManyToManyDbContext context)
    {
        var result = await context.Orders
            .Select(o => new
            {
                Order = o,
                OrderType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == "OrderType")
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                DeliveryType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == "DeliveryType")
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                OrderStatus = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == "OrderStatus")
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                PaymentStatus = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == "PaymentStatus")
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                PaymentType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == "PaymentType")
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault()
            })
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    // Запрос 3: Фильтр по статусу заказа
    public async Task<int> Query2(DenormalizedManyToManyDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == "OrderStatus" && de.Code == "NEW"
                           join o in context.Orders on ov.OrderId equals o.Id
                           select o)
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    // Запрос 4: Сложная фильтрация
    public async Task<int> Query3(DenormalizedManyToManyDbContext context)
    {
        var orderIds = await (from ov in context.OrderDictionaryValues
                              join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                              where (ov.DictionaryType == "OrderStatus" && de.Code == "PROCESSING")
                                 || (ov.DictionaryType == "PaymentType" && de.Code == "CARD")
                                 || (ov.DictionaryType == "DeliveryType" && de.Code == "COURIER")
                              group ov by ov.OrderId into g
                              where g.Count() == 3
                              select g.Key)
                              .Take(100)
                              .ToListAsync();
        
        return orderIds.Count;
    }
    
    // Запрос 5: Группировка по статусам
    public async Task<int> Query4(DenormalizedManyToManyDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == "OrderStatus"
                           group ov by de.Code into g
                           select new
                           {
                               StatusCode = g.Key,
                               Count = g.Count(),
                               TotalSum = context.Orders
                                   .Where(o => g.Select(x => x.OrderId).Contains(o.Id))
                                   .Sum(o => o.TotalAmount)
                           })
                           .ToListAsync();
        
        return result.Count;
    }
    
    // Запрос 6: Поиск по номеру заказа
    public async Task<int> Query5(DenormalizedManyToManyDbContext context)
    {
        var orderNumber = await context.Orders
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
            
        var result = await context.Orders
            .Where(o => o.OrderNumber == orderNumber)
            .FirstOrDefaultAsync();
        
        return result != null ? 1 : 0;
    }
    
    // Запрос 7: Статистика по типам оплаты
    public async Task<int> Query6(DenormalizedManyToManyDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == "PaymentType"
                           group ov by de.Code into g
                           select new
                           {
                               PaymentTypeCode = g.Key,
                               Count = g.Count(),
                               AvgAmount = context.Orders
                                   .Where(o => g.Select(x => x.OrderId).Contains(o.Id))
                                   .Average(o => o.TotalAmount)
                           })
                           .ToListAsync();
        
        return result.Count;
    }
    
    public async Task RunWarmup(DenormalizedManyToManyDbContext context, int warmupIterations)
    {
        for (int i = 0; i < warmupIterations; i++)
        {
            await Query1(context);
            await Query2(context);
            await Task.Delay(50);
        }
    }
}