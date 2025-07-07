using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedDiagnosisRepository
    {
        Task<List<MedDiagnosis>> ListAsync();
        Task<MedDiagnosis> GetByIdAsync(int id);
        Task AddAsync(MedDiagnosis d);
        Task UpdateAsync(MedDiagnosis d);
        Task DeleteAsync(int id);

        // Add this new method
        Task<bool> IsDiagnosisNameExistsAsync(string diagnosisName, int? excludeId = null);
    }
}
