using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedExamCategoryRepository
    {
        Task<List<MedExamCategory>> ListAsync();
        Task<MedExamCategory?> GetByIdAsync(int id);
        Task AddAsync(MedExamCategory entity);
        Task UpdateAsync(MedExamCategory entity);
        Task DeleteAsync(int id);
        // Add this new method for composite uniqueness check
        Task<bool> IsCategoryDetailsExistsAsync(string catName, byte yearsFreq, string annuallyRule, string monthsSched, int? excludeId = null);
    }
}
