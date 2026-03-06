using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseCommon.Models.NormalizedModels;

// Справочники (нормализованная схема)
public class OrderType
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    // Навигационное свойство
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class DeliveryType
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public decimal? BasePrice { get; set; }
    public int? EstimatedDays { get; set; }
    
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class OrderStatus
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? ColorCode { get; set; }
    public int SortOrder { get; set; }
    
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class PaymentStatus
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public bool IsPaid { get; set; }
    
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class PaymentType
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public decimal? Commission { get; set; }
    
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}

// Основная таблица заказов (нормализованная)
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
    
    // Внешние ключи
    public int OrderTypeId { get; set; }
    public int DeliveryTypeId { get; set; }
    public int OrderStatusId { get; set; }
    public int PaymentStatusId { get; set; }
    public int PaymentTypeId { get; set; }
    
    // Навигационные свойства
    [ForeignKey(nameof(OrderTypeId))]
    public virtual OrderType OrderType { get; set; } = null!;
    
    [ForeignKey(nameof(DeliveryTypeId))]
    public virtual DeliveryType DeliveryType { get; set; } = null!;
    
    [ForeignKey(nameof(OrderStatusId))]
    public virtual OrderStatus OrderStatus { get; set; } = null!;
    
    [ForeignKey(nameof(PaymentStatusId))]
    public virtual PaymentStatus PaymentStatus { get; set; } = null!;
    
    [ForeignKey(nameof(PaymentTypeId))]
    public virtual PaymentType PaymentType { get; set; } = null!;
}