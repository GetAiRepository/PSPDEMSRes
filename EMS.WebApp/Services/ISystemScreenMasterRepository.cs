using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISystemScreenMasterRepository
    {
        Task<ScreenUpdateResult> UpdateIfControllerExistsAsync(sys_screen_name d);
        Task<ScreenUpdateResult> AddIfControllerExistsAsync(sys_screen_name d);

        Task<List<sys_screen_name>> ListAsync();
        Task<sys_screen_name> GetByIdAsync(int id);
        Task AddAsync(sys_screen_name d);
        Task UpdateAsync(sys_screen_name d);
        Task DeleteAsync(int id);
    }
}

