using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedMasterRepository
    {
        Task<MedMaster?> GetByIdWithBaseAsync(int id);
        Task<IEnumerable<MedMaster>> ListWithBaseAsync();
        Task<IEnumerable<MedBase>> GetBaseListAsync();

        Task<List<MedMaster>> ListAsync();
        Task<MedMaster?> GetByIdAsync(int id);
        Task AddAsync(MedMaster entity);
        Task UpdateAsync(MedMaster entity);
        Task DeleteAsync(int id);

        //Add this new method for composite uniqueness check
       Task<bool> IsMedItemDetailsExistsAsync(string medItemName, int? baseId, string? companyName, int? excludeId = null);
    }
}
