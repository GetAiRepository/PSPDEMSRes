using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IAccountLoginRepository
    {
        Task<AccountLogin?> GetByAdidAsync(string adid);

        Task<AccountLogin?> GetByEmailAndPasswordAsync(string user_name, string password);
        Task<AccountLogin?> GetByEmailAsync(string user_name);

        Task UpdateAsync(AccountLogin user);

    }
}
