using InventoryApp.Core.Models;
using InventoryApp.Core.Models.PosModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryApp.Core.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- Inventory Tables ---
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<RepackItem> RepackItem { get; set; }
        public DbSet<DisplayItem> DisplayItems { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<OperatingExpense> OperatingExpenses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // --- POS Tables ---
        public DbSet<PosModels.AuditLog> POSAuditLogs { get; set; }
        public DbSet<PosModels.AuditLogItem> POSAuditLogItem { get; set; }
        public DbSet<PosModels.Discount> POSDiscount { get; set; }
        public DbSet<PosModels.Product> POSProduct { get; set; }

        // --- New Transaction Tables ---
        public DbSet<PosModels.TransactionHeader> POSTransactionHeaders { get; set; }
        public DbSet<PosModels.TransactionDetail> POSTransactionDetails { get; set; }
        public DbSet<PosModels.TransactionRepackItem> TransactionRepackItems { get; set; }
        public DbSet<PosModels.CreditMemo> CreditMemos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Global filters for soft delete ---
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Supplier>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<Inventory>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<RepackItem>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<DisplayItem>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Sale>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(po => !po.IsDeleted);
            modelBuilder.Entity<PosModels.AuditLog>().HasQueryFilter(oe => !oe.IsDeleted);
            modelBuilder.Entity<PosModels.AuditLogItem>().HasQueryFilter(oe => !oe.IsDeleted);
            modelBuilder.Entity<PosModels.Discount>().HasQueryFilter(oe => !oe.IsDeleted);
            modelBuilder.Entity<PosModels.Product>().HasQueryFilter(oe => !oe.IsDeleted);
            modelBuilder.Entity<PosModels.TransactionHeader>().HasQueryFilter(oe => !oe.IsDeleted);
            modelBuilder.Entity<PosModels.TransactionDetail>().HasQueryFilter(oe => !oe.IsDeleted);

            // --- Inventory Foreign Key Relationships ---
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Inventory)
                .WithMany(p => p.Sales)
                .HasForeignKey(s => s.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.repackItem)
                .WithMany()
                .HasForeignKey(s => s.RepackItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.displayItem)
                .WithMany()
                .HasForeignKey(s => s.DisplayItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RepackItem>()
                .HasOne(r => r.product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- DisplayItem Relationships (prevent multiple cascade paths) ---
            
            modelBuilder.Entity<DisplayItem>()
                .HasOne(d => d.RepackItem)
                .WithMany()
                .HasForeignKey(d => d.RepackItemId)
                .OnDelete(DeleteBehavior.Cascade); // Keep cascade from RepackItem

            // --- Decimal Precision Config ---
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
                .Property(r => r.PricePerUnit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalPrice)
                .HasPrecision(18, 2);

            // --- POS Models Relationships ---
            modelBuilder.Entity<PosModels.AuditLog>()
                .HasMany(a => a.ItemsAffected)
                .WithOne(i => i.AuditLog)
                .HasForeignKey(i => i.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PosModels.AuditLogItem>()
                .HasOne(i => i.AuditLog)
                .WithMany(a => a.ItemsAffected)
                .HasForeignKey(i => i.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PosModels.AuditLog>()
                .HasOne(a => a.OldDiscountType)
                .WithMany()
                .HasForeignKey(a => a.OldDiscountTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PosModels.AuditLog>()
                .HasOne(a => a.NewDiscountType)
                .WithMany()
                .HasForeignKey(a => a.NewDiscountTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Transaction Header & Detail Relationship ---
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .HasMany(h => h.TransactionDetails)
                .WithOne(d => d.TransactionHeader)
                .HasForeignKey(d => d.TransactionHeaderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Decimal precision for amounts in TransactionHeader
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.AmountTendered)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.ChangeAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.RegularDiscount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.StatutoryDiscount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.VATIncluded)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PosModels.TransactionHeader>()
                .Property(t => t.VATExcluded)
                .HasPrecision(18, 2);
        }
    }
}
