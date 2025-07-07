using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedDiagnosisRepository : IMedDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        public MedDiagnosisRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedDiagnosis>> ListAsync() =>
          await _db.MedDiagnosis.ToListAsync();

        public async Task<MedDiagnosis> GetByIdAsync(int id) =>
          await _db.MedDiagnosis.FindAsync(id);

        public async Task AddAsync(MedDiagnosis d)
        {
            _db.MedDiagnosis.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedDiagnosis d)
        {
            _db.MedDiagnosis.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.MedDiagnosis.FindAsync(id);
            if (d != null)
            {
                _db.MedDiagnosis.Remove(d);
                await _db.SaveChangesAsync();
            }
        }

        // Implement the new method
        public async Task<bool> IsDiagnosisNameExistsAsync(string diagnosisName, int? excludeId = null)
        {
            var query = _db.MedDiagnosis.Where(d => d.diag_name.ToLower() == diagnosisName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.diag_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
