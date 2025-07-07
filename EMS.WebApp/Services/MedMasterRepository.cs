using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedMasterRepository : IMedMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public MedMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<MedMaster>> ListWithBaseAsync()
        {
            return await _db.med_masters.Include(m => m.MedBase).ToListAsync();
        }
        public async Task<MedMaster?> GetByIdWithBaseAsync(int id)
        {
            return await _db.med_masters
                .Include(m => m.MedBase)
                .FirstOrDefaultAsync(m => m.MedItemId == id);
        }

        public async Task<IEnumerable<MedBase>> GetBaseListAsync()
        {
            return await _db.med_bases.ToListAsync();
        }
       



        public async Task<List<MedMaster>> ListAsync() =>
            await _db.Set<MedMaster>().ToListAsync();

        public async Task<MedMaster?> GetByIdAsync(int id) =>
            await _db.Set<MedMaster>().FindAsync(id);

        public async Task AddAsync(MedMaster entity)
        {
            _db.Set<MedMaster>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedMaster entity)
        {
            _db.Set<MedMaster>().Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _db.Set<MedMaster>().FindAsync(id);
            if (ent != null)
            {
                _db.Set<MedMaster>().Remove(ent);
                await _db.SaveChangesAsync();
            }
        }


        // Implement the new method for composite uniqueness check
        public async Task<bool> IsMedItemDetailsExistsAsync(string medItemName, int? baseId, string? companyName, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(medItemName))
                return false;

            var query = _db.Set<MedMaster>().Where(m =>
                m.MedItemName.ToLower() == medItemName.ToLower() &&
                m.BaseId == baseId);

            // Handle nullable CompanyName comparison
            if (string.IsNullOrWhiteSpace(companyName))
            {
                query = query.Where(m => string.IsNullOrEmpty(m.CompanyName));
            }
            else
            {
                query = query.Where(m => !string.IsNullOrEmpty(m.CompanyName) &&
                                        m.CompanyName.ToLower() == companyName.ToLower());
            }

            if (excludeId.HasValue)
            {
                query = query.Where(m => m.MedItemId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
