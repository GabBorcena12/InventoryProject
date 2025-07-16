using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryApp.Core.Authorizations
{
    public static class RoleConstants
    {
        // Individual Roles
        public const string Admin = "SuperUser";
        public const string InventoryAdmin = "Inventory-Admin";
        public const string PosAdmin = "POS-Admin";
        public const string InventoryUser = "Inventory-User";
        public const string PosUser = "POS-User";

        // Combined Roles
        public const string AllRoles = Admin + "," + InventoryAdmin + "," + PosAdmin + "," + InventoryUser + "," + PosUser;
        public const string AdminsOnly = Admin + "," + InventoryAdmin + "," + PosAdmin;
        public const string PosRoles = Admin + "," + PosAdmin + "," + PosUser;
        public const string InventoryRoles = Admin + "," + InventoryAdmin + "," + InventoryUser;
    }
}
