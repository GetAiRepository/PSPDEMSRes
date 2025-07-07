using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IPlantMasterRepository
    {
        Task<List<org_plant>> ListAsync();
        Task<org_plant> GetByIdAsync(short id);
        Task AddAsync(org_plant d);
        Task UpdateAsync(org_plant d);
        Task DeleteAsync(short id);
        // Add this new method
        Task<bool> IsPlantCodeExistsAsync(string plantCode, short? excludeId = null);
    }
}
