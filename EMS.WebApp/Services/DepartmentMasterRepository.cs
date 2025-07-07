using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class DepartmentMasterRepository : IDepartmentMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public DepartmentMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<org_department>> ListAsync() =>
          await _db.org_departments.ToListAsync();

        public async Task<org_department> GetByIdAsync(short id) =>
          await _db.org_departments.FindAsync(id);

        public async Task AddAsync(org_department d)
        {
            _db.org_departments.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(org_department d)
        {
            _db.org_departments.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(short id)
        {
            var d = await _db.org_departments.FindAsync(id);
            if (d != null)
            {
                _db.org_departments.Remove(d);
                await _db.SaveChangesAsync();
            }
        }

        // Implement the new method
        public async Task<bool> IsDepartmentNameExistsAsync(string deptName, short? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(deptName)) return false;

            var query = _db.org_departments.Where(d => d.dept_name.ToLower() == deptName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.dept_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
