using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISysUserRepository
    {

        Task<SysUser?> GetByIdWithBaseAsync(int id);
        Task<IEnumerable<SysUser>> ListWithBaseAsync();
        Task<IEnumerable<SysRole>> GetBaseListAsync();



        Task<List<SysUser>> ListAsync();
        Task<SysUser> GetByIdAsync(int id);
        Task AddAsync(SysUser u);
        Task UpdateAsync(SysUser u);
        Task DeleteAsync(int id);
    }
}
