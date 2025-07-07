using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class AccountLoginRepository :IAccountLoginRepository
    {
        private readonly ApplicationDbContext _db;
        public AccountLoginRepository(ApplicationDbContext db) => _db = db;
        public async Task<AccountLogin?> GetByAdidAsync(string adid)
        {
            return await _db.AccountLogin
                .FirstOrDefaultAsync(u => u.user_name == adid && u.is_active);
        }


        public async Task<AccountLogin?> GetByEmailAndPasswordAsync(string user_name, string password)
        {
            return await _db.AccountLogin
                .FirstOrDefaultAsync(u => u.user_name == user_name && u.password == password && u.is_active);
        }

        public async Task<AccountLogin?> GetByEmailAsync(string user_name)
        {
            return await _db.AccountLogin.FirstOrDefaultAsync(u => u.user_name == user_name && u.is_active);
        }

        public async Task UpdateAsync(AccountLogin user)
        {
            _db.AccountLogin.Update(user);
            await _db.SaveChangesAsync();
        }
    }
}
