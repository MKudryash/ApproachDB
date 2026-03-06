// NormalizedWithEnumBenchmarkService.cs
using System.Diagnostics;
using DatabaseCommon.Models.NormalizedWithEnumModels;
using Microsoft.EntityFrameworkCore;
using OrderSystemComparison.Data;

namespace PerformanceComparison;

public class NormalizedWithEnumBenchmarkService
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
    
    public async Task<int> Query1(NormalizedWithEnumDbContext context)
    {
        var result = await context.Orders
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query2(NormalizedWithEnumDbContext context)
    {
        var result = await context.Orders
            .Where(o => o.OrderStatus == OrderStatusEnum.New)
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query3(NormalizedWithEnumDbContext context)
    {
        var result = await context.Orders
            .Where(o => o.OrderStatus == OrderStatusEnum.Processing 
                     && o.PaymentType == PaymentTypeEnum.Card
                     && o.DeliveryType == DeliveryTypeEnum.Courier)
            .OrderBy(o => o.Id)
            .Take(100)
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query4(NormalizedWithEnumDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.OrderStatus)
            .Select(g => new 
            { 
                Status = g.Key, 
                Count = g.Count(),
                TotalSum = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task<int> Query5(NormalizedWithEnumDbContext context)
    {
        var orderNumber = await context.Orders
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
            
        var result = await context.Orders
            .Where(o => o.OrderNumber == orderNumber)
            .FirstOrDefaultAsync();
        
        return result != null ? 1 : 0;
    }
    
    public async Task<int> Query6(NormalizedWithEnumDbContext context)
    {
        var result = await context.Orders
            .GroupBy(o => o.PaymentType)
            .Select(g => new 
            { 
                PaymentType = g.Key, 
                Count = g.Count(),
                AvgAmount = g.Average(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return result.Count;
    }
    
    public async Task RunWarmup(NormalizedWithEnumDbContext context, int warmupIterations)
    {
        for (int i = 0; i < warmupIterations; i++)
        {
            await Query1(context);
            await Query2(context);
            await Task.Delay(50);
        }
    }
}