using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class HrEmployeeController : Controller
    {
        private readonly IHrEmployeeRepository _repo;
        private readonly IMemoryCache _cache;

        public HrEmployeeController(IHrEmployeeRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListWithBaseAsync();

                var result = list.Select(x => new
                {
                    x.emp_uid,
                    x.emp_id,
                    x.emp_name,
                    x.emp_DOB,
                    x.emp_Gender,
                    x.emp_Grade,
                    dept_name = x.org_department != null ? x.org_department.dept_name : "",
                    plant_name = x.org_plant != null ? x.org_plant.plant_name : "",
                    x.emp_blood_Group
                });

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            var departmentList = await _repo.GetDepartmentListAsync();
            var plantList = await _repo.GetPlantListAsync();

            ViewBag.OrgDepartmentList = new SelectList(departmentList, "dept_id", "dept_name");
            ViewBag.OrgPlantList = new SelectList(plantList, "plant_id", "plant_name");

            return PartialView("_CreateEdit", new HrEmployee());
        }

        [HttpPost]
        public async Task<IActionResult> Create(HrEmployee model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate employee ID
                if (await _repo.IsEmployeeIdExistsAsync(model.emp_id))
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists. Please choose a different ID.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_hremployee_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 employees every 5 minutes. Please wait and try again.";
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.AddAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_HrEmployee_EmpId_Unique") == true)
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists. Please choose a different ID.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the employee. Please try again.";
                }

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", item.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", item.plant_id);

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(HrEmployee model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate employee ID (excluding current record)
                if (await _repo.IsEmployeeIdExistsAsync(model.emp_id, model.emp_uid))
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists. Please choose a different ID.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_hremployee_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 employees every 5 minutes. Please wait and try again.";
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.UpdateAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_HrEmployee_EmpId_Unique") == true)
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists. Please choose a different ID.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the employee. Please try again.";
                }

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(), "dept_id", "dept_name", model.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(), "plant_id", "plant_name", model.plant_id);
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _repo.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while deleting the employee." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();
                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckEmployeeIdExists(string empId, int? empUid = null)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return Json(new { exists = false });

            // Sanitize input before checking
            empId = SanitizeString(empId);

            var exists = await _repo.IsEmployeeIdExistsAsync(empId, empUid);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private HrEmployee SanitizeInput(HrEmployee model)
        {
            if (model == null) return model;

            model.emp_id = SanitizeString(model.emp_id);
            model.emp_name = SanitizeString(model.emp_name);
            model.emp_Grade = SanitizeString(model.emp_Grade);
            model.emp_blood_Group = SanitizeString(model.emp_blood_Group);

            return model;
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // HTML encode the input to prevent XSS
            input = HttpUtility.HtmlEncode(input);

            // Remove or replace potentially dangerous characters
            input = Regex.Replace(input, @"[<>""'&]", "", RegexOptions.IgnoreCase);

            // Remove script tags and javascript
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"vbscript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            return input.Trim();
        }

        private bool IsInputSecure(HrEmployee model)
        {
            if (model == null) return false;

            // Check for potentially dangerous patterns
            var dangerousPatterns = new[]
            {
                @"<script",
                @"</script>",
                @"javascript:",
                @"vbscript:",
                @"on\w+\s*=",
                @"eval\s*\(",
                @"expression\s*\(",
                @"<iframe",
                @"<object",
                @"<embed",
                @"<form",
                @"<input"
            };

            var inputsToCheck = new[] { model.emp_id, model.emp_name, model.emp_Grade, model.emp_blood_Group };

            foreach (var input in inputsToCheck)
            {
                if (string.IsNullOrEmpty(input)) continue;

                foreach (var pattern in dangerousPatterns)
                {
                    if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion
    }
}