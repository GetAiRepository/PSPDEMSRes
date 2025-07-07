using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IDepartmentMasterRepository
    {

        Task<List<org_department>> ListAsync();
        Task<org_department> GetByIdAsync(short id);
        Task AddAsync(org_department d);
        Task UpdateAsync(org_department d);
        Task DeleteAsync(short id);

        // Add this new method
        Task<bool> IsDepartmentNameExistsAsync(string deptName, short? excludeId = null);
    }
}
