using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;  // Added for caching
using System.Linq;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class SysAttachScreenRoleController : Controller
    {
        private readonly ISysAttachScreenRoleRepository _repo;
        private readonly IMemoryCache _cache;  // Added cache field

        // Inject repo and cache
        public SysAttachScreenRoleController(ISysAttachScreenRoleRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        // GET: /SysAttachScreenRole
        public IActionResult Index() => View();

        // AJAX for DataTable
        public async Task<IActionResult> LoadData()
        {
            var list = await _repo.ListWithBaseAsync();
            var screenList = await _repo.GetScreenListAsync();

            var result = list.Select(x => new
            {
                x.uid,
                role_name = x.SysRole?.role_name ?? "",
                screen_names = string.Join(", ",
                    x.screen_uids.Select(id =>
                        screenList.FirstOrDefault(s => s.screen_uid == id)?.screen_name ?? $"[ID:{id}]"
                    )
                )
            });

            return Json(new { data = result });
        }

        // GET: create form partial
        public async Task<IActionResult> Create()
        {
            ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name");
            ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name");

            return PartialView("_CreateEdit", new SysAttachScreenRole());
        }

        // POST: create with rate limiting
        [HttpPost]
        public async Task<IActionResult> Create(SysAttachScreenRole model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }

            // Rate limiting logic starts here
            var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            var cacheKey = $"rate_limit_create_attachscreenrole_{userId}";

            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                return new List<DateTime>();
            });

            timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

            if (timestamps.Count >= 5)
            {
                ViewBag.Error = "⚠ You can only create 5 screen-role assignments every 5 minutes. Please wait and try again.";
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }

            timestamps.Add(DateTime.UtcNow);
            _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));
            // Rate limiting logic ends here

            // Optional: Check if this role already exists
            var exists = await _repo.ExistsRoleAsync(model.role_uid);
            if (exists)
            {
                ModelState.AddModelError("", "This role is already assigned.");
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }

            await _repo.AddAsync(model);
            return Json(new { success = true });
        }

        // GET: edit form partial
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _repo.GetByIdWithBaseAsync(id);
            if (item == null) return NotFound();

            ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", item.role_uid);
            ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", item.screen_uids);

            return PartialView("_CreateEdit", item);
        }

        // POST: edit
        [HttpPost]
        public async Task<IActionResult> Edit(SysAttachScreenRole model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }

            await _repo.UpdateAsync(model);
            return Json(new { success = true });
        }

        // POST: delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            //// Rate limiting logic starts here
            //var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            //var cacheKey = $"rate_limit_delete_attachscreenrole_{userId}";

            //var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            //{
            //    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            //    return new List<DateTime>();
            //});

            //timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

            //if (timestamps.Count >= 5)
            //{
            //    return Json(new
            //    {
            //        success = false,
            //        error = "⚠ You can only delete 5 screen-role assignments every 5 minutes. Please wait and try again."
            //    });
            //}

            //timestamps.Add(DateTime.UtcNow);
            //_cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));
            //// Rate limiting logic ends here
            await _repo.DeleteAsync(id);
            return Json(new { success = true });
        }

        // GET: /SysAttachScreenRole/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var item = await _repo.GetByIdWithBaseAsync(id);
            if (item == null) return NotFound();

            var screenNames = await _repo.GetScreenNamesFromCsvAsync(item.screen_uid);
            ViewBag.ScreenNames = screenNames;

            return PartialView("_View", item);
        }
    }
}
