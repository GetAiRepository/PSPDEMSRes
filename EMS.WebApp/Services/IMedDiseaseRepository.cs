using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedDiseaseRepository
    {
        Task<List<MedDisease>> ListAsync();
        Task<MedDisease> GetByIdAsync(int id);
        Task AddAsync(MedDisease d);
        Task UpdateAsync(MedDisease d);
        Task DeleteAsync(int id);
        // Add this new method
        Task<bool> IsDiseaseNameExistsAsync(string diseaseName, int? excludeId = null);
    }
}
