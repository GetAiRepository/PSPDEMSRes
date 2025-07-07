
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class PlantMasterRepository : IPlantMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public PlantMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<org_plant>> ListAsync() =>
          await _db.org_plants.ToListAsync();

        public async Task<org_plant> GetByIdAsync(short id) =>
          await _db.org_plants.FindAsync(id);

        public async Task AddAsync(org_plant d)
        {
            _db.org_plants.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(org_plant d)
        {
            _db.org_plants.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(short id)
        {
            var d = await _db.org_plants.FindAsync(id);
            if (d != null) { _db.org_plants.Remove(d); await _db.SaveChangesAsync(); }
        }
        // Implement the new method
        public async Task<bool> IsPlantCodeExistsAsync(string plantCode, short? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(plantCode)) return false;

            var query = _db.org_plants.Where(p => p.plant_code.ToLower() == plantCode.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.plant_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
