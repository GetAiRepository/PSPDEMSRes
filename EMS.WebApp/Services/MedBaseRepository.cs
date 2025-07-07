using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedBaseRepository : IMedBaseRepository
    {
        private readonly ApplicationDbContext _db;
        public MedBaseRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedBase>> ListAsync() =>
            await _db.Set<MedBase>().ToListAsync();

        public async Task<MedBase?> GetByIdAsync(int id) =>
            await _db.Set<MedBase>().FindAsync(id);

        public async Task AddAsync(MedBase entity)
        {
            _db.Set<MedBase>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedBase entity)
        {
            _db.Set<MedBase>().Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _db.Set<MedBase>().FindAsync(id);
            if (ent != null)
            {
                _db.Set<MedBase>().Remove(ent);
                await _db.SaveChangesAsync();
            }
        }

        // Implement the new method
        public async Task<bool> IsBaseNameExistsAsync(string baseName, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return false;

            var query = _db.Set<MedBase>().Where(b => b.BaseName.ToLower() == baseName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.BaseId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}