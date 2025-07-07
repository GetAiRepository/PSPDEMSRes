using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class HrEmployeeDependentRepository : IHrEmployeeDependentRepository
    {
        private readonly ApplicationDbContext _db;
        public HrEmployeeDependentRepository(ApplicationDbContext db) => _db = db;


        public async Task<IEnumerable<HrEmployeeDependent>> ListWithBaseAsync()
        {
            return await _db.HrEmployeeDependents.Include(m => m.HrEmployee).ToListAsync();
        }
        public async Task<HrEmployeeDependent?> GetByIdWithBaseAsync(int id)
        {
            return await _db.HrEmployeeDependents
                .Include(m => m.HrEmployee)
                .FirstOrDefaultAsync(m => m.emp_dep_id == id);
        }

        public async Task<IEnumerable<HrEmployee>> GetBaseListAsync()
        {
            return await _db.HrEmployees.ToListAsync();
        }



        public async Task<List<HrEmployeeDependent>> ListAsync() =>
          await _db.HrEmployeeDependents.ToListAsync();

        public async Task<HrEmployeeDependent> GetByIdAsync(int id) =>
          await _db.HrEmployeeDependents.FindAsync(id);

        public async Task AddAsync(HrEmployeeDependent d)
        {
            _db.HrEmployeeDependents.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(HrEmployeeDependent d)
        {
            _db.HrEmployeeDependents.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.HrEmployeeDependents.FindAsync(id);
            if (d != null) { _db.HrEmployeeDependents.Remove(d); await _db.SaveChangesAsync(); }
        }

    }
}
