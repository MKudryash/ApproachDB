// DatabaseCommon/Models/DenormalizedModels/DenormalizedManyToManyWithEnum/
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DatabaseCommon.Models;

namespace DatabaseCommon.Models.DenormalizedManyToManyWithEnum;

/// <summary>
/// Единый справочник для всех типов данных с использованием enum
/// </summary>
public class DictionaryEntry
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public DictionaryType DictionaryType { get; set; } // Enum вместо string
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;
    
    public string? AdditionalData { get; set; } // JSON поле
    
    // Навигационное свойство
    public virtual ICollection<OrderDictionaryValue> OrderValues { get; set; } = new List<OrderDictionaryValue>();
}

/// <summary>
/// Заказ (базовая информация)
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
    
    // Навигационное свойство
    public virtual ICollection<OrderDictionaryValue> DictionaryValues { get; set; } = new List<OrderDictionaryValue>();
}

/// <summary>
/// Таблица связи между заказами и значениями справочника
/// </summary>
public class OrderDictionaryValue
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int OrderId { get; set; }
    
    [Required]
    public int DictionaryEntryId { get; set; }
    
    [Required]
    public DictionaryType DictionaryType { get; set; } // Enum для быстрого доступа
    
    // Навигационные свойства
    [ForeignKey(nameof(OrderId))]
    public virtual Order Order { get; set; } = null!;
    
    [ForeignKey(nameof(DictionaryEntryId))]
    public virtual DictionaryEntry DictionaryEntry { get; set; } = null!;
}

public enum DictionaryType
{
    OrderType = 1,
    DeliveryType = 2,
    OrderStatus = 3,
    PaymentStatus = 4,
    PaymentType = 5
}