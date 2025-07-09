using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryApp.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Core.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<RepackItem> RepackItem { get; set; }
        public DbSet<DisplayItem> DisplayItems { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<OperatingExpense> OperatingExpenses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply global filters for soft-deleted entities
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Supplier>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<Inventory>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<RepackItem>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<DisplayItem>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Sale>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(po => !po.IsDeleted);
            modelBuilder.Entity<OperatingExpense>().HasQueryFilter(oe => !oe.IsDeleted);

            // Foreign key relationships
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Inventory)
                .WithMany(p => p.Sales)
                .HasForeignKey(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.RepackItem)
                .WithMany()
                .HasForeignKey(s => s.RepackItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.DisplayItem)
                .WithMany()
                .HasForeignKey(s => s.DisplayItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RepackItem>()
                .HasOne(r => r.product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Decimal precision configuration
            modelBuilder.Entity<Inventory>()
                .Property(i => i.CostPerUnit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Inventory>()
                .Property(i => i.PricePerUnit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OperatingExpense>()
                .Property(o => o.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PurchaseOrder>()
                .Property(p => p.CostPerUnit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RepackItem>()
                .Property(r => r.Discount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RepackItem>()
                .Property(r => r.PricePerUnit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalPrice)
                .HasPrecision(18, 2);
        }
    }
}
