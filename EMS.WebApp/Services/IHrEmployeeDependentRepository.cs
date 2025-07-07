using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHrEmployeeDependentRepository
    {

        Task<HrEmployeeDependent?> GetByIdWithBaseAsync(int id);
        Task<IEnumerable<HrEmployeeDependent>> ListWithBaseAsync();
        Task<IEnumerable<HrEmployee>> GetBaseListAsync();
        Task<List<HrEmployeeDependent>> ListAsync();
        Task<HrEmployeeDependent> GetByIdAsync(int id);
        Task AddAsync(HrEmployeeDependent d);
        Task UpdateAsync(HrEmployeeDependent d);
        Task DeleteAsync(int id);
    }
}
