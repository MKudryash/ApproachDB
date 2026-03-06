using Bogus;
using DatabaseCommon.Models;
using DenormalizedManyToMany = DatabaseCommon.Models.DenormalizedModels.DenormalizedManyToMany;
using DenormalizedModels = DatabaseCommon.Models.DenormalizedModels;
using NormalizedModels = DatabaseCommon.Models.NormalizedModels;
namespace OrderSystemComparison.Data;

public static class DataGenerator
{
    public static List<NormalizedModels.OrderType> GenerateOrderTypes()
    {
        return new List<NormalizedModels.OrderType>
        {
            new() { Code = "STANDARD", Name = "Стандартный заказ", Description = "Обычный заказ" },
            new() { Code = "PREORDER", Name = "Предзаказ", Description = "Заказ товара под заказ" },
            new() { Code = "EXPRESS", Name = "Срочный заказ", Description = "Заказ срочной доставки" },
            new() { Code = "WHOLESALE", Name = "Оптовый заказ", Description = "Оптовая продажа" },
            new() { Code = "GIFT", Name = "Подарочный", Description = "Подарочный заказ" }
        };
    }
    
    public static List<NormalizedModels.DeliveryType> GenerateDeliveryTypes()
    {
        return new List<NormalizedModels.DeliveryType>
        {
            new() { Code = "COURIER", Name = "Курьер", BasePrice = 300, EstimatedDays = 1 },
            new() { Code = "PICKUP", Name = "Самовывоз", BasePrice = 0, EstimatedDays = 1 },
            new() { Code = "POST", Name = "Почта", BasePrice = 200, EstimatedDays = 7 },
            new() { Code = "EXPRESS", Name = "Экспресс-доставка", BasePrice = 500, EstimatedDays = 1 },
            new() { Code = "INTERNATIONAL", Name = "Международная", BasePrice = 1500, EstimatedDays = 14 }
        };
    }
    
    public static List<NormalizedModels.OrderStatus> GenerateOrderStatuses()
    {
        return new List<NormalizedModels.OrderStatus>
        {
            new() { Code = "NEW", Name = "Новый", ColorCode = "#3498db", SortOrder = 10 },
            new() { Code = "PROCESSING", Name = "В обработке", ColorCode = "#f39c12", SortOrder = 20 },
            new() { Code = "CONFIRMED", Name = "Подтвержден", ColorCode = "#2ecc71", SortOrder = 30 },
            new() { Code = "SHIPPED", Name = "Отправлен", ColorCode = "#9b59b6", SortOrder = 40 },
            new() { Code = "DELIVERED", Name = "Доставлен", ColorCode = "#27ae60", SortOrder = 50 },
            new() { Code = "CANCELLED", Name = "Отменен", ColorCode = "#e74c3c", SortOrder = 60 },
            new() { Code = "RETURNED", Name = "Возврат", ColorCode = "#e67e22", SortOrder = 70 }
        };
    }
    
    public static List<NormalizedModels.PaymentStatus> GeneratePaymentStatuses()
    {
        return new List<NormalizedModels.PaymentStatus>
        {
            new() { Code = "PENDING", Name = "Ожидает оплаты", IsPaid = false },
            new() { Code = "PAID", Name = "Оплачен", IsPaid = true },
            new() { Code = "PARTIALLY_PAID", Name = "Частично оплачен", IsPaid = false },
            new() { Code = "REFUNDED", Name = "Возвращен", IsPaid = false },
            new() { Code = "FAILED", Name = "Ошибка оплаты", IsPaid = false }
        };
    }
    
    public static List<NormalizedModels.PaymentType> GeneratePaymentTypes()
    {
        return new List<NormalizedModels.PaymentType>
        {
            new() { Code = "CARD", Name = "Банковская карта", Commission = 1.5m },
            new() { Code = "CASH", Name = "Наличные", Commission = 0 },
            new() { Code = "ONLINE", Name = "Онлайн-кошелек", Commission = 1.0m },
            new() { Code = "INVOICE", Name = "Счет", Commission = 0 },
            new() { Code = "CRYPTO", Name = "Криптовалюта", Commission = 0.5m }
        };
    }
    
    public static List<DenormalizedModels.DictionaryEntry> ConvertToDictionaryEntries(
        List<NormalizedModels.OrderType> orderTypes,
        List<NormalizedModels.DeliveryType> deliveryTypes,
        List<NormalizedModels.OrderStatus> orderStatuses,
        List<NormalizedModels.PaymentStatus> paymentStatuses,
        List<NormalizedModels.PaymentType> paymentTypes)
    {
        var entries = new List<DenormalizedModels.DictionaryEntry>();
        
        foreach (var item in orderTypes)
        {
            entries.Add(new DenormalizedModels.DictionaryEntry
            {
                DictionaryType = "OrderType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"Description\":\"{item.Description}\"}}"
            });
        }
        
        foreach (var item in deliveryTypes)
        {
            entries.Add(new DenormalizedModels.DictionaryEntry
            {
                DictionaryType = "DeliveryType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"BasePrice\":{item.BasePrice},\"EstimatedDays\":{item.EstimatedDays}}}"
            });
        }
        
        foreach (var item in orderStatuses)
        {
            entries.Add(new DenormalizedModels.DictionaryEntry
            {
                DictionaryType = "OrderStatus",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"ColorCode\":\"{item.ColorCode}\",\"SortOrder\":{item.SortOrder}}}"
            });
        }
        
        foreach (var item in paymentStatuses)
        {
            entries.Add(new DenormalizedModels.DictionaryEntry
            {
                DictionaryType = "PaymentStatus",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"IsPaid\":{item.IsPaid.ToString().ToLower()}}}"
            });
        }
        
        foreach (var item in paymentTypes)
        {
            entries.Add(new DenormalizedModels.DictionaryEntry
            {
                DictionaryType = "PaymentType",
                Code = item.Code,
                Value = item.Name,
                AdditionalData = $"{{\"Commission\":{item.Commission}}}"
            });
        }
        
        return entries;
    }
    
    public static List<NormalizedModels.Order> GenerateNormalizedOrders(
        List<NormalizedModels.OrderType> orderTypes,
        List<NormalizedModels.DeliveryType> deliveryTypes,
        List<NormalizedModels.OrderStatus> orderStatuses,
        List<NormalizedModels.PaymentStatus> paymentStatuses,
        List<NormalizedModels.PaymentType> paymentTypes,
        int count)
    {
        var faker = new Faker();
        var orders = new List<NormalizedModels.Order>();
        var random = new Random();
        
        // Устанавливаем фиксированную дату для всех заказов, чтобы избежать проблем с часовыми поясами
        var baseDate = DateTime.UtcNow.AddYears(-1);
        
        for (int i = 0; i < count; i++)
        {
            // Генерируем дату в UTC
            var orderDate = baseDate.AddDays(random.Next(0, 365));
            
            orders.Add(new NormalizedModels.Order
            {
                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{i:D6}",
                OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc), // Явно указываем UTC
                TotalAmount = Math.Round(faker.Random.Decimal(100, 10000), 2),
                CustomerComment = faker.Random.Bool(0.3f) ? faker.Lorem.Sentence() : null,
                OrderTypeId = faker.PickRandom(orderTypes).Id,
                DeliveryTypeId = faker.PickRandom(deliveryTypes).Id,
                OrderStatusId = faker.PickRandom(orderStatuses).Id,
                PaymentStatusId = faker.PickRandom(paymentStatuses).Id,
                PaymentTypeId = faker.PickRandom(paymentTypes).Id
            });
        }
        
        return orders;
    }
    
public static List<DenormalizedModels.Order> GenerateDenormalizedOrders(
        List<DenormalizedModels.DictionaryEntry> dictionaryEntries,
        int count)
    {
        var orderTypes = dictionaryEntries.Where(e => e.DictionaryType == "OrderType").ToList();
        var deliveryTypes = dictionaryEntries.Where(e => e.DictionaryType == "DeliveryType").ToList();
        var orderStatuses = dictionaryEntries.Where(e => e.DictionaryType == "OrderStatus").ToList();
        var paymentStatuses = dictionaryEntries.Where(e => e.DictionaryType == "PaymentStatus").ToList();
        var paymentTypes = dictionaryEntries.Where(e => e.DictionaryType == "PaymentType").ToList();
        
        var faker = new Faker();
        var orders = new List<DenormalizedModels.Order>();
        var random = new Random();
        
        // Устанавливаем фиксированную дату для всех заказов
        var baseDate = DateTime.UtcNow.AddYears(-1);
        
        for (int i = 0; i < count; i++)
        {
            var orderType = faker.PickRandom(orderTypes);
            var deliveryType = faker.PickRandom(deliveryTypes);
            var orderStatus = faker.PickRandom(orderStatuses);
            var paymentStatus = faker.PickRandom(paymentStatuses);
            var paymentType = faker.PickRandom(paymentTypes);
            
            // Генерируем дату в UTC
            var orderDate = baseDate.AddDays(random.Next(0, 365));
            
            orders.Add(new DenormalizedModels.Order()
            {
                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{i:D6}",
                OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc), // Явно указываем UTC
                TotalAmount = Math.Round(faker.Random.Decimal(100, 10000), 2),
                CustomerComment = faker.Random.Bool(0.3f) ? faker.Lorem.Sentence() : null,
                OrderTypeCode = orderType.Code,
                DeliveryTypeCode = deliveryType.Code,
                OrderStatusCode = orderStatus.Code,
                PaymentStatusCode = paymentStatus.Code,
                PaymentTypeCode = paymentType.Code,
                OrderTypeName = orderType.Value,
                DeliveryTypeName = deliveryType.Value,
                OrderStatusName = orderStatus.Value,
                PaymentStatusName = paymentStatus.Value,
                PaymentTypeName = paymentType.Value
            });
        }
        
        return orders;
    }


public static List<DenormalizedManyToMany.OrderDictionaryValue> GenerateOrderDictionaryValues(
    List<DenormalizedManyToMany.Order> orders,
    List<DenormalizedManyToMany.DictionaryEntry> dictionaryEntries,
    Dictionary<string, List<DenormalizedManyToMany.DictionaryEntry>> entriesByType)
{
    var orderValues = new List<DenormalizedManyToMany.OrderDictionaryValue>();
    var random = new Random();
    
    foreach (var order in orders)
    {
        // Для каждого заказа добавляем по одному значению каждого типа
        // Тип заказа
        var orderType = entriesByType["OrderType"][random.Next(entriesByType["OrderType"].Count)];
        orderValues.Add(new DenormalizedManyToMany.OrderDictionaryValue
        {
            OrderId = order.Id,
            DictionaryEntryId = orderType.Id,
            DictionaryType = "OrderType"
        });
        
        // Тип доставки
        var deliveryType = entriesByType["DeliveryType"][random.Next(entriesByType["DeliveryType"].Count)];
        orderValues.Add(new DenormalizedManyToMany.OrderDictionaryValue
        {
            OrderId = order.Id,
            DictionaryEntryId = deliveryType.Id,
            DictionaryType = "DeliveryType"
        });
        
        // Статус заказа
        var orderStatus = entriesByType["OrderStatus"][random.Next(entriesByType["OrderStatus"].Count)];
        orderValues.Add(new DenormalizedManyToMany.OrderDictionaryValue
        {
            OrderId = order.Id,
            DictionaryEntryId = orderStatus.Id,
            DictionaryType = "OrderStatus"
        });
        
        // Статус оплаты
        var paymentStatus = entriesByType["PaymentStatus"][random.Next(entriesByType["PaymentStatus"].Count)];
        orderValues.Add(new DenormalizedManyToMany.OrderDictionaryValue
        {
            OrderId = order.Id,
            DictionaryEntryId = paymentStatus.Id,
            DictionaryType = "PaymentStatus"
        });
        
        // Тип оплаты
        var paymentType = entriesByType["PaymentType"][random.Next(entriesByType["PaymentType"].Count)];
        orderValues.Add(new DenormalizedManyToMany.OrderDictionaryValue
        {
            OrderId = order.Id,
            DictionaryEntryId = paymentType.Id,
            DictionaryType = "PaymentType"
        });
    }
    
    return orderValues;
}

public static List<DenormalizedManyToMany.Order> GenerateDenormalizedManyToManyOrders(
    List<DenormalizedManyToMany.DictionaryEntry> dictionaryEntries,
    int count)
{
    var faker = new Faker();
    var orders = new List<DenormalizedManyToMany.Order>();
    var random = new Random();
    
    // Группируем записи справочника по типам для удобства
    var entriesByType = dictionaryEntries
        .GroupBy(e => e.DictionaryType)
        .ToDictionary(g => g.Key, g => g.ToList());
    
    var baseDate = DateTime.UtcNow.AddYears(-1);
    
    for (int i = 0; i < count; i++)
    {
        var orderDate = baseDate.AddDays(random.Next(0, 365));
        
        orders.Add(new DenormalizedManyToMany.Order
        {
            OrderNumber = $"ORD-MTM-{DateTime.UtcNow:yyyyMMdd}-{i:D6}",
            OrderDate = DateTime.SpecifyKind(orderDate, DateTimeKind.Utc),
            TotalAmount = Math.Round(faker.Random.Decimal(100, 10000), 2),
            CustomerComment = faker.Random.Bool(0.3f) ? faker.Lorem.Sentence() : null
        });
    }
    
    return orders;
}
}