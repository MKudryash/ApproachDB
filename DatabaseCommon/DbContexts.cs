using DatabaseCommon.Models;
using Microsoft.EntityFrameworkCore;

using DenormalizedManyToMany = DatabaseCommon.Models.DenormalizedModels.DenormalizedManyToMany;
using DenormalizedModels = DatabaseCommon.Models.DenormalizedModels;
using NormalizedModels = DatabaseCommon.Models.NormalizedModels;
using DenormalizedManyToManyWithEnum = DatabaseCommon.Models.DenormalizedManyToManyWithEnum;
using NormalizedWithEnumModels = DatabaseCommon.Models.NormalizedWithEnumModels;


namespace OrderSystemComparison.Data;

public class NormalizedDbContext : DbContext
{
    public NormalizedDbContext(DbContextOptions<NormalizedDbContext> options) : base(options) { }
    
    public DbSet<NormalizedModels.OrderType> OrderTypes { get; set; }
    public DbSet<NormalizedModels.DeliveryType> DeliveryTypes { get; set; }
    public DbSet<NormalizedModels.OrderStatus> OrderStatuses { get; set; }
    public DbSet<NormalizedModels.PaymentStatus> PaymentStatuses { get; set; }
    public DbSet<NormalizedModels.PaymentType> PaymentTypes { get; set; }
    public DbSet<NormalizedModels.Order> Orders { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Уникальные индексы для справочников
        modelBuilder.Entity<NormalizedModels.OrderType>()
            .HasIndex(ot => ot.Code)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedModels.DeliveryType>()
            .HasIndex(dt => dt.Code)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedModels.OrderStatus>()
            .HasIndex(os => os.Code)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedModels.PaymentStatus>()
            .HasIndex(ps => ps.Code)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedModels.PaymentType>()
            .HasIndex(pt => pt.Code)
            .IsUnique();
            
        // Индексы для заказов
        modelBuilder.Entity<NormalizedModels.Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedModels.Order>()
            .HasIndex(o => o.OrderDate);
            
        modelBuilder.Entity<NormalizedModels.Order>()
            .HasIndex(o => o.OrderTypeId);
            
        modelBuilder.Entity<NormalizedModels.Order>()
            .HasIndex(o => o.OrderStatusId);
    }
}

public class DenormalizedDbContext : DbContext
{
    public DenormalizedDbContext(DbContextOptions<DenormalizedDbContext> options) : base(options) { }
    
    public DbSet<DenormalizedModels.DictionaryEntry> DictionaryEntries { get; set; }
    public DbSet<DenormalizedModels.Order> Orders { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Составной уникальный индекс для словаря
        modelBuilder.Entity<DenormalizedModels.DictionaryEntry>()
            .HasIndex(de => new { de.DictionaryType, de.Code })
            .IsUnique();
            
        // Индекс для типа словаря
        modelBuilder.Entity<DenormalizedModels.DictionaryEntry>()
            .HasIndex(de => de.DictionaryType);
            
        // Индексы для заказов
        modelBuilder.Entity<DenormalizedModels.Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
            
        modelBuilder.Entity<DenormalizedModels.Order>()
            .HasIndex(o => o.OrderDate);
            
        // Индексы для кодов
        modelBuilder.Entity<DenormalizedModels.Order>()
            .HasIndex(o => o.OrderTypeCode);
            
        modelBuilder.Entity<DenormalizedModels.Order>()
            .HasIndex(o => o.OrderStatusCode);
    }
}

public class DenormalizedManyToManyDbContext : DbContext
{
    public DenormalizedManyToManyDbContext(DbContextOptions<DenormalizedManyToManyDbContext> options) : base(options) { }
    
    public DbSet<DenormalizedManyToMany.DictionaryEntry> DictionaryEntries { get; set; }
    public DbSet<DenormalizedManyToMany.Order> Orders { get; set; }
    public DbSet<DenormalizedManyToMany.OrderDictionaryValue> OrderDictionaryValues { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация для DictionaryEntry
        modelBuilder.Entity<DenormalizedManyToMany.DictionaryEntry>()
            .HasIndex(de => new { de.DictionaryType, de.Code })
            .IsUnique();
            
        modelBuilder.Entity<DenormalizedManyToMany.DictionaryEntry>()
            .HasIndex(de => de.DictionaryType);
        
        // Конфигурация для Order
        modelBuilder.Entity<DenormalizedManyToMany.Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
            
        modelBuilder.Entity<DenormalizedManyToMany.Order>()
            .HasIndex(o => o.OrderDate);
        
        // Конфигурация для OrderDictionaryValue
        modelBuilder.Entity<DenormalizedManyToMany.OrderDictionaryValue>()
            .HasIndex(odv => new { odv.OrderId, odv.DictionaryType })
            .HasDatabaseName("IX_OrderDictionaryValue_Order_Type");
            
        modelBuilder.Entity<DenormalizedManyToMany.OrderDictionaryValue>()
            .HasIndex(odv => odv.DictionaryEntryId);
            
        modelBuilder.Entity<DenormalizedManyToMany.OrderDictionaryValue>()
            .HasIndex(odv => odv.DictionaryType);
        
        // Настройка связей
        modelBuilder.Entity<DenormalizedManyToMany.OrderDictionaryValue>()
            .HasOne(odv => odv.Order)
            .WithMany(o => o.DictionaryValues)
            .HasForeignKey(odv => odv.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<DenormalizedManyToMany.OrderDictionaryValue>()
            .HasOne(odv => odv.DictionaryEntry)
            .WithMany(de => de.OrderValues)
            .HasForeignKey(odv => odv.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Restrict); // Не удаляем записи справочника, если они используются
    }
}

public class DenormalizedManyToManyWithEnumDbContext : DbContext
{
    public DenormalizedManyToManyWithEnumDbContext(DbContextOptions<DenormalizedManyToManyWithEnumDbContext> options) : base(options) { }
    
    public DbSet<DenormalizedManyToManyWithEnum.DictionaryEntry> DictionaryEntries { get; set; }
    public DbSet<DenormalizedManyToManyWithEnum.Order> Orders { get; set; }
    public DbSet<DenormalizedManyToManyWithEnum.OrderDictionaryValue> OrderDictionaryValues { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация для DictionaryEntry
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.DictionaryEntry>()
            .HasIndex(de => new { de.DictionaryType, de.Code })
            .IsUnique();
        
        // Храним enum как int в БД (более эффективно)
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.DictionaryEntry>()
            .Property(e => e.DictionaryType)
            .HasConversion<int>();
        
        // Конфигурация для Order
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
        
        // Конфигурация для OrderDictionaryValue
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.OrderDictionaryValue>()
            .HasIndex(odv => new { odv.OrderId, odv.DictionaryType })
            .HasDatabaseName("IX_OrderDictionaryValue_Order_Type");
        
        // Храним enum как int
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.OrderDictionaryValue>()
            .Property(odv => odv.DictionaryType)
            .HasConversion<int>();
        
        // Связи
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.OrderDictionaryValue>()
            .HasOne(odv => odv.Order)
            .WithMany(o => o.DictionaryValues)
            .HasForeignKey(odv => odv.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<DenormalizedManyToManyWithEnum.OrderDictionaryValue>()
            .HasOne(odv => odv.DictionaryEntry)
            .WithMany(de => de.OrderValues)
            .HasForeignKey(odv => odv.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// OrderSystemComparison.Data/DbContexts.cs
public class NormalizedWithEnumDbContext : DbContext
{
    public NormalizedWithEnumDbContext(DbContextOptions<NormalizedWithEnumDbContext> options) : base(options) { }
    
    public DbSet<NormalizedWithEnumModels.Order> Orders { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Индексы для заказов
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();
            
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .HasIndex(o => o.OrderDate);
        
        // Индексы для enum полей (для ускорения фильтрации)
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .HasIndex(o => o.OrderStatus);
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .HasIndex(o => o.OrderType);
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .HasIndex(o => o.PaymentType);
        
        // Храним enum как int в БД
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .Property(o => o.OrderType)
            .HasConversion<int>();
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .Property(o => o.DeliveryType)
            .HasConversion<int>();
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .Property(o => o.OrderStatus)
            .HasConversion<int>();
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .Property(o => o.PaymentStatus)
            .HasConversion<int>();
        
        modelBuilder.Entity<NormalizedWithEnumModels.Order>()
            .Property(o => o.PaymentType)
            .HasConversion<int>();
    }
}