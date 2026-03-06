using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseCommon.Models.DenormalizedModels;

// Единая таблица справочников (плохая практика - EAV-like)
public class DictionaryEntry
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string DictionaryType { get; set; } = string.Empty; // 'OrderType', 'DeliveryType', 'OrderStatus', etc.
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;
    
    public string? AdditionalData { get; set; } // JSON поле для хранения дополнительных атрибутов
}

// Денормализованная таблица заказов (хранит коды напрямую)
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
    
    // Вместо внешних ключей - строковые коды (денормализация)
    [StringLength(50)]
    public string OrderTypeCode { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string DeliveryTypeCode { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string OrderStatusCode { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string PaymentStatusCode { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string PaymentTypeCode { get; set; } = string.Empty;
    
    // Денормализованные копии названий для быстрого доступа (еще хуже, но часто встречается)
    [StringLength(200)]
    public string? OrderTypeName { get; set; }
    
    [StringLength(200)]
    public string? DeliveryTypeName { get; set; }
    
    [StringLength(200)]
    public string? OrderStatusName { get; set; }
    
    [StringLength(200)]
    public string? PaymentStatusName { get; set; }
    
    [StringLength(200)]
    public string? PaymentTypeName { get; set; }
}