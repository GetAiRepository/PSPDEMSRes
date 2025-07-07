using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class SysUserRepository : ISysUserRepository
    {
        private readonly ApplicationDbContext _db;
        public SysUserRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<SysUser>> ListWithBaseAsync()
        {
            return await _db.SysUsers.Include(m => m.SysRole).ToListAsync();
        }

        // FIXED: Changed from role_id == id to user_id == id
        public async Task<SysUser?> GetByIdWithBaseAsync(int id)
        {
            return await _db.SysUsers
                .Include(m => m.SysRole)
                .FirstOrDefaultAsync(m => m.user_id == id);  // ← FIXED: Now correctly filters by user_id
        }

        public async Task<IEnumerable<SysRole>> GetBaseListAsync()
        {
            return await _db.SysRoles.ToListAsync();
        }

        public async Task<List<SysUser>> ListAsync() =>
          await _db.SysUsers.ToListAsync();

        public async Task<SysUser> GetByIdAsync(int id) =>
          await _db.SysUsers.FindAsync(id);

        public async Task AddAsync(SysUser u)
        {
            _db.SysUsers.Add(u);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysUser u)
        {
            _db.SysUsers.Update(u);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var u = await _db.SysUsers.FindAsync(id);
            if (u != null) { _db.SysUsers.Remove(u); await _db.SaveChangesAsync(); }
        }
    }
}