using EMS.WebApp.Data;
using EMS.WebApp.Data.Migrations;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class DoctorDiagnosisRepository : IDoctorDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly ICompounderIndentRepository _compounderIndentRepo;
        private readonly IEncryptionService _encryptionService;

        public DoctorDiagnosisRepository(
            ApplicationDbContext db,
            ICompounderIndentRepository compounderIndentRepo,
            IEncryptionService encryptionService)
        {
            _db = db;
            _compounderIndentRepo = compounderIndentRepo;
            _encryptionService = encryptionService;
        }

        public async Task<HrEmployee?> GetEmployeeByEmpIdAsync(string empId)
        {
            return await _db.HrEmployees
                .Include(e => e.org_department)
                .Include(e => e.org_plant)
                .FirstOrDefaultAsync(e => e.emp_id.ToLower() == empId.ToLower());
        }

        public async Task<List<RefMedCondition>> GetMedicalConditionsAsync()
        {
            return await _db.RefMedConditions.ToListAsync();
        }

        public async Task<List<int>> GetEmployeeSelectedConditionsAsync(int empUid, DateTime examDate)
        {
            var examDateOnly = DateOnly.FromDateTime(examDate.Date);

            // Find the exam record for this employee and date
            var exam = await _db.MedExamHeaders
                .FirstOrDefaultAsync(e => e.emp_uid == empUid &&
                                       e.exam_date.HasValue &&
                                       e.exam_date.Value == examDateOnly);

            if (exam == null)
                return new List<int>();

            // Get selected condition IDs for this exam
            return await _db.MedExamConditions
                .Where(c => c.exam_id == exam.exam_id)
                .Select(c => c.cond_uid)
                .ToListAsync();
        }

        public async Task<List<MedDisease>> GetDiseasesAsync()
        {
            return await _db.MedDiseases.OrderBy(d => d.DiseaseName).ToListAsync();
        }

        public async Task<List<MedMaster>> GetMedicinesAsync()
        {
            return await _db.med_masters
                .Include(m => m.MedBase)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        public async Task<List<string>> SearchEmployeeIdsAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<string>();

            return await _db.HrEmployees
                .Where(e => e.emp_id.ToLower().Contains(term.ToLower()))
                .OrderBy(e => e.emp_id)
                .Select(e => e.emp_id)
                .Take(10)
                .ToListAsync();
        }

        public async Task<List<DiagnosisEntry>> GetEmployeeDiagnosesAsync(string empId)
        {
            var employee = await GetEmployeeByEmpIdAsync(empId);
            if (employee == null)
                return new List<DiagnosisEntry>();

            // Get actual prescription data
            var prescriptions = await _db.MedPrescriptions
                .Include(p => p.PrescriptionDiseases)
                    .ThenInclude(pd => pd.MedDisease)
                .Where(p => p.emp_uid == employee.emp_uid)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();

            return prescriptions.Select(p => new DiagnosisEntry
            {
                DiagnosisId = p.PrescriptionId,
                DiagnosisName = p.PrescriptionDiseases.Any()
                    ? string.Join(", ", p.PrescriptionDiseases.Select(pd => pd.MedDisease?.DiseaseName))
                    : "General Consultation",
                LastVisitDate = p.PrescriptionDate,
                EmpId = empId
            }).ToList();
        }

        // NEW: Get medicines with batch information and stock, sorted by expiry date
        public async Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync()
        {
            try
            {
                Console.WriteLine("🔍 Getting medicines from compounder indent with batch info and stock");

                var medicineStocks = await _db.CompounderIndentItems
                    .Include(i => i.MedMaster)
                        .ThenInclude(m => m.MedBase)
                    .Include(i => i.CompounderIndent)
                    .Where(i => i.CompounderIndent.Status == "Approved" && // Only from approved indents
                               i.ReceivedQuantity > 0 && // Must have received quantity
                               i.AvailableStock.HasValue && i.AvailableStock.Value > 0 && // Must have available stock > 0
                               !string.IsNullOrEmpty(i.BatchNo) && // Must have batch number
                               i.ExpiryDate.HasValue) // Must have expiry date
                    .Select(i => new MedicineStockInfo
                    {
                        IndentItemId = i.IndentItemId,
                        MedItemId = i.MedItemId,
                        MedItemName = i.MedMaster.MedItemName,
                        CompanyName = i.MedMaster.CompanyName ?? "Not Defined",
                        BatchNo = i.BatchNo,
                        ExpiryDate = i.ExpiryDate,
                        AvailableStock = i.AvailableStock.Value
                    })
                    .ToListAsync();

                // Sort by expiry date (earliest first - medicines expiring soon at top)
                var sortedMedicines = medicineStocks
                    .OrderBy(m => m.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(m => m.MedItemName)
                    .ToList();

                Console.WriteLine($"✅ Found {sortedMedicines.Count} medicines with available stock");

                foreach (var med in sortedMedicines.Take(5)) // Log first 5 for debugging
                {
                    Console.WriteLine($"   - {med.DisplayName}, Stock: {med.AvailableStock}, Expires: {med.ExpiryDateFormatted}, Days to expiry: {med.DaysToExpiry}");
                }

                return sortedMedicines;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting medicines from compounder indent: {ex.Message}");
                // Fallback to all medicines if compounder indent fails
                var fallbackMedicines = await GetMedicinesAsync();
                return fallbackMedicines.Select(m => new MedicineStockInfo
                {
                    IndentItemId = 0, // No specific indent item
                    MedItemId = m.MedItemId,
                    MedItemName = m.MedItemName,
                    CompanyName = m.CompanyName ?? "Not Defined",
                    BatchNo = "N/A",
                    ExpiryDate = null,
                    AvailableStock = 999 // Unlimited stock for fallback
                }).ToList();
            }
        }

        // NEW: Get available stock for a specific medicine batch
        public async Task<int> GetAvailableStockAsync(int indentItemId)
        {
            try
            {
                var item = await _db.CompounderIndentItems
                    .FirstOrDefaultAsync(i => i.IndentItemId == indentItemId);

                return item?.AvailableStock ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting available stock for item {indentItemId}: {ex.Message}");
                return 0;
            }
        }

        // NEW: Update available stock after prescription
        public async Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed)
        {
            try
            {
                Console.WriteLine($"🔄 Updating available stock for item {indentItemId}, using {quantityUsed} units");

                var item = await _db.CompounderIndentItems
                    .FirstOrDefaultAsync(i => i.IndentItemId == indentItemId);

                if (item == null)
                {
                    Console.WriteLine($"❌ Compounder indent item {indentItemId} not found");
                    return false;
                }

                if (!item.AvailableStock.HasValue || item.AvailableStock.Value < quantityUsed)
                {
                    Console.WriteLine($"❌ Insufficient stock. Available: {item.AvailableStock}, Requested: {quantityUsed}");
                    return false;
                }

                var oldStock = item.AvailableStock.Value;
                item.AvailableStock = oldStock - quantityUsed;

                _db.CompounderIndentItems.Update(item);
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ Stock updated for item {indentItemId}: {oldStock} → {item.AvailableStock}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating available stock for item {indentItemId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SavePrescriptionAsync(string empId, DateTime examDate,
             List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
             VitalSigns vitalSigns, string createdBy, string? visitType = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Log visit type for audit purposes
                if (!string.IsNullOrEmpty(visitType))
                {
                    Console.WriteLine($"🏥 Processing {visitType} for employee {empId} by {createdBy}");
                }

                var employee = await GetEmployeeByEmpIdAsync(empId);
                if (employee == null)
                {
                    throw new InvalidOperationException($"Employee with ID '{empId}' not found.");
                }

                var examDateOnly = DateOnly.FromDateTime(examDate.Date);

                // Find or create exam header
                var exam = await _db.MedExamHeaders
                    .FirstOrDefaultAsync(e => e.emp_uid == employee.emp_uid &&
                                           e.exam_date.HasValue &&
                                           e.exam_date.Value == examDateOnly);

                if (exam == null)
                {
                    // Create new exam header if it doesn't exist
                    exam = new MedExamHeader
                    {
                        emp_uid = employee.emp_uid,
                        exam_date = examDateOnly
                    };
                    _db.MedExamHeaders.Add(exam);
                    await _db.SaveChangesAsync(); // Save to get the exam_id
                }

                // NEW: Validate medicine stock before saving prescription
                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        // Check if this is a medicine with batch tracking
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value);
                            if (availableStock < medicine.Quantity)
                            {
                                throw new InvalidOperationException($"Insufficient stock for {medicine.MedicineName}. Available: {availableStock}, Requested: {medicine.Quantity}");
                            }
                        }
                    }
                }

                // Determine approval status and details based on visit type
                var approvalStatus = visitType == "First Aid or Emergency" ? "Pending" : "Approved";
                var approvedBy = approvalStatus == "Approved" ? createdBy : null;
                var approvedDate = approvalStatus == "Approved" ? DateTime.Now : (DateTime?)null;

                // Prepare remarks with visit type
                var remarks = !string.IsNullOrEmpty(visitType) ? $"Visit Type: {visitType}" : string.Empty;

                // Create prescription record with ENCRYPTED vital signs
                var prescription = new MedPrescription
                {
                    emp_uid = employee.emp_uid,
                    exam_id = exam.exam_id,
                    PrescriptionDate = examDate,
                    BloodPressure = _encryptionService.Encrypt(vitalSigns.BloodPressure),
                    Pulse = _encryptionService.Encrypt(vitalSigns.Pulse),
                    Temperature = _encryptionService.Encrypt(vitalSigns.Temperature),
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now,
                    Remarks = remarks,
                    ApprovalStatus = approvalStatus,
                    ApprovedBy = approvedBy,
                    ApprovedDate = approvedDate
                };

                _db.MedPrescriptions.Add(prescription);
                await _db.SaveChangesAsync(); // Save to get the PrescriptionId

                // Save prescription diseases
                if (selectedDiseases?.Any() == true)
                {
                    var prescriptionDiseases = selectedDiseases.Select(diseaseId => new MedPrescriptionDisease
                    {
                        PrescriptionId = prescription.PrescriptionId,
                        DiseaseId = diseaseId
                    }).ToList();

                    _db.MedPrescriptionDiseases.AddRange(prescriptionDiseases);
                }

                // Save prescription medicines with ENCRYPTED medicine names in Instructions AND update stock
                if (medicines?.Any() == true)
                {
                    var prescriptionMedicines = medicines.Select(med => new MedPrescriptionMedicine
                    {
                        PrescriptionId = prescription.PrescriptionId,
                        MedItemId = med.MedItemId,
                        Quantity = med.Quantity,
                        Dose = med.Dose,
                        // Encrypt the medicine name in the instructions field
                        Instructions = $"{_encryptionService.Encrypt(med.MedicineName)} - {med.Dose}"
                    }).ToList();

                    _db.MedPrescriptionMedicines.AddRange(prescriptionMedicines);

                    // NEW: Update available stock for each medicine
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var stockUpdated = await UpdateAvailableStockAsync(medicine.IndentItemId.Value, medicine.Quantity);
                            if (!stockUpdated)
                            {
                                throw new InvalidOperationException($"Failed to update stock for {medicine.MedicineName}");
                            }
                        }
                    }
                }

                // Update or create general exam with ENCRYPTED vital signs
                var generalExam = await _db.MedGeneralExams
                    .FirstOrDefaultAsync(g => g.exam_id == exam.exam_id);

                if (generalExam != null)
                {
                    generalExam.bp = _encryptionService.Encrypt(vitalSigns.BloodPressure);
                    generalExam.pulse = _encryptionService.Encrypt(vitalSigns.Pulse);
                    _db.MedGeneralExams.Update(generalExam);
                }
                else
                {
                    // Create new general exam record
                    generalExam = new MedGeneralExam
                    {
                        emp_uid = employee.emp_uid,
                        exam_id = exam.exam_id,
                        bp = _encryptionService.Encrypt(vitalSigns.BloodPressure),
                        pulse = _encryptionService.Encrypt(vitalSigns.Pulse)
                    };
                    _db.MedGeneralExams.Add(generalExam);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"✅ Prescription saved successfully for {empId} with status: {approvalStatus}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error saving prescription: {ex.Message}");
                throw;
            }
        }

        public async Task<PrescriptionDetailsViewModel?> GetPrescriptionDetailsAsync(int prescriptionId)
        {
            try
            {
                var prescription = await _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                    .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId);

                if (prescription == null)
                    return null;

                var result = new PrescriptionDetailsViewModel
                {
                    PrescriptionId = prescription.PrescriptionId,
                    EmployeeId = prescription.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = prescription.HrEmployee?.emp_name ?? "N/A",
                    Department = prescription.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = prescription.HrEmployee?.org_plant?.plant_name ?? "N/A",
                    PrescriptionDate = prescription.PrescriptionDate,
                    CreatedBy = prescription.CreatedBy,
                    // Decrypt vital signs
                    BloodPressure = _encryptionService.Decrypt(prescription.BloodPressure),
                    Pulse = _encryptionService.Decrypt(prescription.Pulse),
                    Temperature = _encryptionService.Decrypt(prescription.Temperature),
                    Remarks = prescription.Remarks,
                    Diseases = prescription.PrescriptionDiseases?.Select(pd => new PrescriptionDiseaseDetails
                    {
                        DiseaseId = pd.DiseaseId,
                        DiseaseName = pd.MedDisease?.DiseaseName ?? "Unknown Disease",
                        DiseaseDescription = pd.MedDisease?.DiseaseDesc
                    }).ToList() ?? new List<PrescriptionDiseaseDetails>(),
                    Medicines = prescription.PrescriptionMedicines?.Select(pm =>
                    {
                        // Extract and decrypt medicine name from instructions if needed
                        var medicineName = pm.MedMaster?.MedItemName ?? "Unknown Medicine";

                        // If instructions contain encrypted medicine name, decrypt it
                        if (!string.IsNullOrEmpty(pm.Instructions) && pm.Instructions.Contains(" - "))
                        {
                            var parts = pm.Instructions.Split(" - ", 2);
                            if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                            {
                                medicineName = _encryptionService.Decrypt(parts[0]) ?? medicineName;
                            }
                        }

                        return new PrescriptionMedicineDetails
                        {
                            MedItemId = pm.MedItemId,
                            MedicineName = medicineName,
                            Quantity = pm.Quantity,
                            Dose = pm.Dose,
                            Instructions = pm.Instructions,
                            CompanyName = pm.MedMaster?.CompanyName
                        };
                    }).ToList() ?? new List<PrescriptionMedicineDetails>()
                };

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting prescription details: {ex.Message}");
                return null;
            }
        }

        // Keep the existing approval methods unchanged...
        public async Task<int> GetPendingApprovalCountAsync()
        {
            try
            {
                var count = await _db.MedPrescriptions
                    .CountAsync(p => p.ApprovalStatus == "Pending");

                Console.WriteLine($"📊 Pending approval count: {count}");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting pending approval count: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<PendingApprovalViewModel>> GetPendingApprovalsAsync()
        {
            try
            {
                var pendingPrescriptions = await _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                    .Where(p => p.ApprovalStatus == "Pending")
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                Console.WriteLine($"📋 Found {pendingPrescriptions.Count} pending prescriptions for approval");

                return pendingPrescriptions.Select(p => new PendingApprovalViewModel
                {
                    PrescriptionId = p.PrescriptionId,
                    EmployeeId = p.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = p.HrEmployee?.emp_name ?? "N/A",
                    Department = p.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = p.HrEmployee?.org_plant?.plant_name ?? "N/A",
                    PrescriptionDate = p.PrescriptionDate,
                    VisitType = ExtractVisitTypeFromRemarks(p.Remarks) ?? "First Aid or Emergency",
                    CreatedBy = p.CreatedBy,
                    // Decrypt vital signs
                    BloodPressure = _encryptionService.Decrypt(p.BloodPressure),
                    Pulse = _encryptionService.Decrypt(p.Pulse),
                    Temperature = _encryptionService.Decrypt(p.Temperature),
                    ApprovalStatus = p.ApprovalStatus,
                    MedicineCount = p.PrescriptionMedicines?.Count ?? 0,
                    Diseases = p.PrescriptionDiseases?.Select(pd => new PrescriptionDiseaseDetails
                    {
                        DiseaseId = pd.DiseaseId,
                        DiseaseName = pd.MedDisease?.DiseaseName ?? "Unknown Disease",
                        DiseaseDescription = pd.MedDisease?.DiseaseDesc
                    }).ToList() ?? new List<PrescriptionDiseaseDetails>(),
                    Medicines = p.PrescriptionMedicines?.Select(pm =>
                    {
                        // Extract and decrypt medicine name from instructions if needed
                        var medicineName = pm.MedMaster?.MedItemName ?? "Unknown Medicine";

                        // If instructions contain encrypted medicine name, decrypt it
                        if (!string.IsNullOrEmpty(pm.Instructions) && pm.Instructions.Contains(" - "))
                        {
                            var parts = pm.Instructions.Split(" - ", 2);
                            if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                            {
                                medicineName = _encryptionService.Decrypt(parts[0]) ?? medicineName;
                            }
                        }

                        return new PrescriptionMedicineDetails
                        {
                            MedItemId = pm.MedItemId,
                            MedicineName = medicineName,
                            Quantity = pm.Quantity,
                            Dose = pm.Dose,
                            Instructions = pm.Instructions,
                            CompanyName = pm.MedMaster?.CompanyName
                        };
                    }).ToList() ?? new List<PrescriptionMedicineDetails>()
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting pending approvals: {ex.Message}");
                return new List<PendingApprovalViewModel>();
            }
        }

        public async Task<bool> ApprovePrescriptionAsync(int prescriptionId, string approvedBy)
        {
            try
            {
                var prescription = await _db.MedPrescriptions
                    .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId && p.ApprovalStatus == "Pending");

                if (prescription == null)
                {
                    Console.WriteLine($"⚠️ Prescription {prescriptionId} not found or not pending");
                    return false;
                }

                // Update approval fields
                prescription.ApprovalStatus = "Approved";
                prescription.ApprovedBy = approvedBy;
                prescription.ApprovedDate = DateTime.Now;

                _db.MedPrescriptions.Update(prescription);
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ Prescription {prescriptionId} approved by {approvedBy}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error approving prescription {prescriptionId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RejectPrescriptionAsync(int prescriptionId, string rejectionReason, string rejectedBy)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    Console.WriteLine($"⚠️ Rejection reason is required for prescription {prescriptionId}");
                    return false;
                }

                var prescription = await _db.MedPrescriptions
                    .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId && p.ApprovalStatus == "Pending");

                if (prescription == null)
                {
                    Console.WriteLine($"⚠️ Prescription {prescriptionId} not found or not pending");
                    return false;
                }

                // Update rejection fields
                prescription.ApprovalStatus = "Rejected";
                prescription.ApprovedBy = rejectedBy; // Store who rejected it
                prescription.ApprovedDate = DateTime.Now; // Store when it was processed
                prescription.RejectionReason = rejectionReason;

                _db.MedPrescriptions.Update(prescription);
                await _db.SaveChangesAsync();

                Console.WriteLine($"❌ Prescription {prescriptionId} rejected by {rejectedBy}. Reason: {rejectionReason}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error rejecting prescription {prescriptionId}: {ex.Message}");
                return false;
            }
        }

        public async Task<int> ApproveAllPrescriptionsAsync(List<int> prescriptionIds, string approvedBy)
        {
            if (prescriptionIds == null || !prescriptionIds.Any())
            {
                Console.WriteLine($"⚠️ No prescription IDs provided for bulk approval");
                return 0;
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var approvedCount = 0;
                var approvalTimestamp = DateTime.Now;

                var prescriptions = await _db.MedPrescriptions
                    .Where(p => prescriptionIds.Contains(p.PrescriptionId) && p.ApprovalStatus == "Pending")
                    .ToListAsync();

                Console.WriteLine($"📋 Found {prescriptions.Count} valid prescriptions out of {prescriptionIds.Count} requested for bulk approval");

                foreach (var prescription in prescriptions)
                {
                    prescription.ApprovalStatus = "Approved";
                    prescription.ApprovedBy = approvedBy;
                    prescription.ApprovedDate = approvalTimestamp;
                    approvedCount++;
                }

                if (approvedCount > 0)
                {
                    _db.MedPrescriptions.UpdateRange(prescriptions);
                    await _db.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                Console.WriteLine($"✅ {approvedCount} prescriptions approved by {approvedBy}");
                return approvedCount;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error approving multiple prescriptions: {ex.Message}");
                return 0;
            }
        }

        // Helper method to extract visit type from remarks
        private string? ExtractVisitTypeFromRemarks(string? remarks)
        {
            if (string.IsNullOrEmpty(remarks))
                return null;

            if (remarks.Contains("Visit Type: First Aid or Emergency", StringComparison.OrdinalIgnoreCase))
                return "First Aid or Emergency";

            if (remarks.Contains("Visit Type: Regular Visitor", StringComparison.OrdinalIgnoreCase))
                return "Regular Visitor";

            // Fallback - if it's pending, it's likely emergency
            return "First Aid or Emergency";
        }
    }
}