using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderSystemComparison.Data;
using Spectre.Console;
using Bogus;
using DatabaseCommon.Models;
using DenormalizedManyToMany = DatabaseCommon.Models.DenormalizedModels.DenormalizedManyToMany;
using DenormalizedModels = DatabaseCommon.Models.DenormalizedModels;
using NormalizedModels = DatabaseCommon.Models.NormalizedModels;

namespace PerformanceComparison;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Сравнительный анализ производительности ===\n");

        var host = CreateHostBuilder(args).Build();
        var config = host.Services.GetRequiredService<IConfiguration>();

        var iterations = config.GetValue<int>("Benchmark:Iterations", 3);
        var warmupIterations = config.GetValue<int>("Benchmark:WarmupIterations", 1);

        // Инициализация баз данных
        await InitializeDatabases(host.Services);

        // Выполнение бенчмарков
        var normalizedResults = await RunNormalizedBenchmarks(host.Services, iterations, warmupIterations);
        var denormalizedResults = await RunDenormalizedBenchmarks(host.Services, iterations, warmupIterations);
        var manyToManyResults = await RunDenormalizedManyToManyBenchmarks(host.Services, iterations, warmupIterations);
        var manyToManyWithEnumResults =
            await RunDenormalizedManyToManyWithEnumBenchmarks(host.Services, iterations, warmupIterations);
        var normalizedWithEnumResults = await RunNormalizedWithEnumBenchmarks(host.Services, iterations, warmupIterations);

        // Отображение результатов
        DisplayResults(normalizedResults, denormalizedResults, manyToManyResults, manyToManyWithEnumResults,normalizedWithEnumResults);

        // Сохранение результатов
        await SaveResults(normalizedResults, denormalizedResults, manyToManyResults);
    }

  

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                var normalizedConnection = context.Configuration.GetConnectionString("NormalizedConnection");
                var denormalizedConnection = context.Configuration.GetConnectionString("DenormalizedConnection");
                var manyToManyConnection = context.Configuration.GetConnectionString("ManyToManyConnection");
                var normalizedWithEnumConnection = context.Configuration.GetConnectionString("NormalizedWithEnumConnection");

                services.AddDbContext<NormalizedWithEnumDbContext>(options =>
                    options.UseNpgsql(normalizedWithEnumConnection));

                services.AddSingleton<NormalizedWithEnumBenchmarkService>();

                var manyToManyWithEnumConnection =
                    context.Configuration.GetConnectionString("ManyToManyWithEnumConnection");

                services.AddDbContext<NormalizedDbContext>(options =>
                    options.UseNpgsql(normalizedConnection));

                services.AddDbContext<DenormalizedDbContext>(options =>
                    options.UseNpgsql(denormalizedConnection));

                services.AddDbContext<DenormalizedManyToManyDbContext>(options =>
                    options.UseNpgsql(manyToManyConnection));

                // Добавьте регистрацию для Enum DbContext
                services.AddDbContext<DenormalizedManyToManyWithEnumDbContext>(options =>
                    options.UseNpgsql(manyToManyWithEnumConnection));

                services.AddSingleton<NormalizedBenchmarkService>();
                services.AddSingleton<DenormalizedBenchmarkService>();
                services.AddSingleton<DenormalizedManyToManyBenchmarkService>();

                // Добавьте регистрацию для нового benchmark сервиса
                services.AddSingleton<DenormalizedManyToManyWithEnumBenchmarkService>();
            });

    static async Task InitializeDatabases(IServiceProvider services)
    {
        await AnsiConsole.Status()
            .StartAsync("Инициализация баз данных...", async ctx =>
            {
                using var scope = services.CreateScope();
                var normalizedContext = scope.ServiceProvider.GetRequiredService<NormalizedDbContext>();
                var denormalizedContext = scope.ServiceProvider.GetRequiredService<DenormalizedDbContext>();
                var manyToManyContext = scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyDbContext>();
                var manyToManyWithEnumContext =
                    scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyWithEnumDbContext>();

                // НОРМАЛИЗОВАННАЯ БД
                ctx.Status("Создание нормализованной БД...");
                await InitializeNormalizedDatabase(normalizedContext);

                // ДЕНОРМАЛИЗОВАННАЯ БД (с кодами)
                ctx.Status("Создание денормализованной БД (с кодами)...");
                await InitializeDenormalizedDatabase(denormalizedContext);

                // ДЕНОРМАЛИЗОВАННАЯ БД (многие-ко-многим)
                ctx.Status("Создание денормализованной БД (многие-ко-многим)...");
                await InitializeManyToManyDatabase(manyToManyContext);

                // ДЕНОРМАЛИЗОВАННАЯ БД (многие-ко-многим с Enum)
                ctx.Status("Создание денормализованной БД (многие-ко-многим с Enum)...");
                await InitializeManyToManyWithEnumDatabase(manyToManyWithEnumContext);
                
                var normalizedWithEnumContext = scope.ServiceProvider.GetRequiredService<NormalizedWithEnumDbContext>();
                await InitializeNormalizedWithEnumDatabase(normalizedWithEnumContext);

                ctx.Status("Готово!");
                await Task.Delay(500);
            });
    }

    static async Task InitializeManyToManyWithEnumDatabase(DenormalizedManyToManyWithEnumDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Создаем записи справочника
        var orderTypes = DataGenerator.GenerateOrderTypes();
        var deliveryTypes = DataGenerator.GenerateDeliveryTypes();
        var orderStatuses = DataGenerator.GenerateOrderStatuses();
        var paymentStatuses = DataGenerator.GeneratePaymentStatuses();
        var paymentTypes = DataGenerator.GeneratePaymentTypes();

        // Генерируем записи справочника с enum
        var dictionaryEntries = DataGenerator.GenerateDictionaryEntriesWithEnum(
            orderTypes, deliveryTypes, orderStatuses, paymentStatuses, paymentTypes);

        await context.DictionaryEntries.AddRangeAsync(dictionaryEntries);
        await context.SaveChangesAsync();

        // Получаем записи с ID
        var entriesWithIds = await context.DictionaryEntries.ToListAsync();

        // Группируем по enum типу
        var entriesByType = entriesWithIds
            .GroupBy(e => e.DictionaryType)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Генерируем заказы
        var orders = DataGenerator.GenerateDenormalizedManyToManyWithEnumOrders(entriesWithIds, 1000);

        // Добавляем заказы
        var batchSize = 100;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize).ToList();
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }

        // Генерируем связи
        var orderValues = DataGenerator.GenerateOrderDictionaryValuesWithEnum(orders, entriesWithIds, entriesByType);

        // Добавляем связи
        for (int i = 0; i < orderValues.Count; i += batchSize)
        {
            var batch = orderValues.Skip(i).Take(batchSize).ToList();
            await context.OrderDictionaryValues.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }

    static async Task InitializeNormalizedDatabase(NormalizedDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

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

        var orderTypesWithIds = await context.OrderTypes.ToListAsync();
        var deliveryTypesWithIds = await context.DeliveryTypes.ToListAsync();
        var orderStatusesWithIds = await context.OrderStatuses.ToListAsync();
        var paymentStatusesWithIds = await context.PaymentStatuses.ToListAsync();
        var paymentTypesWithIds = await context.PaymentTypes.ToListAsync();

        var orders = DataGenerator.GenerateNormalizedOrders(
            orderTypesWithIds, deliveryTypesWithIds, orderStatusesWithIds,
            paymentStatusesWithIds, paymentTypesWithIds, 1000);

        var batchSize = 100;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize).ToList();
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }

    static async Task InitializeDenormalizedDatabase(DenormalizedDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var orderTypes = DataGenerator.GenerateOrderTypes();
        var deliveryTypes = DataGenerator.GenerateDeliveryTypes();
        var orderStatuses = DataGenerator.GenerateOrderStatuses();
        var paymentStatuses = DataGenerator.GeneratePaymentStatuses();
        var paymentTypes = DataGenerator.GeneratePaymentTypes();

        var dictionaryEntries = DataGenerator.ConvertToDictionaryEntries(
            orderTypes, deliveryTypes, orderStatuses, paymentStatuses, paymentTypes);
        await context.DictionaryEntries.AddRangeAsync(dictionaryEntries);
        await context.SaveChangesAsync();

        var denormalizedOrders = DataGenerator.GenerateDenormalizedOrders(dictionaryEntries, 1000);

        var batchSize = 100;
        for (int i = 0; i < denormalizedOrders.Count; i += batchSize)
        {
            var batch = denormalizedOrders.Skip(i).Take(batchSize).ToList();
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }

    static async Task InitializeManyToManyDatabase(DenormalizedManyToManyDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Создаем записи справочника
        var orderTypes = DataGenerator.GenerateOrderTypes();
        var deliveryTypes = DataGenerator.GenerateDeliveryTypes();
        var orderStatuses = DataGenerator.GenerateOrderStatuses();
        var paymentStatuses = DataGenerator.GeneratePaymentStatuses();
        var paymentTypes = DataGenerator.GeneratePaymentTypes();

        // Конвертируем в DictionaryEntry для many-to-many схемы
        var dictionaryEntries = new List<DenormalizedManyToMany.DictionaryEntry>();

        foreach (var item in orderTypes)
        {
            dictionaryEntries.Add(new DenormalizedManyToMany.DictionaryEntry
            {
                DictionaryType = "OrderType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"Description\":\"{item.Description}\"}}"
            });
        }

        foreach (var item in deliveryTypes)
        {
            dictionaryEntries.Add(new DenormalizedManyToMany.DictionaryEntry
            {
                DictionaryType = "DeliveryType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"BasePrice\":{item.BasePrice},\"EstimatedDays\":{item.EstimatedDays}}}"
            });
        }

        foreach (var item in orderStatuses)
        {
            dictionaryEntries.Add(new DenormalizedManyToMany.DictionaryEntry
            {
                DictionaryType = "OrderStatus",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"ColorCode\":\"{item.ColorCode}\",\"SortOrder\":{item.SortOrder}}}"
            });
        }

        foreach (var item in paymentStatuses)
        {
            dictionaryEntries.Add(new DenormalizedManyToMany.DictionaryEntry
            {
                DictionaryType = "PaymentStatus",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"IsPaid\":{item.IsPaid.ToString().ToLower()}}}"
            });
        }

        foreach (var item in paymentTypes)
        {
            dictionaryEntries.Add(new DenormalizedManyToMany.DictionaryEntry
            {
                DictionaryType = "PaymentType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"Commission\":{item.Commission}}}"
            });
        }

        await context.DictionaryEntries.AddRangeAsync(dictionaryEntries);
        await context.SaveChangesAsync();

        // Получаем все записи справочника с ID
        var entriesWithIds = await context.DictionaryEntries.ToListAsync();

        // Группируем по типам для удобства
        var entriesByType = entriesWithIds
            .GroupBy(e => e.DictionaryType)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Генерируем заказы
        var orders = DataGenerator.GenerateDenormalizedManyToManyOrders(entriesWithIds, 1000);

        // Добавляем заказы в базу данных
        var batchSize = 100;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize).ToList();
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }

        // Генерируем связи между заказами и справочными значениями
        var orderValues = DataGenerator.GenerateOrderDictionaryValues(orders, entriesWithIds, entriesByType);

        // Добавляем связи в базу данных
        for (int i = 0; i < orderValues.Count; i += batchSize)
        {
            var batch = orderValues.Skip(i).Take(batchSize).ToList();
            await context.OrderDictionaryValues.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }
    
    static async Task InitializeNormalizedWithEnumDatabase(NormalizedWithEnumDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var orders = DataGenerator.GenerateNormalizedWithEnumOrders(1000);

        var batchSize = 100;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize).ToList();
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }

    static async Task<Dictionary<string, BenchmarkResult>> RunNormalizedBenchmarks(
        IServiceProvider services, int iterations, int warmupIterations)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NormalizedDbContext>();
        var benchmarkService = scope.ServiceProvider.GetRequiredService<NormalizedBenchmarkService>();

        return await AnsiConsole.Status()
            .StartAsync("Выполнение бенчмарков нормализованной схемы...", async ctx =>
            {
                var results = new Dictionary<string, BenchmarkResult>();

                ctx.Status("Прогрев...");
                await benchmarkService.RunWarmup(context, warmupIterations);

                ctx.Status("Запрос 1: Все заказы с данными...");
                results["1. Все заказы с данными"] = await benchmarkService.BenchmarkQuery(
                    "1. Все заказы с данными", iterations,
                    async () => await benchmarkService.Query1(context));

                ctx.Status("Запрос 2: Фильтр по статусу...");
                results["2. Фильтр по статусу 'NEW'"] = await benchmarkService.BenchmarkQuery(
                    "2. Фильтр по статусу 'NEW'", iterations,
                    async () => await benchmarkService.Query2(context));

                ctx.Status("Запрос 3: Сложная фильтрация...");
                results["3. Сложная фильтрация"] = await benchmarkService.BenchmarkQuery(
                    "3. Сложная фильтрация", iterations,
                    async () => await benchmarkService.Query3(context));

                ctx.Status("Запрос 4: Группировка...");
                results["4. Группировка по статусам"] = await benchmarkService.BenchmarkQuery(
                    "4. Группировка по статусам", iterations,
                    async () => await benchmarkService.Query4(context));

                ctx.Status("Запрос 5: Поиск по номеру...");
                results["5. Поиск по номеру"] = await benchmarkService.BenchmarkQuery(
                    "5. Поиск по номеру", iterations,
                    async () => await benchmarkService.Query5(context));

                ctx.Status("Запрос 6: Статистика по типам оплаты...");
                results["6. Статистика по типам оплаты"] = await benchmarkService.BenchmarkQuery(
                    "6. Статистика по типам оплаты", iterations,
                    async () => await benchmarkService.Query6(context));

                return results;
            });
    }

    static async Task<Dictionary<string, BenchmarkResult>> RunDenormalizedBenchmarks(
        IServiceProvider services, int iterations, int warmupIterations)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DenormalizedDbContext>();
        var benchmarkService = scope.ServiceProvider.GetRequiredService<DenormalizedBenchmarkService>();

        return await AnsiConsole.Status()
            .StartAsync("Выполнение бенчмарков денормализованной схемы (с кодами)...", async ctx =>
            {
                var results = new Dictionary<string, BenchmarkResult>();

                ctx.Status("Прогрев...");
                await benchmarkService.RunWarmup(context, warmupIterations);

                ctx.Status("Запрос 1: Все заказы с данными...");
                results["1. Все заказы с данными"] = await benchmarkService.BenchmarkQuery(
                    "1. Все заказы с данными", iterations,
                    async () => await benchmarkService.Query1(context));

                ctx.Status("Запрос 2: Фильтр по статусу...");
                results["2. Фильтр по статусу 'NEW'"] = await benchmarkService.BenchmarkQuery(
                    "2. Фильтр по статусу 'NEW'", iterations,
                    async () => await benchmarkService.Query2(context));

                ctx.Status("Запрос 3: Сложная фильтрация...");
                results["3. Сложная фильтрация"] = await benchmarkService.BenchmarkQuery(
                    "3. Сложная фильтрация", iterations,
                    async () => await benchmarkService.Query3(context));

                ctx.Status("Запрос 4: Группировка...");
                results["4. Группировка по статусам"] = await benchmarkService.BenchmarkQuery(
                    "4. Группировка по статусам", iterations,
                    async () => await benchmarkService.Query4(context));

                ctx.Status("Запрос 5: Поиск по номеру...");
                results["5. Поиск по номеру"] = await benchmarkService.BenchmarkQuery(
                    "5. Поиск по номеру", iterations,
                    async () => await benchmarkService.Query5(context));

                ctx.Status("Запрос 6: Статистика по типам оплаты...");
                results["6. Статистика по типам оплаты"] = await benchmarkService.BenchmarkQuery(
                    "6. Статистика по типам оплаты", iterations,
                    async () => await benchmarkService.Query6(context));

                return results;
            });
    }

    static async Task<Dictionary<string, BenchmarkResult>> RunDenormalizedManyToManyBenchmarks(
        IServiceProvider services, int iterations, int warmupIterations)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyDbContext>();
        var benchmarkService = scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyBenchmarkService>();

        return await AnsiConsole.Status()
            .StartAsync("Выполнение бенчмарков денормализованной схемы (многие-ко-многим)...", async ctx =>
            {
                var results = new Dictionary<string, BenchmarkResult>();

                ctx.Status("Прогрев...");
                await benchmarkService.RunWarmup(context, warmupIterations);
                

                ctx.Status("Запрос 1: Полные данные заказов...");
                results["1. Полные данные заказов (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "1. Полные данные заказов (М:М)", iterations,
                    async () => await benchmarkService.Query1(context));

                ctx.Status("Запрос 2: Фильтр по статусу...");
                results["2. Фильтр по статусу 'NEW' (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "2. Фильтр по статусу 'NEW' (М:М)", iterations,
                    async () => await benchmarkService.Query2(context));

                ctx.Status("Запрос 3: Сложная фильтрация...");
                results["3. Сложная фильтрация (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "3. Сложная фильтрация (М:М)", iterations,
                    async () => await benchmarkService.Query3(context));

                ctx.Status("Запрос 4: Группировка...");
                results["4. Группировка по статусам (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "4. Группировка по статусам (М:М)", iterations,
                    async () => await benchmarkService.Query4(context));

                ctx.Status("Запрос 5: Поиск по номеру...");
                results["5. Поиск по номеру (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "5. Поиск по номеру (М:М)", iterations,
                    async () => await benchmarkService.Query5(context));

                ctx.Status("Запрос 6: Статистика по типам оплаты...");
                results["6. Статистика по типам оплаты (М:М)"] = await benchmarkService.BenchmarkQuery(
                    "6. Статистика по типам оплаты (М:М)", iterations,
                    async () => await benchmarkService.Query6(context));

                return results;
            });
    }

    static async Task<Dictionary<string, BenchmarkResult>> RunDenormalizedManyToManyWithEnumBenchmarks(
        IServiceProvider services, int iterations, int warmupIterations)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyWithEnumDbContext>();
        var benchmarkService =
            scope.ServiceProvider.GetRequiredService<DenormalizedManyToManyWithEnumBenchmarkService>();

        return await AnsiConsole.Status()
            .StartAsync("Выполнение бенчмарков денормализованной схемы (многие-ко-многим с Enum)...", async ctx =>
            {
                var results = new Dictionary<string, BenchmarkResult>();

                ctx.Status("Прогрев...");
                await benchmarkService.RunWarmup(context, warmupIterations);

                ctx.Status("Запрос 1: Полные данные заказов...");
                results["1. Полные данные заказов (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "1. Полные данные заказов (М:М Enum)", iterations,
                    async () => await benchmarkService.Query1(context));

                ctx.Status("Запрос 2: Фильтр по статусу...");
                results["2. Фильтр по статусу 'NEW' (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "2. Фильтр по статусу 'NEW' (М:М Enum)", iterations,
                    async () => await benchmarkService.Query2(context));

                ctx.Status("Запрос 3: Сложная фильтрация...");
                results["3. Сложная фильтрация (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "3. Сложная фильтрация (М:М Enum)", iterations,
                    async () => await benchmarkService.Query3(context));

                ctx.Status("Запрос 4: Группировка...");
                results["4. Группировка по статусам (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "4. Группировка по статусам (М:М Enum)", iterations,
                    async () => await benchmarkService.Query4(context));

                ctx.Status("Запрос 5: Поиск по номеру...");
                results["5. Поиск по номеру (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "5. Поиск по номеру (М:М Enum)", iterations,
                    async () => await benchmarkService.Query5(context));

                ctx.Status("Запрос 6: Статистика по типам оплаты...");
                results["6. Статистика по типам оплаты (М:М Enum)"] = await benchmarkService.BenchmarkQuery(
                    "6. Статистика по типам оплаты (М:М Enum)", iterations,
                    async () => await benchmarkService.Query6(context));

                return results;
            });
    }
    private static async Task<Dictionary<string, BenchmarkResult>> RunNormalizedWithEnumBenchmarks(IServiceProvider hostServices, int iterations, int warmupIterations)
    {
       using var scope = hostServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NormalizedWithEnumDbContext>();
        var benchmarkService =
            scope.ServiceProvider.GetRequiredService<NormalizedWithEnumBenchmarkService>();

        return await AnsiConsole.Status()
            .StartAsync("Выполнение бенчмарков нормализованной схемы (многие-ко-многим с Enum)...", async ctx =>
            {
                var results = new Dictionary<string, BenchmarkResult>();

                ctx.Status("Прогрев...");
                await benchmarkService.RunWarmup(context, warmupIterations);

                ctx.Status("Запрос 1: Полные данные заказов...");
                results["1. Полные данные заказов (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "1. Полные данные заказов (Enum)", iterations,
                    async () => await benchmarkService.Query1(context));

                ctx.Status("Запрос 2: Фильтр по статусу...");
                results["2. Фильтр по статусу 'NEW' (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "2. Фильтр по статусу 'NEW' (Enum)", iterations,
                    async () => await benchmarkService.Query2(context));

                ctx.Status("Запрос 3: Сложная фильтрация...");
                results["3. Сложная фильтрация (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "3. Сложная фильтрация (Enum)", iterations,
                    async () => await benchmarkService.Query3(context));

                ctx.Status("Запрос 4: Группировка...");
                results["4. Группировка по статусам (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "4. Группировка по статусам (Enum)", iterations,
                    async () => await benchmarkService.Query4(context));

                ctx.Status("Запрос 5: Поиск по номеру...");
                results["5. Поиск по номеру (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "5. Поиск по номеру (Enum)", iterations,
                    async () => await benchmarkService.Query5(context));

                ctx.Status("Запрос 6: Статистика по типам оплаты...");
                results["6. Статистика по типам оплаты (Enum)"] = await benchmarkService.BenchmarkQuery(
                    "6. Статистика по типам оплаты (Enum)", iterations,
                    async () => await benchmarkService.Query6(context));

                return results;
            });
    }
    static void DisplayResults(
        Dictionary<string, BenchmarkResult> normalizedResults,
        Dictionary<string, BenchmarkResult> denormalizedResults,
        Dictionary<string, BenchmarkResult> manyToManyResults,
        Dictionary<string, BenchmarkResult> manyToManyWithEnumResults,
        Dictionary<string, BenchmarkResult> manyToManyWithEnumWithEnumResults)
    {
        var table = new Table();
        table.Title = new TableTitle("Сравнение производительности (время в миллисекундах)");
        table.AddColumn("Запрос");
        table.AddColumn("Нормализ.");
        table.AddColumn("Денормал. (коды)");
        table.AddColumn("Денормал. (М:М)");
        table.AddColumn("Денормал. (М:М Enum)");
        table.AddColumn("Нормализ. (Enum)");

        // Сопоставление запросов
        var queryMapping = new Dictionary<string, (string norm, string denorm, string mtm, string mtmEnum, string nEnum)>
        {
            ["Все заказы"] = (
                "1. Все заказы с данными",
                "1. Все заказы с данными",
                "1. Полные данные заказов (М:М)",
                "1. Полные данные заказов (М:М Enum)",
                "1. Полные данные заказов (Enum)"
                ),
            ["Фильтр по статусу"] = (
                "2. Фильтр по статусу 'NEW'",
                "2. Фильтр по статусу 'NEW'",
                "2. Фильтр по статусу 'NEW' (М:М)",
                "2. Фильтр по статусу 'NEW' (М:М Enum)",
                "2. Фильтр по статусу 'NEW' (Enum)"),
            ["Сложная фильтрация"] = (
                "3. Сложная фильтрация",
                "3. Сложная фильтрация",
                "3. Сложная фильтрация (М:М)",
                "3. Сложная фильтрация (М:М Enum)",
                "3. Сложная фильтрация (Enum)"),
            ["Группировка"] = (
                "4. Группировка по статусам",
                "4. Группировка по статусам",
                "4. Группировка по статусам (М:М)",
                "4. Группировка по статусам (М:М Enum)",
                "4. Группировка по статусам (Enum)"),
            ["Поиск по номеру"] = (
                "5. Поиск по номеру",
                "5. Поиск по номеру",
                "5. Поиск по номеру (М:М)",
                "5. Поиск по номеру (М:М Enum)",
                "5. Поиск по номеру (Enum)"),
            ["Статистика по оплате"] = (
                "6. Статистика по типам оплаты",
                "6. Статистика по типам оплаты",
                "6. Статистика по типам оплаты (М:М)",
                "6. Статистика по типам оплаты (М:М Enum)",
                "6. Статистика по типам оплаты (Enum)")
        };

        foreach (var mapping in queryMapping)
        {
            string normValue = GetResultValue(normalizedResults, mapping.Value.norm);
            string denormValue = GetResultValue(denormalizedResults, mapping.Value.denorm);
            string mtmValue = GetResultValue(manyToManyResults, mapping.Value.mtm);
            string mtmEnumValue = GetResultValue(manyToManyWithEnumResults, mapping.Value.mtmEnum);
            string nornEnumValue = GetResultValue(manyToManyWithEnumWithEnumResults, mapping.Value.nEnum);

            table.AddRow(
                mapping.Key,
                normValue,
                denormValue,
                mtmValue,
                mtmEnumValue,
                nornEnumValue
            );
        }

        AnsiConsole.Write(table);
    }

    static string GetResultValue(Dictionary<string, BenchmarkResult> results, string key)
    {
        return results.ContainsKey(key) ? $"{results[key].AverageMs:F2} ms" : "—";
    }

    static async Task SaveResults(
        Dictionary<string, BenchmarkResult> normalizedResults,
        Dictionary<string, BenchmarkResult> denormalizedResults,
        Dictionary<string, BenchmarkResult> manyToManyResults)
    {
        var comparison = new
        {
            Timestamp = DateTime.Now,
            Normalized = normalizedResults,
            Denormalized = denormalizedResults,
            ManyToMany = manyToManyResults,
            Summary = new
            {
                NormalizedAverage = normalizedResults.Values.Any()
                    ? normalizedResults.Values.Average(r => r.AverageMs)
                    : 0,
                DenormalizedAverage = denormalizedResults.Values.Any()
                    ? denormalizedResults.Values.Average(r => r.AverageMs)
                    : 0,
                ManyToManyAverage = manyToManyResults.Values.Any()
                    ? manyToManyResults.Values.Average(r => r.AverageMs)
                    : 0
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(comparison, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var filename = $"benchmark_results_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        await File.WriteAllTextAsync(filename, json);

        Console.WriteLine($"\nРезультаты сохранены в {filename}");
    }
}

public class BenchmarkResult
{
    public string QueryName { get; set; } = string.Empty;
    public double AverageMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double StdDevMs { get; set; }
    public int ResultCount { get; set; }
    public int Iterations { get; set; }
}