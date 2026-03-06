using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderSystemComparison.Data;

namespace PerformanceComparison;

public class NormalizedBenchmarkService
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
    
    // НОРМАЛИЗОВАННЫЕ ЗАПРОСЫ
    public async Task<int> Query1(NormalizedDbContext context)
    {
        var result = await context.Orders
            .Include(o => o.OrderType)
            .Include(o => o.DeliveryType)
            .Include(o => o.OrderStatus)
            .Include(o => o.PaymentStatus)
            .Include(o => o.PaymentType)
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query2(NormalizedDbContext context)
    {
        var result = await context.Orders
            .Include(o => o.OrderType)
            .Include(o => o.DeliveryType)
            .Include(o => o.OrderStatus)
            .Include(o => o.PaymentStatus)
            .Include(o => o.PaymentType)
            .Where(o => o.OrderStatus.Code == "NEW")
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query3(NormalizedDbContext context)
    {
        var result = await context.Orders
            .Include(o => o.OrderType)
            .Include(o => o.DeliveryType)
            .Include(o => o.OrderStatus)
            .Include(o => o.PaymentStatus)
            .Include(o => o.PaymentType)
            .Where(o => o.OrderStatus.Code == "PROCESSING" 
                     && o.PaymentType.Code == "CARD"
                     && o.DeliveryType.Code == "COURIER")
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query4(NormalizedDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.OrderStatusId)
            .Select(g => new 
            { 
                StatusId = g.Key, 
                Count = g.Count(),
                TotalSum = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query5(NormalizedDbContext context)
    {
        var orderNumber = await context.Orders
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
            
        var result = await context.Orders
            .Include(o => o.OrderType)
            .Include(o => o.DeliveryType)
            .Include(o => o.OrderStatus)
            .Include(o => o.PaymentStatus)
            .Include(o => o.PaymentType)
            .Where(o => o.OrderNumber == orderNumber)
            .FirstOrDefaultAsync();
        
        return result != null ? 1 : 0;
    }
    
    public async Task<int> Query6(NormalizedDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.PaymentTypeId)
            .Select(g => new 
            { 
                PaymentTypeId = g.Key, 
                Count = g.Count(),
                AvgAmount = g.Average(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task RunWarmup(NormalizedDbContext context, int warmupIterations)
    {
        for (int i = 0; i < warmupIterations; i++)
        {
            await Query1(context);
            await Query2(context);
            await Task.Delay(50);
        }
    }
}