using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedCategoryRepository : IMedCategoryRepository
    {
        private readonly ApplicationDbContext _db;
        public MedCategoryRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedCategory>> ListAsync() =>
            await _db.Set<MedCategory>().ToListAsync();

        public async Task<MedCategory?> GetByIdAsync(int id) =>
            await _db.Set<MedCategory>().FindAsync(id);

        public async Task AddAsync(MedCategory entity)
        {
            _db.Set<MedCategory>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedCategory entity)
        {
            _db.Set<MedCategory>().Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _db.Set<MedCategory>().FindAsync(id);
            if (ent != null)
            {
                _db.Set<MedCategory>().Remove(ent);
                await _db.SaveChangesAsync();
            }
        }

        // Implement the new method
        public async Task<bool> IsCategoryNameExistsAsync(string categoryName, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return false;

            var query = _db.Set<MedCategory>().Where(c => c.MedCatName.ToLower() == categoryName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.MedCatId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
