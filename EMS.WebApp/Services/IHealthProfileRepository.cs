
using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHealthProfileRepository
    {
        /// <summary>
        /// Loads health-related data for an employee based on EmpNo and optional exam date.
        /// If examDate is null, loads the latest exam data or creates empty form for new entry.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="examDate">Optional exam date to load specific exam data.</param>
        /// <returns>A ViewModel populated with health profile data.</returns>
        Task<HealthProfileViewModel> LoadFormData(int empNo, DateTime? examDate = null);

        /// <summary>
        /// Loads health-related data for an employee based on EmpNo (uses latest exam).
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <returns>A ViewModel populated with health profile data.</returns>
        Task<HealthProfileViewModel> LoadFormData(int empNo);

        /// <summary>
        /// Saves or updates all the related health profile data.
        /// Creates new exam header if none exists for the given date.
        /// </summary>
        /// <param name="model">The health profile view model.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task SaveFormDataAsync(HealthProfileViewModel model);

        /// <summary>
        /// Gets all available exam dates across all employees.
        /// </summary>
        /// <returns>List of available exam dates.</returns>
        Task<List<DateTime>> GetAvailableExamDatesAsync();

        /// <summary>
        /// Gets all available exam dates for a specific employee.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <returns>List of available exam dates for the employee.</returns>
        Task<List<DateTime>> GetAvailableExamDatesAsync(int empNo);

        /// <summary>
        /// Gets matching employee numbers based on search term.
        /// </summary>
        /// <param name="term">Search term for employee numbers.</param>
        /// <returns>List of matching employee numbers as strings.</returns>
        Task<List<string>> GetMatchingEmployeeNosAsync(string term);
    }
}