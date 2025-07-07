using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class HealthProfileRepository : IHealthProfileRepository
    {
        private readonly ApplicationDbContext _db;

        public HealthProfileRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HealthProfileViewModel> LoadFormData(int empNo, DateTime? examDate = null)
        {
            // Check if employee exists
            var employee = await _db.HrEmployees.FirstOrDefaultAsync(e => e.emp_uid == empNo);
            if (employee == null)
            {
                return null; // Employee not found
            }

            MedExamHeader exam = null;
            int examId = 0;
            DateTime currentExamDate = DateTime.Now.Date;
            bool isNewEntry = false;

            // If examDate is provided, try to find the specific exam record
            if (examDate.HasValue)
            {
                // Convert DateTime to DateOnly for comparison
                var examDateOnly = DateOnly.FromDateTime(examDate.Value.Date);

                exam = await _db.MedExamHeaders
                    .FirstOrDefaultAsync(e => e.emp_uid == empNo &&
                                           e.exam_date.HasValue &&
                                           e.exam_date.Value == examDateOnly);

                currentExamDate = examDate.Value.Date;

                // If no exam found for the specific date, this is a new entry for that date
                if (exam == null)
                {
                    isNewEntry = true;
                }
            }
            else
            {
                // No examDate provided - this is a new entry with current system date
                isNewEntry = true;
                currentExamDate = DateTime.Now.Date;
            }

            // Set examId if exam exists
            if (exam != null && !isNewEntry)
            {
                examId = exam.exam_id;
            }

            // Get all reference data (always needed)
            var allWorkAreas = await _db.RefWorkAreas.ToListAsync();
            var allMedConditions = await _db.RefMedConditions.ToListAsync();
            var dependents = await _db.HrEmployeeDependents
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();
            var employeeDetails = await _db.HrEmployees
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();

            // Initialize with empty data for new entries
            var selectedAreaIds = new List<int>();
            var selectedConditionIds = new List<int>();
            var examConditions = new List<MedExamCondition>();
            var generalExam = new MedGeneralExam { emp_uid = empNo };
            var workHistories = new List<MedWorkHistory>();
            string foodHabit = null;

            // Only load existing data if not a new entry
            if (examId > 0 && !isNewEntry)
            {
                selectedAreaIds = await _db.MedExamWorkAreas
                    .Where(m => m.exam_id == examId)
                    .Select(m => m.area_uid)
                    .ToListAsync();

                selectedConditionIds = await _db.MedExamConditions
                    .Where(c => c.exam_id == examId)
                    .Select(c => c.cond_uid)
                    .ToListAsync();

                examConditions = await _db.MedExamConditions
                    .Where(c => c.exam_id == examId)
                    .Include(c => c.RefMedCondition)
                    .ToListAsync();

                var existingGeneralExam = await _db.MedGeneralExams
                    .FirstOrDefaultAsync(g => g.emp_uid == empNo && g.exam_id == examId);

                if (existingGeneralExam != null)
                {
                    generalExam = existingGeneralExam;
                }
                else
                {
                    generalExam.exam_id = examId;
                }

                workHistories = await _db.MedWorkHistories
                    .Where(w => w.emp_uid == empNo && w.exam_id == examId)
                    .ToListAsync();

                foodHabit = exam?.food_habit;
            }

            // If no work histories exist or it's a new entry, add an empty one for the form
            if (!workHistories.Any())
            {
                workHistories.Add(new MedWorkHistory
                {
                    emp_uid = empNo,
                    exam_id = examId
                });
            }

            var viewModel = new HealthProfileViewModel
            {
                EmpNo = empNo,
                ExamDate = currentExamDate,
                FoodHabit = foodHabit,
                IsNewEntry = isNewEntry,

                ReferenceWorkAreas = allWorkAreas,
                SelectedWorkAreaIds = selectedAreaIds,

                MedConditions = allMedConditions,
                SelectedConditionIds = selectedConditionIds,

                Dependents = dependents,
                EmployeeDetails = employeeDetails,
                ExamConditions = examConditions,
                GeneralExam = generalExam,
                WorkHistories = workHistories
            };

            // Get all available exam dates for this employee
            // Load all exam headers first, then convert DateOnly to DateTime in memory
            var examHeaders = await _db.MedExamHeaders
                .Where(e => e.emp_uid == empNo && e.exam_date.HasValue)
                .OrderByDescending(e => e.exam_date)
                .ToListAsync(); // Bring to memory first

            viewModel.AvailableExamDates = examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .ToList();

            return viewModel;
        }

        public async Task<HealthProfileViewModel> LoadFormData(int empNo)
        {
            return await LoadFormData(empNo, null);
        }

        public async Task<List<DateTime>> GetAvailableExamDatesAsync()
        {
            // Load exam headers first, then convert DateOnly to DateTime in memory
            var examHeaders = await _db.MedExamHeaders
                .Where(e => e.exam_date.HasValue)
                .OrderByDescending(e => e.exam_date)
                .ToListAsync(); // Bring to memory first

            return examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();
        }

        public async Task<List<DateTime>> GetAvailableExamDatesAsync(int empNo)
        {
            // Load exam headers first, then convert DateOnly to DateTime in memory
            var examHeaders = await _db.MedExamHeaders
                .Where(e => e.emp_uid == empNo && e.exam_date.HasValue)
                .OrderByDescending(e => e.exam_date)
                .ToListAsync(); // Bring to memory first

            return examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();
        }

        public async Task<List<string>> GetMatchingEmployeeNosAsync(string term)
        {
            return await _db.HrEmployees
                .Where(e => e.emp_uid.ToString().StartsWith(term))
                .OrderBy(e => e.emp_uid)
                .Select(e => e.emp_uid.ToString())
                .Take(10)
                .ToListAsync();
        }

        public async Task SaveFormDataAsync(HealthProfileViewModel model)
        {
            // Use current system date if no exam date is provided
            var examDate = model.ExamDate ?? DateTime.Now.Date;
            var examDateOnly = DateOnly.FromDateTime(examDate);

            // Try to find existing exam for this employee and date
            var exam = await _db.MedExamHeaders
                .FirstOrDefaultAsync(e => e.emp_uid == model.EmpNo &&
                                       e.exam_date.HasValue &&
                                       e.exam_date.Value == examDateOnly);

            if (exam == null)
            {
                // Create new exam header
                exam = new MedExamHeader
                {
                    emp_uid = model.EmpNo,
                    exam_date = examDateOnly,
                    food_habit = model.FoodHabit
                };
                _db.MedExamHeaders.Add(exam);
                await _db.SaveChangesAsync(); // Save to get the exam_id
            }
            else
            {
                // Update existing exam header
                exam.food_habit = model.FoodHabit;
                _db.MedExamHeaders.Update(exam);
                await _db.SaveChangesAsync();
            }

            // Handle General Exam
            var existingGenExam = await _db.MedGeneralExams
                .FirstOrDefaultAsync(g => g.emp_uid == model.EmpNo && g.exam_id == exam.exam_id);

            if (existingGenExam != null)
            {
                // Update existing general exam
                existingGenExam.bp = model.GeneralExam.bp;
                existingGenExam.height_cm = model.GeneralExam.height_cm;
                existingGenExam.weight_kg = model.GeneralExam.weight_kg;
                existingGenExam.pulse = model.GeneralExam.pulse;
                existingGenExam.respiratory = model.GeneralExam.respiratory;
                existingGenExam.cns = model.GeneralExam.cns;
                existingGenExam.abdomen = model.GeneralExam.abdomen;
                existingGenExam.bmi = model.GeneralExam.bmi;
                existingGenExam.cvs = model.GeneralExam.cvs;
                existingGenExam.genito_urinary = model.GeneralExam.genito_urinary;
                existingGenExam.remarks = model.GeneralExam.remarks;
                existingGenExam.rr = model.GeneralExam.rr;
                existingGenExam.skin = model.GeneralExam.skin;
                existingGenExam.ent = model.GeneralExam.ent;
                existingGenExam.opthal = model.GeneralExam.opthal;
                existingGenExam.others = model.GeneralExam.others;
                _db.MedGeneralExams.Update(existingGenExam);
            }
            else
            {
                // Create new general exam
                model.GeneralExam.emp_uid = model.EmpNo;
                model.GeneralExam.exam_id = exam.exam_id;
                _db.MedGeneralExams.Add(model.GeneralExam);
            }

            // Handle Medical Conditions
            var existingConditions = await _db.MedExamConditions
                .Where(c => c.exam_id == exam.exam_id)
                .ToListAsync();
            _db.MedExamConditions.RemoveRange(existingConditions);

            if (model.SelectedConditionIds?.Any() == true)
            {
                foreach (var condId in model.SelectedConditionIds.Distinct())
                {
                    _db.MedExamConditions.Add(new MedExamCondition
                    {
                        exam_id = exam.exam_id,
                        cond_uid = condId,
                        present = true
                    });
                }
            }

            // Handle Work Areas
            var existingWorkAreas = await _db.MedExamWorkAreas
                .Where(w => w.exam_id == exam.exam_id)
                .ToListAsync();
            _db.MedExamWorkAreas.RemoveRange(existingWorkAreas);

            if (model.SelectedWorkAreaIds?.Any() == true)
            {
                foreach (var areaId in model.SelectedWorkAreaIds.Distinct())
                {
                    _db.MedExamWorkAreas.Add(new MedExamWorkArea
                    {
                        exam_id = exam.exam_id,
                        area_uid = areaId
                    });
                }
            }

            // Handle Work Histories
            if (model.WorkHistories?.Any() == true)
            {
                // Remove existing work histories for this exam
                var existingWorkHistories = await _db.MedWorkHistories
                    .Where(w => w.emp_uid == model.EmpNo && w.exam_id == exam.exam_id)
                    .ToListAsync();
                _db.MedWorkHistories.RemoveRange(existingWorkHistories);

                // Add new work histories (only non-empty job names)
                foreach (var wh in model.WorkHistories.Where(w => !string.IsNullOrWhiteSpace(w.job_name)))
                {
                    wh.emp_uid = model.EmpNo;
                    wh.exam_id = exam.exam_id;
                    wh.work_uid = 0; // Reset to 0 for new entry
                    _db.MedWorkHistories.Add(wh);
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}