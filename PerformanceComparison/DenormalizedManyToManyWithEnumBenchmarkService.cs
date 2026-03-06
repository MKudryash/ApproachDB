// DenormalizedManyToManyWithEnumBenchmarkService.cs
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderSystemComparison.Data;
using DatabaseCommon.Models;
using DatabaseCommon.Models.DenormalizedManyToManyWithEnum;

namespace PerformanceComparison;

public class DenormalizedManyToManyWithEnumBenchmarkService
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
    
    // ЗАПРОС 1: Полные данные заказов (все атрибуты)
    public async Task<int> Query1(DenormalizedManyToManyWithEnumDbContext context)
    {
        var result = await context.Orders
            .Select(o => new
            {
                Order = o,
                OrderType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == DictionaryType.OrderType)
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                DeliveryType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == DictionaryType.DeliveryType)
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                OrderStatus = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == DictionaryType.OrderStatus)
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                PaymentStatus = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == DictionaryType.PaymentStatus)
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault(),
                PaymentType = context.OrderDictionaryValues
                    .Where(ov => ov.OrderId == o.Id && ov.DictionaryType == DictionaryType.PaymentType)
                    .Select(ov => ov.DictionaryEntry)
                    .FirstOrDefault()
            })
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    // ЗАПРОС 2: Фильтр по статусу 'NEW'
    public async Task<int> Query2(DenormalizedManyToManyWithEnumDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == DictionaryType.OrderStatus && de.Code == "NEW"
                           join o in context.Orders on ov.OrderId equals o.Id
                           select o)
                           .Distinct()
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    // ЗАПРОС 3: Сложная фильтрация (PROCESSING + CARD + COURIER)
    public async Task<int> Query3(DenormalizedManyToManyWithEnumDbContext context)
    {
        // Получаем ID заказов, которые соответствуют всем трём условиям
        var orderIds = await (from ov in context.OrderDictionaryValues
                              join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                              where (ov.DictionaryType == DictionaryType.OrderStatus && de.Code == "PROCESSING")
                                 || (ov.DictionaryType == DictionaryType.PaymentType && de.Code == "CARD")
                                 || (ov.DictionaryType == DictionaryType.DeliveryType && de.Code == "COURIER")
                              group ov by ov.OrderId into g
                              where g.Count() == 3  // Все три условия выполнены
                              select g.Key)
                              .Take(100)
                              .ToListAsync();
        
        // Загружаем сами заказы
        var orders = await context.Orders
            .Where(o => orderIds.Contains(o.Id))
            .Take(100)
            .ToListAsync();
        
        return orders.Count;
    }
    
    // ЗАПРОС 4: Группировка по статусам с суммой
    public async Task<int> Query4(DenormalizedManyToManyWithEnumDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == DictionaryType.OrderStatus
                           group new { ov, de } by de.Code into g
                           select new
                           {
                               StatusCode = g.Key,
                               Count = g.Count(),
                               TotalSum = context.Orders
                                   .Where(o => g.Select(x => x.ov.OrderId).Contains(o.Id))
                                   .Sum(o => o.TotalAmount)
                           })
                           .ToListAsync();
        
        return result.Count;
    }
    
    // ЗАПРОС 5: Поиск по номеру заказа
    public async Task<int> Query5(DenormalizedManyToManyWithEnumDbContext context)
    {
        var orderNumber = await context.Orders
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
            
        var result = await context.Orders
            .Include(o => o.DictionaryValues)  
                .ThenInclude(dv => dv.DictionaryEntry)  
            .Where(o => o.OrderNumber == orderNumber)
            .FirstOrDefaultAsync();
        
        return result != null ? 1 : 0;
    }
    
    // ЗАПРОС 6: Статистика по типам оплаты
    public async Task<int> Query6(DenormalizedManyToManyWithEnumDbContext context)
    {
        var result = await (from ov in context.OrderDictionaryValues
                           join de in context.DictionaryEntries on ov.DictionaryEntryId equals de.Id
                           where ov.DictionaryType == DictionaryType.PaymentType
                           group new { ov, de } by de.Code into g
                           select new
                           {
                               PaymentTypeCode = g.Key,
                               Count = g.Count(),
                               AvgAmount = context.Orders
                                   .Where(o => g.Select(x => x.ov.OrderId).Contains(o.Id))
                                   .Average(o => o.TotalAmount)
                           })
                           .ToListAsync();
        
        return result.Count;
    }
    
   
    
   
    // Прогрев
    public async Task RunWarmup(DenormalizedManyToManyWithEnumDbContext context, int warmupIterations)
    {
        for (int i = 0; i < warmupIterations; i++)
        {
            await Query1(context);
            await Query2(context);
            await Query3(context);
            await Task.Delay(50);
        }
    }
}