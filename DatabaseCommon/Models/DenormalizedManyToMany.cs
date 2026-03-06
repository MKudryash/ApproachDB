using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseCommon.Models.DenormalizedModels.DenormalizedManyToMany;

/// <summary>
/// Единый справочник для всех типов данных (ключ-значение)
/// </summary>
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
    
    // Навигационное свойство
    public virtual ICollection<OrderDictionaryValue> OrderValues { get; set; } = new List<OrderDictionaryValue>();
}


/// <summary>
/// Заказ (хранит только базовую информацию)
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
    
    // Навигационное свойство для связи многие-ко-многим
    public virtual ICollection<OrderDictionaryValue> DictionaryValues { get; set; } = new List<OrderDictionaryValue>();
}


/// <summary>
/// Таблица связи между заказами и значениями справочника (многие-ко-многим)
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
    [StringLength(50)]
    public string DictionaryType { get; set; } = string.Empty; // Денормализация - дублируем тип для быстрого доступа
    
    // Навигационные свойства
    [ForeignKey(nameof(OrderId))]
    public virtual Order Order { get; set; } = null!;
    
    [ForeignKey(nameof(DictionaryEntryId))]
    public virtual DictionaryEntry DictionaryEntry { get; set; } = null!;
}