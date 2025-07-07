using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class HrEmployeeRepository : IHrEmployeeRepository
    {
        private readonly ApplicationDbContext _db;
        public HrEmployeeRepository(ApplicationDbContext db) => _db = db;


        public async Task<IEnumerable<HrEmployee>> ListWithBaseAsync()
        {
            return await _db.HrEmployees.Include(m => m.org_department).Include(m => m.org_plant).ToListAsync();
        }
        public async Task<HrEmployee?> GetByIdWithBaseAsync(int id)
        {
          
            return await _db.HrEmployees
                .Include(e => e.org_department)
                .Include(e => e.org_plant)
                .FirstOrDefaultAsync(e => e.emp_uid == id);
        }

        public async Task<IEnumerable<org_department>> GetDepartmentListAsync()
        {
            return await _db.org_departments.ToListAsync();
        }

        public async Task<IEnumerable<org_plant>> GetPlantListAsync()
        {
            return await _db.org_plants.ToListAsync();
        }


        public async Task<List<HrEmployee>> ListAsync() =>
          await _db.HrEmployees.ToListAsync();

        public async Task<HrEmployee> GetByIdAsync(int id) =>
          await _db.HrEmployees.FindAsync(id);

        public async Task AddAsync(HrEmployee e)
        {
            _db.HrEmployees.Add(e);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(HrEmployee e)
        {
            _db.HrEmployees.Update(e);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var e = await _db.HrEmployees.FindAsync(id);
            if (e != null) { _db.HrEmployees.Remove(e); await _db.SaveChangesAsync(); }
        }

        // Implement the new method
        public async Task<bool> IsEmployeeIdExistsAsync(string empId, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(empId)) return false;

            var query = _db.HrEmployees.Where(e => e.emp_id.ToLower() == empId.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(e => e.emp_uid != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
