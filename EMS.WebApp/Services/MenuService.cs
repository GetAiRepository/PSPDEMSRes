using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MenuService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<MenuItemViewModel>> GetMenuItemsForUserAsync(string fullName)
        {
            var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.adid == fullName && u.is_active);
            if (user == null)
            {
                user = await _db.SysUsers.FirstOrDefaultAsync(u => u.full_name == fullName && u.is_active);
            }
            if (user == null)
            {
                return new List<MenuItemViewModel>();
            }

            var roleId = user.role_id;

            var screens = await _db.sys_screen_names.ToListAsync();
            var mappings = await _db.SysAttachScreenRoles
                                    .Where(m => m.role_uid == roleId)
                                    .ToListAsync();

            var menuItems = mappings
                .SelectMany(m =>
                    m.screen_uid.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(idStr => int.TryParse(idStr, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .Select(screenId =>
                    {
                        var screen = screens.FirstOrDefault(s => s.screen_uid == screenId);
                        return screen != null ? new MenuItemViewModel
                        {
                            ScreenName = screen.screen_name,
                            ControllerName = screen.screen_name,
                            ActionName = "Index",
                            MenuGroup = DetermineMenuGroup(screen.screen_name)
                        } : null;
                    })
                    .Where(mi => mi != null)
                )
                .ToList();

            Console.WriteLine($"Menu count: {menuItems.Count}");

            return menuItems;
        }

        /// <summary>
        /// Determines the menu group based on screen name
        /// </summary>
        /// <param name="screenName">Name of the screen</param>
        /// <returns>Menu group name</returns>
        private string DetermineMenuGroup(string screenName)
        {
            if (string.IsNullOrEmpty(screenName))
                return "Masters";

            var lowerScreenName = screenName.ToLower();

            // Transaction-related keywords
            var transactionKeywords = new[]
            {
                "transaction", "expiredmedicine", "doctordiagnosis", "compoundercndent", "storeindent",
                "employeehealthprofile", "othersdiagnosis", "indent", "entry", "posting",
                "bill", "purchase", "sale", "order", "delivery",
                "stock", "inventory", "movement", "transfer",
                "cashbook", "bankbook", "daybook"
            };

            // Check if screen name contains transaction keywords
            if (transactionKeywords.Any(keyword => lowerScreenName.Contains(keyword)))
            {
                return "Transactions";
            }

            // Master-related keywords (optional - for explicit categorization)
            var masterKeywords = new[]
            {
                "master", "setup", "config", "user", "role",
                "category", "group", "type", "status", "settings",
                "customer", "vendor", "supplier",
                "product", "item", "company", "branch", "department"
            };

            if (masterKeywords.Any(keyword => lowerScreenName.Contains(keyword)))
            {
                return "Masters";
            }

            // Default to Masters for unmatched screens
            return "Masters";
        }

        /// <summary>
        /// Alternative method: Get menu items grouped by menu type
        /// </summary>
        /// <param name="fullName">User's full name</param>
        /// <returns>Dictionary with menu groups as keys and menu items as values</returns>
        public async Task<Dictionary<string, List<MenuItemViewModel>>> GetGroupedMenuItemsForUserAsync(string fullName)
        {
            var allMenuItems = await GetMenuItemsForUserAsync(fullName);

            return allMenuItems
                .GroupBy(item => item.MenuGroup)
                .ToDictionary(group => group.Key, group => group.ToList());
        }
    }
}