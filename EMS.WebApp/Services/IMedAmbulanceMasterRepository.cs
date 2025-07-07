using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedAmbulanceMasterRepository
    {
    
        Task<List<med_ambulance_master>> ListAsync();
        Task<med_ambulance_master> GetByIdAsync(int id);
        Task AddAsync(med_ambulance_master d);
        Task UpdateAsync(med_ambulance_master d);
        Task DeleteAsync(int id);
        // Add this new method for vehicle number uniqueness check
        Task<bool> IsVehicleNumberExistsAsync(string vehicleNo, int? excludeId = null);
    }
}
