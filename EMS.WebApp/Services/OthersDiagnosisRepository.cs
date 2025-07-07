using EMS.WebApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class OthersDiagnosisRepository : IOthersDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IEncryptionService _encryptionService;

        public OthersDiagnosisRepository(ApplicationDbContext db, IEncryptionService encryptionService)
        {
            _db = db;
            _encryptionService = encryptionService;
        }

        // ======= NEW: Get medicines with batch information and stock, sorted by expiry date =======
        public async Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync()
        {
            try
            {
                Console.WriteLine("🔍 Getting medicines from compounder indent with batch info and stock for Others Diagnosis");

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

                Console.WriteLine($"✅ Found {sortedMedicines.Count} medicines with available stock for Others Diagnosis");

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

        // ======= NEW: Get available stock for a specific medicine batch =======
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

        // ======= NEW: Update available stock after prescription =======
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

        // ======= UPDATED: SaveDiagnosisAsync with batch tracking and stock management =======
        public async Task<(bool Success, string ErrorMessage)> SaveDiagnosisAsync(OthersDiagnosisViewModel model, string createdBy)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine("=== SaveDiagnosisAsync Repository Debug ===");
                Console.WriteLine($"Model received - TreatmentId: {model.TreatmentId}");
                Console.WriteLine($"Model received - PatientName: {model.PatientName}");
                Console.WriteLine($"Model received - Age: {model.Age}");
                Console.WriteLine($"Model received - DiagnosedBy: {model.DiagnosedBy}");
                Console.WriteLine($"Model received - VisitType: {model.VisitType}");
                Console.WriteLine($"Model received - Diseases Count: {model.SelectedDiseaseIds?.Count ?? 0}");
                Console.WriteLine($"Model received - Medicines Count: {model.PrescriptionMedicines?.Count ?? 0}");

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.TreatmentId))
                    return (false, "Treatment ID is required");

                if (string.IsNullOrWhiteSpace(model.PatientName))
                    return (false, "Patient name is required");

                if (model.Age <= 0 || model.Age > 120)
                    return (false, "Valid age is required");

                if (string.IsNullOrWhiteSpace(model.DiagnosedBy))
                    return (false, "Diagnosed By is required");

                // Validate visit type
                if (string.IsNullOrWhiteSpace(model.VisitType))
                    model.VisitType = "Regular Visitor";

                // ======= NEW: Validate medicine stock before saving =======
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        // Check if this is a medicine with batch tracking
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value);
                            if (availableStock < medicine.Quantity)
                            {
                                return (false, $"Insufficient stock for {medicine.MedicineName}. Available: {availableStock}, Requested: {medicine.Quantity}");
                            }
                        }
                    }
                }

                OtherPatient? patient;

                if (model.PatientId.HasValue)
                {
                    // Existing patient
                    patient = await _db.OtherPatients.FindAsync(model.PatientId.Value);
                    if (patient == null)
                        return (false, $"Patient with ID {model.PatientId} not found");

                    // Update patient info
                    patient.PatientName = model.PatientName;
                    patient.Age = model.Age;
                    patient.PNumber = model.PNumber ?? string.Empty;
                    patient.Category = model.Category ?? string.Empty;
                    patient.OtherDetails = model.OtherDetails;
                    Console.WriteLine($"Updated existing patient: {patient.PatientId}");
                }
                else
                {
                    // Check if patient with this Treatment ID already exists
                    patient = await _db.OtherPatients
                        .FirstOrDefaultAsync(p => p.TreatmentId == model.TreatmentId);

                    if (patient != null)
                    {
                        // Update existing patient
                        patient.PatientName = model.PatientName;
                        patient.Age = model.Age;
                        patient.PNumber = model.PNumber ?? string.Empty;
                        patient.Category = model.Category ?? string.Empty;
                        patient.OtherDetails = model.OtherDetails;
                        Console.WriteLine($"Updated existing patient by TreatmentId: {patient.PatientId}");
                    }
                    else
                    {
                        // New patient
                        patient = new OtherPatient
                        {
                            TreatmentId = model.TreatmentId,
                            PatientName = model.PatientName,
                            Age = model.Age,
                            PNumber = model.PNumber ?? string.Empty,
                            Category = model.Category ?? string.Empty,
                            OtherDetails = model.OtherDetails,
                            CreatedBy = createdBy,
                            CreatedDate = DateTime.Now
                        };
                        _db.OtherPatients.Add(patient);

                        try
                        {
                            await _db.SaveChangesAsync();
                            Console.WriteLine($"Created new patient with ID: {patient.PatientId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating patient: {ex.Message}");
                            return (false, $"Error creating patient: {ex.Message}");
                        }
                    }
                }

                // Determine approval status based on visit type
                string approvalStatus = model.VisitType == "First Aid or Emergency" ? "Pending" : "Approved";
                string? approvedBy = model.VisitType == "Regular Visitor" ? createdBy : null;
                DateTime? approvedDate = model.VisitType == "Regular Visitor" ? DateTime.Now : null;

                Console.WriteLine($"Visit Type: {model.VisitType}, Approval Status: {approvalStatus}");

                // Create diagnosis record with ENCRYPTED vital signs
                var diagnosis = new OthersDiagnosis
                {
                    PatientId = patient.PatientId,
                    VisitDate = DateTime.Now,
                    LastVisitDate = model.LastVisitDate,
                    // ENCRYPT vital signs
                    BloodPressure = _encryptionService.Encrypt(model.BloodPressure),
                    PulseRate = _encryptionService.Encrypt(model.PulseRate),
                    Sugar = _encryptionService.Encrypt(model.Sugar),
                    Remarks = model.Remarks,
                    DiagnosedBy = model.DiagnosedBy,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now,
                    VisitType = model.VisitType,
                    ApprovalStatus = approvalStatus,
                    ApprovedBy = approvedBy,
                    ApprovedDate = approvedDate
                };

                _db.OthersDiagnoses.Add(diagnosis);

                try
                {
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Created diagnosis with ID: {diagnosis.DiagnosisId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating diagnosis: {ex.Message}");
                    return (false, $"Error creating diagnosis: {ex.Message}");
                }

                // Save diagnosed diseases
                if (model.SelectedDiseaseIds?.Any() == true)
                {
                    Console.WriteLine($"Processing {model.SelectedDiseaseIds.Count} diseases");
                    var diagnosisDiseases = model.SelectedDiseaseIds.Select(diseaseId =>
                        new OthersDiagnosisDisease
                        {
                            DiagnosisId = diagnosis.DiagnosisId,
                            DiseaseId = diseaseId
                        }).ToList();

                    _db.OthersDiagnosisDiseases.AddRange(diagnosisDiseases);

                    try
                    {
                        await _db.SaveChangesAsync();
                        Console.WriteLine($"Successfully saved {diagnosisDiseases.Count} diseases");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving diseases: {ex.Message}");
                        return (false, $"Error saving diseases: {ex.Message}");
                    }
                }

                // ======= UPDATED: Save prescribed medicines with ENCRYPTED medicine names AND update stock =======
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    Console.WriteLine($"Processing {model.PrescriptionMedicines.Count} medicines");

                    // Validate that all MedItemIds exist in the database
                    var medItemIds = model.PrescriptionMedicines.Select(m => m.MedItemId).ToList();
                    Console.WriteLine($"Medicine IDs to validate: [{string.Join(", ", medItemIds)}]");

                    var existingMedicines = await _db.med_masters
                        .Where(m => medItemIds.Contains(m.MedItemId))
                        .Select(m => new { m.MedItemId, m.MedItemName })
                        .ToListAsync();

                    Console.WriteLine($"Found {existingMedicines.Count} existing medicines in database:");
                    foreach (var em in existingMedicines)
                    {
                        Console.WriteLine($"  - MedItemId: {em.MedItemId}, Name: {em.MedItemName}");
                    }

                    var existingMedItemIds = existingMedicines.Select(m => m.MedItemId).ToList();
                    var missingMedicines = medItemIds.Except(existingMedItemIds).ToList();

                    if (missingMedicines.Any())
                    {
                        Console.WriteLine($"Missing medicine IDs: [{string.Join(", ", missingMedicines)}]");
                        return (false, $"Invalid medicine IDs: {string.Join(", ", missingMedicines)}");
                    }

                    var diagnosisMedicines = new List<OthersDiagnosisMedicine>();

                    foreach (var med in model.PrescriptionMedicines)
                    {
                        // Encrypt medicine name in instructions field
                        var encryptedMedicineName = _encryptionService.Encrypt(med.MedicineName);
                        var instructions = !string.IsNullOrEmpty(med.Instructions)
                            ? $"{encryptedMedicineName} - {med.Instructions}"
                            : $"{encryptedMedicineName} - {med.Dose}";

                        var diagnosisMedicine = new OthersDiagnosisMedicine
                        {
                            DiagnosisId = diagnosis.DiagnosisId,
                            MedItemId = med.MedItemId,
                            Quantity = med.Quantity,
                            Dose = med.Dose ?? string.Empty,
                            Instructions = instructions // Contains encrypted medicine name
                        };

                        diagnosisMedicines.Add(diagnosisMedicine);
                        Console.WriteLine($"Prepared medicine: DiagnosisId={diagnosisMedicine.DiagnosisId}, MedItemId={diagnosisMedicine.MedItemId}, Qty={diagnosisMedicine.Quantity}, Dose='{diagnosisMedicine.Dose}'");
                    }

                    _db.OthersDiagnosisMedicines.AddRange(diagnosisMedicines);

                    try
                    {
                        await _db.SaveChangesAsync();
                        Console.WriteLine($"Successfully saved {diagnosisMedicines.Count} medicines to database");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving medicines: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        return (false, $"Error saving medicines: {ex.Message}");
                    }

                    // ======= NEW: Update available stock for each medicine =======
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var stockUpdated = await UpdateAvailableStockAsync(medicine.IndentItemId.Value, medicine.Quantity);
                            if (!stockUpdated)
                            {
                                return (false, $"Failed to update stock for {medicine.MedicineName}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No medicines to save");
                }

                await transaction.CommitAsync();
                Console.WriteLine("Transaction committed successfully");

                // Return different messages based on approval status
                string successMessage = approvalStatus == "Pending"
                    ? "Emergency diagnosis saved and sent for doctor approval."
                    : "Diagnosis saved successfully.";

                return (true, successMessage);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Unexpected error in SaveDiagnosisAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, $"Unexpected error: {ex.Message}");
            }
        }

        // ======= KEEP ALL EXISTING METHODS UNCHANGED =======

        public async Task<List<OthersDiagnosisListViewModel>> GetAllDiagnosesAsync()
        {
            return await _db.OthersDiagnoses
                .Include(d => d.Patient)
                .OrderByDescending(d => d.VisitDate)
                .Select(d => new OthersDiagnosisListViewModel
                {
                    DiagnosisId = d.DiagnosisId,
                    TreatmentId = d.Patient!.TreatmentId,
                    PatientName = d.Patient.PatientName,
                    Age = d.Patient.Age,
                    Category = d.Patient.Category,
                    VisitDate = d.VisitDate,
                    DiagnosedBy = d.DiagnosedBy,
                    VisitType = d.VisitType,
                    ApprovalStatus = d.ApprovalStatus
                })
                .ToListAsync();
        }

        public async Task<string> GenerateNewTreatmentIdAsync()
        {
            var currentYear = DateTime.Now.Year.ToString().Substring(2);
            var prefix = $"TID-{currentYear}";

            var lastId = await _db.OtherPatients
                .Where(p => p.TreatmentId.StartsWith(prefix))
                .OrderByDescending(p => p.TreatmentId)
                .Select(p => p.TreatmentId)
                .FirstOrDefaultAsync();

            if (lastId == null)
            {
                return $"{prefix}001";
            }

            var numberPart = lastId.Substring(prefix.Length);
            if (int.TryParse(numberPart, out int lastNumber))
            {
                return $"{prefix}{(lastNumber + 1):D3}";
            }

            return $"{prefix}001";
        }

        public async Task<OtherPatient?> GetPatientByTreatmentIdAsync(string treatmentId)
        {
            return await _db.OtherPatients
                .Include(p => p.Diagnoses.OrderByDescending(d => d.VisitDate).Take(1))
                .FirstOrDefaultAsync(p => p.TreatmentId == treatmentId);
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

        public async Task<OthersDiagnosisDetailsViewModel?> GetDiagnosisDetailsAsync(int diagnosisId)
        {
            try
            {
                Console.WriteLine($"=== GetDiagnosisDetailsAsync for ID: {diagnosisId} ===");

                // Get the basic diagnosis info first
                var diagnosis = await _db.OthersDiagnoses
                    .Include(d => d.Patient)
                    .FirstOrDefaultAsync(d => d.DiagnosisId == diagnosisId);

                if (diagnosis == null)
                {
                    Console.WriteLine("Diagnosis not found");
                    return null;
                }

                Console.WriteLine($"Found diagnosis for patient: {diagnosis.Patient?.PatientName}");

                // Get diseases using explicit join
                var diseases = await (from dd in _db.OthersDiagnosisDiseases
                                      join md in _db.MedDiseases on dd.DiseaseId equals md.DiseaseId
                                      where dd.DiagnosisId == diagnosisId
                                      select new OthersDiseaseDetails
                                      {
                                          DiseaseId = dd.DiseaseId,
                                          DiseaseName = md.DiseaseName,
                                          DiseaseDescription = md.DiseaseDesc
                                      }).ToListAsync();

                Console.WriteLine($"Found {diseases.Count} diseases");

                // Get medicines with DECRYPTION
                var medicines = await (from dm in _db.OthersDiagnosisMedicines
                                       join mm in _db.med_masters on dm.MedItemId equals mm.MedItemId
                                       where dm.DiagnosisId == diagnosisId
                                       select new
                                       {
                                           dm.MedItemId,
                                           mm.MedItemName,
                                           dm.Quantity,
                                           dm.Dose,
                                           dm.Instructions,
                                           mm.CompanyName
                                       }).ToListAsync();

                // Decrypt medicine names from instructions
                var decryptedMedicines = medicines.Select(m => {
                    var medicineName = m.MedItemName; // Default fallback

                    // Try to extract and decrypt medicine name from instructions
                    if (!string.IsNullOrEmpty(m.Instructions) && m.Instructions.Contains(" - "))
                    {
                        var parts = m.Instructions.Split(" - ", 2);
                        if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                        {
                            var decryptedName = _encryptionService.Decrypt(parts[0]);
                            if (!string.IsNullOrEmpty(decryptedName))
                            {
                                medicineName = decryptedName;
                            }
                        }
                    }

                    return new OthersMedicineDetails
                    {
                        MedItemId = m.MedItemId,
                        MedicineName = medicineName,
                        Quantity = m.Quantity,
                        Dose = m.Dose,
                        Instructions = m.Instructions,
                        CompanyName = m.CompanyName
                    };
                }).ToList();

                Console.WriteLine($"Found {decryptedMedicines.Count} medicines using join query");

                var result = new OthersDiagnosisDetailsViewModel
                {
                    DiagnosisId = diagnosis.DiagnosisId,
                    TreatmentId = diagnosis.Patient?.TreatmentId ?? "N/A",
                    PatientName = diagnosis.Patient?.PatientName ?? "N/A",
                    Age = diagnosis.Patient?.Age ?? 0,
                    PNumber = diagnosis.Patient?.PNumber ?? "N/A",
                    Category = diagnosis.Patient?.Category ?? "N/A",
                    OtherDetails = diagnosis.Patient?.OtherDetails,
                    VisitDate = diagnosis.VisitDate,
                    LastVisitDate = diagnosis.LastVisitDate,
                    // DECRYPT vital signs
                    BloodPressure = _encryptionService.Decrypt(diagnosis.BloodPressure),
                    PulseRate = _encryptionService.Decrypt(diagnosis.PulseRate),
                    Sugar = _encryptionService.Decrypt(diagnosis.Sugar),
                    Remarks = diagnosis.Remarks,
                    DiagnosedBy = diagnosis.DiagnosedBy,
                    VisitType = diagnosis.VisitType,
                    ApprovalStatus = diagnosis.ApprovalStatus,
                    ApprovedBy = diagnosis.ApprovedBy,
                    ApprovedDate = diagnosis.ApprovedDate,
                    RejectionReason = diagnosis.RejectionReason,
                    Diseases = diseases,
                    Medicines = decryptedMedicines
                };

                Console.WriteLine($"Returning result with {result.Diseases.Count} diseases and {result.Medicines.Count} medicines");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetDiagnosisDetailsAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<bool> DeleteDiagnosisAsync(int diagnosisId)
        {
            try
            {
                var diagnosis = await _db.OthersDiagnoses.FindAsync(diagnosisId);
                if (diagnosis == null)
                    return false;

                _db.OthersDiagnoses.Remove(diagnosis);
                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<OthersDiagnosisViewModel?> GetPatientForEditAsync(string treatmentId)
        {
            var patient = await GetPatientByTreatmentIdAsync(treatmentId);
            if (patient == null)
                return null;

            var lastDiagnosis = patient.Diagnoses.FirstOrDefault();

            return new OthersDiagnosisViewModel
            {
                PatientId = patient.PatientId,
                TreatmentId = patient.TreatmentId,
                PatientName = patient.PatientName,
                Age = patient.Age,
                PNumber = patient.PNumber,
                Category = patient.Category,
                OtherDetails = patient.OtherDetails,
                LastVisitDate = lastDiagnosis?.VisitDate,
                DiagnosedBy = lastDiagnosis?.DiagnosedBy ?? ""
            };
        }

        public async Task<object> GetRawMedicineDataAsync(int diagnosisId)
        {
            try
            {
                var rawMedicines = await _db.OthersDiagnosisMedicines
                    .Where(dm => dm.DiagnosisId == diagnosisId)
                    .Select(dm => new {
                        dm.DiagnosisMedicineId,
                        dm.DiagnosisId,
                        dm.MedItemId,
                        dm.Quantity,
                        dm.Dose,
                        dm.Instructions
                    })
                    .ToListAsync();

                var medicineIds = rawMedicines.Select(rm => rm.MedItemId).ToList();

                var medMasters = await _db.med_masters
                    .Where(mm => medicineIds.Contains(mm.MedItemId))
                    .Select(mm => new {
                        mm.MedItemId,
                        mm.MedItemName,
                        mm.CompanyName
                    })
                    .ToListAsync();

                var joinResult = from rm in rawMedicines
                                 join mm in medMasters on rm.MedItemId equals mm.MedItemId
                                 select new
                                 {
                                     rm.DiagnosisMedicineId,
                                     rm.MedItemId,
                                     mm.MedItemName,
                                     rm.Quantity,
                                     rm.Dose,
                                     rm.Instructions,
                                     mm.CompanyName
                                 };

                return new
                {
                    diagnosisId = diagnosisId,
                    rawMedicineCount = rawMedicines.Count,
                    rawMedicines = rawMedicines,
                    medMasterCount = medMasters.Count,
                    medMasters = medMasters,
                    joinResult = joinResult.ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting raw medicine data: {ex.Message}", ex);
            }
        }

        public async Task<List<MedMaster>> GetCompounderMedicinesAsync()
        {
            var medicineIds = await _db.CompounderIndentItems
                .Select(ci => ci.MedItemId)
                .Distinct()
                .ToListAsync();

            var medicines = await _db.med_masters
                .Where(m => medicineIds.Contains(m.MedItemId))
                .ToListAsync();

            return medicines;
        }

        // ======= APPROVAL METHODS (with DECRYPTION for pending approvals) =======

        public async Task<int> GetPendingApprovalCountAsync()
        {
            return await _db.OthersDiagnoses
                .CountAsync(d => d.ApprovalStatus == "Pending");
        }

        public async Task<List<OthersPendingApprovalViewModel>> GetPendingApprovalsAsync()
        {
            var pendingDiagnoses = await _db.OthersDiagnoses
                .Include(d => d.Patient)
                .Include(d => d.DiagnosisDiseases)
                    .ThenInclude(dd => dd.MedDisease)
                .Include(d => d.DiagnosisMedicines)
                    .ThenInclude(dm => dm.MedMaster)
                .Where(d => d.ApprovalStatus == "Pending")
                .OrderBy(d => d.VisitDate)
                .ToListAsync();

            var result = new List<OthersPendingApprovalViewModel>();

            foreach (var diagnosis in pendingDiagnoses)
            {
                var diseases = diagnosis.DiagnosisDiseases.Select(dd => new OthersDiseaseDetails
                {
                    DiseaseId = dd.DiseaseId,
                    DiseaseName = dd.MedDisease?.DiseaseName ?? "Unknown Disease",
                    DiseaseDescription = dd.MedDisease?.DiseaseDesc
                }).ToList();

                // Decrypt medicine names for approval display
                var medicines = diagnosis.DiagnosisMedicines.Select(dm => {
                    var medicineName = dm.MedMaster?.MedItemName ?? "Unknown Medicine";

                    // Try to extract and decrypt medicine name from instructions
                    if (!string.IsNullOrEmpty(dm.Instructions) && dm.Instructions.Contains(" - "))
                    {
                        var parts = dm.Instructions.Split(" - ", 2);
                        if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                        {
                            var decryptedName = _encryptionService.Decrypt(parts[0]);
                            if (!string.IsNullOrEmpty(decryptedName))
                            {
                                medicineName = decryptedName;
                            }
                        }
                    }

                    return new OthersMedicineDetails
                    {
                        MedItemId = dm.MedItemId,
                        MedicineName = medicineName,
                        Quantity = dm.Quantity,
                        Dose = dm.Dose,
                        Instructions = dm.Instructions,
                        CompanyName = dm.MedMaster?.CompanyName
                    };
                }).ToList();

                result.Add(new OthersPendingApprovalViewModel
                {
                    DiagnosisId = diagnosis.DiagnosisId,
                    TreatmentId = diagnosis.Patient?.TreatmentId ?? "N/A",
                    PatientName = diagnosis.Patient?.PatientName ?? "N/A",
                    Age = diagnosis.Patient?.Age ?? 0,
                    Category = diagnosis.Patient?.Category ?? "N/A",
                    VisitDate = diagnosis.VisitDate,
                    VisitType = diagnosis.VisitType,
                    DiagnosedBy = diagnosis.DiagnosedBy,
                    // DECRYPT vital signs
                    BloodPressure = _encryptionService.Decrypt(diagnosis.BloodPressure),
                    PulseRate = _encryptionService.Decrypt(diagnosis.PulseRate),
                    Sugar = _encryptionService.Decrypt(diagnosis.Sugar),
                    ApprovalStatus = diagnosis.ApprovalStatus,
                    MedicineCount = medicines.Count,
                    Diseases = diseases,
                    Medicines = medicines
                });
            }

            return result;
        }

        public async Task<bool> ApproveDiagnosisAsync(int diagnosisId, string approvedBy)
        {
            var diagnosis = await _db.OthersDiagnoses.FindAsync(diagnosisId);
            if (diagnosis == null || diagnosis.ApprovalStatus != "Pending")
                return false;

            diagnosis.ApprovalStatus = "Approved";
            diagnosis.ApprovedBy = approvedBy;
            diagnosis.ApprovedDate = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectDiagnosisAsync(int diagnosisId, string rejectionReason, string rejectedBy)
        {
            var diagnosis = await _db.OthersDiagnoses.FindAsync(diagnosisId);
            if (diagnosis == null || diagnosis.ApprovalStatus != "Pending")
                return false;

            diagnosis.ApprovalStatus = "Rejected";
            diagnosis.ApprovedBy = rejectedBy;
            diagnosis.ApprovedDate = DateTime.Now;
            diagnosis.RejectionReason = rejectionReason;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<int> ApproveAllDiagnosesAsync(List<int> diagnosisIds, string approvedBy)
        {
            var diagnoses = await _db.OthersDiagnoses
                .Where(d => diagnosisIds.Contains(d.DiagnosisId) && d.ApprovalStatus == "Pending")
                .ToListAsync();

            foreach (var diagnosis in diagnoses)
            {
                diagnosis.ApprovalStatus = "Approved";
                diagnosis.ApprovedBy = approvedBy;
                diagnosis.ApprovedDate = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            return diagnoses.Count;
        }
    }
}