using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedDiseaseRepository : IMedDiseaseRepository
    {
        private readonly ApplicationDbContext _db;
        public MedDiseaseRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedDisease>> ListAsync() =>
          await _db.MedDiseases.ToListAsync();

        public async Task<MedDisease> GetByIdAsync(int id) =>
          await _db.MedDiseases.FindAsync(id);

        public async Task AddAsync(MedDisease d)
        {
            _db.MedDiseases.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedDisease d)
        {
            _db.MedDiseases.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.MedDiseases.FindAsync(id);
            if (d != null) { _db.MedDiseases.Remove(d); await _db.SaveChangesAsync(); }
        }
        // Implement the new method
        public async Task<bool> IsDiseaseNameExistsAsync(string diseaseName, int? excludeId = null)
        {
            var query = _db.MedDiseases.Where(d => d.DiseaseName.ToLower() == diseaseName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.DiseaseId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
