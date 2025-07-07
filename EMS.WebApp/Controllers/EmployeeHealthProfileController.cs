using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class EmployeeHealthProfileController : Controller
    {
        private readonly IHealthProfileRepository _healthProfileRepository;
        private readonly ILogger<EmployeeHealthProfileController> _logger;

        public EmployeeHealthProfileController(
            IHealthProfileRepository healthProfileRepository,
            ILogger<EmployeeHealthProfileController> logger)
        {
            _healthProfileRepository = healthProfileRepository;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var model = new HealthProfileViewModel
                {
                    AvailableExamDates = await _healthProfileRepository.GetAvailableExamDatesAsync()
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Index page");
                return View(new HealthProfileViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeHealthForm(int empNo, DateTime? examDate = null)
        {
            try
            {
                _logger.LogInformation($"Loading health form for employee {empNo}, exam date: {examDate}");

                var model = await _healthProfileRepository.LoadFormData(empNo, examDate);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found");
                    return NotFound("Employee not found.");
                }

                // If no examDate was provided, this is a new entry with current system date
                if (!examDate.HasValue)
                {
                    model.ExamDate = DateTime.Now.Date;
                    model.IsNewEntry = true;
                }
                // If examDate was provided but no data exists, create new entry for that date
                else if (examDate.HasValue && model.IsNewEntry)
                {
                    model.ExamDate = examDate.Value.Date;
                }

                _logger.LogInformation($"Successfully loaded health form for employee {empNo}");
                return PartialView("_HealthFormPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee health form for empNo: {empNo}");
                return BadRequest($"Error loading employee health form: {ex.Message}");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken] // Add this if you're using anti-forgery tokens
        public async Task<IActionResult> SaveHealthForm(HealthProfileViewModel model)
        {
            try
            {
                _logger.LogInformation($"SaveHealthForm called for EmpNo: {model.EmpNo}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }

                    var reloadData = await _healthProfileRepository.LoadFormData(model.EmpNo);

                    // Retain edited values
                    reloadData.SelectedWorkAreaIds = model.SelectedWorkAreaIds ?? new List<int>();
                    reloadData.WorkHistories = model.WorkHistories ?? new List<MedWorkHistory>();
                    reloadData.GeneralExam = model.GeneralExam ?? new MedGeneralExam();
                    reloadData.ExamConditions = model.ExamConditions ?? new List<MedExamCondition>();
                    reloadData.FoodHabit = model.FoodHabit;
                    reloadData.ExamDate = model.ExamDate;
                    reloadData.SelectedConditionIds = model.SelectedConditionIds ?? new List<int>();

                    Response.StatusCode = 400;
                    return PartialView("_HealthFormPartial", reloadData);
                }

                await _healthProfileRepository.SaveFormDataAsync(model);
                _logger.LogInformation($"Successfully saved health form for EmpNo: {model.EmpNo}");

                return Json(new { success = true, message = "Health profile saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving employee health form for EmpNo: {model.EmpNo}");
                return Json(new { success = false, message = "An error occurred while saving." });
            }
        }

 

//[HttpPost]
//[ValidateAntiForgeryToken]
//public async Task<IActionResult> SaveHealthForm(HealthProfileViewModel model)
//{
//    try
//    {
//        _logger.LogInformation($"Saving health form for employee {model.EmpNo}");

//        if (!ModelState.IsValid)
//        {
//            _logger.LogWarning($"Model validation failed for employee {model.EmpNo}");
//            // Return validation errors with the model
//            var updatedModel = await _healthProfileRepository.LoadFormData(model.EmpNo, model.ExamDate);
//            if (updatedModel != null)
//            {
//                // Copy the form data from the submitted model to the loaded model
//                updatedModel.GeneralExam = model.GeneralExam;
//                updatedModel.SelectedWorkAreaIds = model.SelectedWorkAreaIds ?? new List<int>();
//                updatedModel.SelectedConditionIds = model.SelectedConditionIds ?? new List<int>();
//                updatedModel.WorkHistories = model.WorkHistories ?? new List<MedWorkHistory>();
//                updatedModel.FoodHabit = model.FoodHabit;
//            }
//            return PartialView("_HealthFormPartial", updatedModel ?? model);
//        }

//        // If no exam date is provided, use current system date
//        if (!model.ExamDate.HasValue)
//        {
//            model.ExamDate = DateTime.Now.Date;
//        }

//        await _healthProfileRepository.SaveFormDataAsync(model);

//        _logger.LogInformation($"Successfully saved health form for employee {model.EmpNo}");
//        return Json(new { success = true, message = "Health profile saved successfully!" });
//    }
//    catch (Exception ex)
//    {
//        _logger.LogError(ex, $"Error saving health profile for empNo: {model.EmpNo}");
//        return Json(new { success = false, message = $"Error saving health profile: {ex.Message}" });
//    }
//}

[HttpGet]
        public async Task<IActionResult> GetAvailableExamDates(int? empNo = null)
        {
            try
            {
                _logger.LogInformation($"Getting available exam dates for employee {empNo}");

                List<DateTime> dates;

                if (empNo.HasValue)
                {
                    dates = await _healthProfileRepository.GetAvailableExamDatesAsync(empNo.Value);
                }
                else
                {
                    dates = await _healthProfileRepository.GetAvailableExamDatesAsync();
                }

                _logger.LogInformation($"Found {dates.Count} exam dates");
                return Json(dates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting exam dates for empNo: {empNo}");
                return Json(new List<DateTime>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployeeNos(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<string>());
                }

                _logger.LogInformation($"Searching employee numbers with term: {term}");

                var employeeNos = await _healthProfileRepository.GetMatchingEmployeeNosAsync(term);

                _logger.LogInformation($"Found {employeeNos.Count} matching employee numbers");
                return Json(employeeNos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee numbers with term: {term}");
                return Json(new List<string>());
            }
        }
    }
}