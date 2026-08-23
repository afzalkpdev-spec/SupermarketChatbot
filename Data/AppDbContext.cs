using Microsoft.EntityFrameworkCore;
using SupermarketBot.Models;

namespace SupermarketBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventory => Set<Inventory>();

     public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<Offer> Offers => Set<Offer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Your tables already exist (created directly via SQL), so this
        // context just maps to them — no migrations needed to create schema.
        // If you later want EF to own schema changes, switch to migrations.

        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.WhatsAppNumber)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
