using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedAmbulanceMasterRepository : IMedAmbulanceMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public MedAmbulanceMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<med_ambulance_master>> ListAsync() =>
          await _db.med_ambulance_masters.ToListAsync();

        public async Task<med_ambulance_master> GetByIdAsync(int id) =>
          await _db.med_ambulance_masters.FindAsync(id);

        public async Task AddAsync(med_ambulance_master d)
        {
            _db.med_ambulance_masters.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(med_ambulance_master d)
        {
            _db.med_ambulance_masters.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.med_ambulance_masters.FindAsync(id);
            if (d != null) { _db.med_ambulance_masters.Remove(d); await _db.SaveChangesAsync(); }
        }
        // Implement the new method for vehicle number uniqueness check
        public async Task<bool> IsVehicleNumberExistsAsync(string vehicleNo, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(vehicleNo))
                return false;

            var query = _db.med_ambulance_masters.Where(a =>
                a.vehicle_no.ToLower() == vehicleNo.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(a => a.amb_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
