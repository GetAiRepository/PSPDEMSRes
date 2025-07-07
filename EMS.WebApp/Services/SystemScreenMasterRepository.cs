using EMS.WebApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EMS.WebApp.Services
{
    public class SystemScreenMasterRepository : ISystemScreenMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public SystemScreenMasterRepository(ApplicationDbContext db) => _db = db;


        public async Task<ScreenUpdateResult> AddIfControllerExistsAsync(sys_screen_name d)
        {
            var controllers = GetAvailableControllerNames();

            if (!controllers.Contains(d.screen_name))
            {
                return new ScreenUpdateResult
                {
                    Success = false,
                    AvailableControllers = controllers
                };
            }

            _db.sys_screen_names.Add(d);
            await _db.SaveChangesAsync();

            return new ScreenUpdateResult { Success = true };
        }

        public async Task<ScreenUpdateResult> UpdateIfControllerExistsAsync(sys_screen_name d)
        {
            var controllers = GetAvailableControllerNames();

            if (!controllers.Contains(d.screen_name))
            {
                return new ScreenUpdateResult
                {
                    Success = false,
                    AvailableControllers = controllers
                };
            }

            _db.sys_screen_names.Update(d);
            await _db.SaveChangesAsync();

            return new ScreenUpdateResult { Success = true };
        }

        private List<string> GetAvailableControllerNames()
        {
            return Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name.Replace("Controller", ""))
                .ToList();
        }


        public async Task<List<sys_screen_name>> ListAsync() =>
          await _db.sys_screen_names.ToListAsync();

        public async Task<sys_screen_name> GetByIdAsync(int id) =>
          await _db.sys_screen_names.FindAsync(id);

        public async Task AddAsync(sys_screen_name d)
        {
            _db.sys_screen_names.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(sys_screen_name d)
        {
            _db.sys_screen_names.Update(d);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.sys_screen_names.FindAsync(id);
            if (d != null) { _db.sys_screen_names.Remove(d); await _db.SaveChangesAsync(); }
        }
    }
}

