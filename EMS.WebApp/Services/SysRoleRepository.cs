using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class SysRoleRepository : ISysRoleRepository
    {
        private readonly ApplicationDbContext _db;
        public SysRoleRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<SysRole>> ListAsync() =>
          await _db.SysRoles.ToListAsync();

        public async Task<SysRole> GetByIdAsync(int id) =>
          await _db.SysRoles.FindAsync(id);

        public async Task AddAsync(SysRole r)
        {
            _db.SysRoles.Add(r);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysRole r)
        {
            _db.SysRoles.Update(r);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var r = await _db.SysRoles.FindAsync(id);
            if (r != null) { _db.SysRoles.Remove(r); await _db.SaveChangesAsync(); }
        }
        // Implement the new method
        public async Task<bool> IsRoleNameExistsAsync(string roleName, int? excludeId = null)
        {
            var query = _db.SysRoles.Where(r => r.role_name.ToLower() == roleName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(r => r.role_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
