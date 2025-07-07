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
    public class HrEmployeeDependentController : Controller
    {
        private readonly IHrEmployeeDependentRepository _repo;
        private readonly IMemoryCache _cache;

        public HrEmployeeDependentController(IHrEmployeeDependentRepository repo, IMemoryCache cache)
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

                var result = list.Select(x => new {
                    x.emp_dep_id,
                    emp_name = x.HrEmployee != null ? x.HrEmployee.emp_name : "",
                    x.dep_name,
                    x.dep_dob,
                    x.relation,
                    x.gender,
                    x.is_active,
                    x.marital_status
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
            try
            {
                var empDependent = await _repo.GetBaseListAsync();

                if (!empDependent.Any())
                {
                    ViewBag.EmpDependentList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No employees found!";
                }
                else
                {
                    ViewBag.EmpDependentList = new SelectList(empDependent, "emp_uid", "emp_name");
                }

                return PartialView("_CreateEdit", new HrEmployeeDependent());
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error loading employee list.";
                return PartialView("_CreateEdit", new HrEmployeeDependent());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(HrEmployeeDependent model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Validate date of birth
                if (model.dep_dob.HasValue)
                {
                    var dobDate = model.dep_dob.Value.ToDateTime(TimeOnly.MinValue);
                    var today = DateTime.Now;
                    var age = today.Year - dobDate.Year;
                    if (dobDate > today.AddYears(-age)) age--;

                    if (dobDate > today)
                    {
                        ModelState.AddModelError("dep_dob", "Date of Birth cannot be in the future.");
                    }
                    else if (age > 100)
                    {
                        ModelState.AddModelError("dep_dob", "Age cannot exceed 100 years.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_empdependent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    ViewBag.Error = "⚠ You can only create 5 dependents every 5 minutes. Please wait and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.AddAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while creating the dependent record. Please try again.";
                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null) return NotFound();

                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", item.emp_uid);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(HrEmployeeDependent model)
        {
            try
            {
                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Validate date of birth
                if (model.dep_dob.HasValue)
                {
                    var dobDate = model.dep_dob.Value.ToDateTime(TimeOnly.MinValue);
                    var today = DateTime.Now;
                    var age = today.Year - dobDate.Year;
                    if (dobDate > today.AddYears(-age)) age--;

                    if (dobDate > today)
                    {
                        ModelState.AddModelError("dep_dob", "Date of Birth cannot be in the future.");
                    }
                    else if (age > 100)
                    {
                        ModelState.AddModelError("dep_dob", "Age cannot exceed 100 years.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_empdependent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    ViewBag.Error = "⚠ You can only edit 10 dependents every 5 minutes. Please wait and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                await _repo.UpdateAsync(model);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while updating the dependent record. Please try again.";
                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(), "emp_uid", "emp_name", model.emp_uid);
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
                return Json(new { success = false, message = "An error occurred while deleting the dependent record." });
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

        #region Private Methods for Input Sanitization and Validation

        private HrEmployeeDependent SanitizeInput(HrEmployeeDependent model)
        {
            if (model == null) return model;

            model.dep_name = SanitizeString(model.dep_name);
            model.relation = SanitizeString(model.relation);

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

        private bool IsInputSecure(HrEmployeeDependent model)
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

            var inputsToCheck = new[] { model.dep_name, model.relation };

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