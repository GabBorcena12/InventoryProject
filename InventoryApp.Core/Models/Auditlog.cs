using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryApp.Core.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Action { get; set; }           // "Create", "Update", "Delete"
        public string EntityName { get; set; }       // "Product", "Inventory", etc.
        public string? EntityId { get; set; }
        public string Description { get; set; }
        public string PerformedBy { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}
