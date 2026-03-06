// DatabaseCommon/Models/NormalizedWithEnumModels/OrderWithEnum.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseCommon.Models.NormalizedWithEnumModels;

/// <summary>
/// Заказ с использованием enum вместо внешних таблиц
/// </summary>
public class Order
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string OrderNumber { get; set; } = string.Empty;
    
    public DateTime OrderDate { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    [StringLength(500)]
    public string? CustomerComment { get; set; }
    
    // Используем enum вместо внешних ключей
    public OrderTypeEnum OrderType { get; set; }
    public DeliveryTypeEnum DeliveryType { get; set; }
    public OrderStatusEnum OrderStatus { get; set; }
    public PaymentStatusEnum PaymentStatus { get; set; }
    public PaymentTypeEnum PaymentType { get; set; }
    
    // Для дополнительных данных, которые были в справочниках
    [Column(TypeName = "jsonb")]
    public string? OrderTypeData { get; set; } // JSON с дополнительными полями (Description)
    
    [Column(TypeName = "jsonb")]
    public string? DeliveryTypeData { get; set; } // JSON с дополнительными полями (BasePrice, EstimatedDays)
    
    [Column(TypeName = "jsonb")]
    public string? OrderStatusData { get; set; } // JSON с дополнительными полями (ColorCode, SortOrder)
    
    [Column(TypeName = "jsonb")]
    public string? PaymentStatusData { get; set; } // JSON с дополнительными полями (IsPaid)
    
    [Column(TypeName = "jsonb")]
    public string? PaymentTypeData { get; set; } // JSON с дополнительными полями (Commission)
}

public enum OrderTypeEnum
{
    Standard = 1,
    Preorder = 2,
    Express = 3,
    Wholesale = 4,
    Gift = 5
}

public enum DeliveryTypeEnum
{
    Courier = 1,
    Pickup = 2,
    Post = 3,
    Express = 4,
    International = 5
}

public enum OrderStatusEnum
{
    New = 1,
    Processing = 2,
    Confirmed = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Returned = 7
}

public enum PaymentStatusEnum
{
    Pending = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Refunded = 4,
    Failed = 5
}

public enum PaymentTypeEnum
{
    Card = 1,
    Cash = 2,
    Online = 3,
    Invoice = 4,
    Crypto = 5
}