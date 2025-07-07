using EMS.WebApp.Data;
using System;
using Microsoft.EntityFrameworkCore;
namespace EMS.WebApp.Services
{
    public class ScreenAccessRepository : IScreenAccessRepository
    {
        private readonly ApplicationDbContext _db;
        public ScreenAccessRepository(ApplicationDbContext db) => _db = db;
        public async Task<bool> HasScreenAccessAsync(string userUsername, string screenName)
        {
            var user = await _db.SysUsers
                .Include(u => u.SysRole)
                .FirstOrDefaultAsync(u => u.full_name == userUsername && u.is_active);

            if (user == null || user.role_id == null)
                return false;

            var mappings = await _db.SysAttachScreenRoles
                .Where(m => m.role_uid == user.role_id)
                .ToListAsync();

            var screenList = await _db.sys_screen_names.ToListAsync();

            foreach (var mapping in mappings)
            {
                var screenIds = mapping.screen_uid
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id, out var i) ? i : 0)
                    .Where(id => id > 0)
                    .ToList();

                foreach (var id in screenIds)
                {
                    var screen = screenList.FirstOrDefault(s => s.screen_uid == id);
                    if (screen != null && screen.screen_name.Equals(screenName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        //public async Task<bool> HasScreenAccessAsync(string userUsername, string screenName)
        //{
        //    return await _db.SysUsers
        //        .Where(u => u.full_name == userUsername && u.is_active)
        //        .Join(_db.SysRoles, u => u.role_id, r => r.role_id, (u, r) => new { u, r })
        //        .Join(_db.SysAttachScreenRoles, ur => ur.r.role_id, asr => asr.role_uid, (ur, asr) => new { ur.u, asr })
        //        .Join(_db.sys_screen_names, asr => asr.asr.screen_uid, sn => sn.screen_uid, (asr, sn) => new { asr.u, sn })
        //        .AnyAsync(x => x.sn.screen_name.ToLower() == screenName.ToLower());
        //}
    }
}
