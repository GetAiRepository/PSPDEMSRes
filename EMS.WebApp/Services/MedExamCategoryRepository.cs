using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedExamCategoryRepository : IMedExamCategoryRepository
    {
        private readonly ApplicationDbContext _db;
        public MedExamCategoryRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedExamCategory>> ListAsync() =>
            await _db.Set<MedExamCategory>().ToListAsync();

        public async Task<MedExamCategory?> GetByIdAsync(int id) =>
            await _db.Set<MedExamCategory>().FindAsync(id);

        public async Task AddAsync(MedExamCategory entity)
        {
            _db.Set<MedExamCategory>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedExamCategory entity)
        {
            _db.Set<MedExamCategory>().Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _db.Set<MedExamCategory>().FindAsync(id);
            if (ent != null)
            {
                _db.Set<MedExamCategory>().Remove(ent);
                await _db.SaveChangesAsync();
            }
        }
        // Implement the new method for composite uniqueness check
        public async Task<bool> IsCategoryDetailsExistsAsync(string catName, byte yearsFreq, string annuallyRule, string monthsSched, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(catName) || string.IsNullOrWhiteSpace(annuallyRule) || string.IsNullOrWhiteSpace(monthsSched))
                return false;

            var query = _db.Set<MedExamCategory>().Where(c =>
                c.CatName.ToLower() == catName.ToLower() &&
                c.YearsFreq == yearsFreq &&
                c.AnnuallyRule.ToLower() == annuallyRule.ToLower() &&
                c.MonthsSched.ToLower() == monthsSched.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.CatId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
