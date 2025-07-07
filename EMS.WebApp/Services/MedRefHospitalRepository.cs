using System.Collections.Generic;
using System.Linq;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedRefHospitalRepository : IMedRefHospitalRepository
    {
        private readonly ApplicationDbContext _db;
        public MedRefHospitalRepository(ApplicationDbContext db) => _db = db;


        public async Task<List<MedRefHospital>> ListAsync() =>
          await _db.MedRefHospital.ToListAsync();

        public async Task<MedRefHospital> GetByIdAsync(int id) =>
          await _db.MedRefHospital.FindAsync(id);

        public async Task AddAsync(MedRefHospital h)
        {
            _db.MedRefHospital.Add(h);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedRefHospital h)
        {
            _db.MedRefHospital.Update(h);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var h = await _db.MedRefHospital.FindAsync(id);
            if (h != null) { _db.MedRefHospital.Remove(h); await _db.SaveChangesAsync(); }
        }
        // Implement the new method for composite uniqueness check
        public async Task<bool> IsHospitalNameCodeExistsAsync(string hospName, string hospCode, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(hospName) || string.IsNullOrWhiteSpace(hospCode))
                return false;

            var query = _db.Set<MedRefHospital>().Where(h =>
                h.hosp_name.ToLower() == hospName.ToLower() &&
                h.hosp_code.ToLower() == hospCode.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(h => h.hosp_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}