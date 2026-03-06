using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderSystemComparison.Data;

namespace PerformanceComparison;

public class DenormalizedBenchmarkService
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
    
    // ДЕНОРМАЛИЗОВАННЫЕ ЗАПРОСЫ
    public async Task<int> Query1(DenormalizedDbContext context)
    {
        var result = await (from o in context.Orders
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
                           orderby o.Id
                           select new { o, ot, dt, os, ps, pt })
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query2(DenormalizedDbContext context)
    {
        var result = await (from o in context.Orders
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
                           orderby o.Id
                           select new { o, ot, dt, os, ps, pt })
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query3(DenormalizedDbContext context)
    {
        var result = await (from o in context.Orders
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
                           orderby o.Id
                           select new { o, ot, dt, os, ps, pt })
                           .Take(100)
                           .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query4(DenormalizedDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.OrderStatusCode)
            .Select(g => new 
            { 
                StatusCode = g.Key, 
                Count = g.Count(),
                TotalSum = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query5(DenormalizedDbContext context)
    {
        var orderNumber = await context.Orders
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
            
        var result = await context.Orders
            .Where(o => o.OrderNumber == orderNumber)
            .FirstOrDefaultAsync();
        
        return result != null ? 1 : 0;
    }
    
    public async Task<int> Query6(DenormalizedDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.PaymentTypeCode)
            .Select(g => new 
            { 
                PaymentTypeCode = g.Key, 
                Count = g.Count(),
                AvgAmount = g.Average(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task RunWarmup(DenormalizedDbContext context, int warmupIterations)
    {
        for (int i = 0; i < warmupIterations; i++)
        {
            await Query1(context);
            await Query2(context);
            await Task.Delay(50);
        }
    }
}

