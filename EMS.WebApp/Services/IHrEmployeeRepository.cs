using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHrEmployeeRepository
    {


        Task<HrEmployee?> GetByIdWithBaseAsync(int id);
        Task<IEnumerable<HrEmployee>> ListWithBaseAsync();
        Task<IEnumerable<org_department>> GetDepartmentListAsync();
        Task<IEnumerable<org_plant>> GetPlantListAsync();

        Task<List<HrEmployee>> ListAsync();
        Task<HrEmployee> GetByIdAsync(int id);
        Task AddAsync(HrEmployee e);
        Task UpdateAsync(HrEmployee e);
        Task DeleteAsync(int id);
        // Add this new method
        Task<bool> IsEmployeeIdExistsAsync(string empId, int? excludeId = null);
    }
}
