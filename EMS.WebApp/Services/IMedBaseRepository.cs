using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedBaseRepository
    {
        Task<List<MedBase>> ListAsync();
        Task<MedBase?> GetByIdAsync(int id);
        Task AddAsync(MedBase entity);
        Task UpdateAsync(MedBase entity);
        Task DeleteAsync(int id);

        // Add this new method
        Task<bool> IsBaseNameExistsAsync(string baseName, int? excludeId = null);
    }
}
