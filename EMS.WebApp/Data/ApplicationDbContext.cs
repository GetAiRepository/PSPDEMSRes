using EMS.WebApp.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EMS.WebApp.Data;

public partial class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Existing DbSets - keeping all your original DbSets
    public virtual DbSet<AccountLogin> AccountLogin { get; set; }
    public virtual DbSet<HrEmployee> HrEmployees { get; set; }
    public virtual DbSet<HrEmployeeDependent> HrEmployeeDependents { get; set; }
    public virtual DbSet<med_ambulance_master> med_ambulance_masters { get; set; }
    public virtual DbSet<MedBase> med_bases { get; set; }
    public virtual DbSet<MedCategory> med_categories { get; set; }
    public virtual DbSet<MedDisease> MedDiseases { get; set; }
    public virtual DbSet<MedDiagnosis> MedDiagnosis { get; set; }
    public virtual DbSet<MedExamCategory> med_exam_categories { get; set; }
    public virtual DbSet<MedMaster> med_masters { get; set; }
    public virtual DbSet<MedRefHospital> MedRefHospital { get; set; }
    public virtual DbSet<org_department> org_departments { get; set; }
    public virtual DbSet<org_plant> org_plants { get; set; }
    public virtual DbSet<SysAttachScreenRole> SysAttachScreenRoles { get; set; }
    public virtual DbSet<SysRole> SysRoles { get; set; }
    public virtual DbSet<sys_screen_name> sys_screen_names { get; set; }
    public virtual DbSet<SysUser> SysUsers { get; set; }

    public virtual DbSet<MedExamHeader> MedExamHeaders { get; set; }
    public virtual DbSet<MedWorkHistory> MedWorkHistories { get; set; }
    public virtual DbSet<RefWorkArea> RefWorkAreas { get; set; }
    public virtual DbSet<MedExamWorkArea> MedExamWorkAreas { get; set; }
    public virtual DbSet<RefMedCondition> RefMedConditions { get; set; }
    public virtual DbSet<MedExamCondition> MedExamConditions { get; set; }
    public virtual DbSet<MedGeneralExam> MedGeneralExams { get; set; }

    //Store Indent
    public virtual DbSet<StoreIndent> StoreIndents { get; set; }
    public virtual DbSet<StoreIndentItem> StoreIndentItems { get; set; }

    //Compounder Indent
    public virtual DbSet<CompounderIndent> CompounderIndents { get; set; }
    public virtual DbSet<ExpiredMedicine> ExpiredMedicines { get; set; }
    public virtual DbSet<CompounderIndentItem> CompounderIndentItems { get; set; }

    // Doctor Prescription
    public virtual DbSet<MedPrescription> MedPrescriptions { get; set; }
    public virtual DbSet<MedPrescriptionDisease> MedPrescriptionDiseases { get; set; }
    public virtual DbSet<MedPrescriptionMedicine> MedPrescriptionMedicines { get; set; }

    // Doctor Other Patients and Diagnoses
    public virtual DbSet<OtherPatient> OtherPatients { get; set; }
    public virtual DbSet<OthersDiagnosis> OthersDiagnoses { get; set; }
    public virtual DbSet<OthersDiagnosisDisease> OthersDiagnosisDiseases { get; set; }
    public virtual DbSet<OthersDiagnosisMedicine> OthersDiagnosisMedicines { get; set; }

    // NEW: Audit Trail DbSets
    public virtual DbSet<AuditTrail> AuditTrails { get; set; }
    public virtual DbSet<UserActionLog> UserActionLogs { get; set; }

    // Override SaveChangesAsync to implement audit logging
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(cancellationToken);
        await OnAfterSaveChangesAsync(auditEntries);
        return result;
    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();
        var currentUser = GetCurrentUser();
        var timestamp = DateTime.Now;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Skip audit entities to prevent infinite loops
            if (entry.Entity is AuditTrail || entry.Entity is UserActionLog)
                continue;

            var auditEntry = new AuditEntry(entry)
            {
                TableName = entry.Entity.GetType().Name,
                UserId = currentUser,
                Timestamp = timestamp,
                IpAddress = GetIpAddress(),
                UserAgent = GetUserAgent(),
                SessionId = GetSessionId()
            };

            auditEntries.Add(auditEntry);

            // Handle BaseAuditEntity fields
            if (entry.Entity is BaseAuditEntity auditableEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditableEntity.CreatedBy = currentUser;
                        auditableEntity.CreatedDate = timestamp;
                        break;
                    case EntityState.Modified:
                        auditableEntity.ModifiedBy = currentUser;
                        auditableEntity.ModifiedDate = timestamp;
                        break;
                    case EntityState.Deleted:
                        // Convert hard delete to soft delete
                        auditableEntity.DeletedBy = currentUser;
                        auditableEntity.DeletedDate = timestamp;
                        auditableEntity.IsDeleted = true;
                        entry.State = EntityState.Modified;
                        break;
                }
            }

            // Collect changed properties
            foreach (var property in entry.Properties)
            {
                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.AuditType = AuditType.Create;
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        auditEntry.AuditType = AuditType.Delete;
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.ChangedColumns.Add(propertyName);
                            auditEntry.AuditType = AuditType.Update;
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        // Remove entries that have no changes
        auditEntries.RemoveAll(x => x.AuditType == AuditType.None);

        return auditEntries;
    }

    private async Task OnAfterSaveChangesAsync(List<AuditEntry> auditEntries)
    {
        if (auditEntries?.Count > 0)
        {
            foreach (var auditEntry in auditEntries)
            {
                // Update primary key for new entities
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                // Create audit trail record
                var auditTrail = new AuditTrail
                {
                    TableName = auditEntry.TableName,
                    Operation = auditEntry.AuditType.ToString().ToUpper(),
                    PrimaryKey = JsonSerializer.Serialize(auditEntry.KeyValues),
                    OldValues = auditEntry.OldValues.Count == 0 ? null : JsonSerializer.Serialize(auditEntry.OldValues),
                    NewValues = auditEntry.NewValues.Count == 0 ? null : JsonSerializer.Serialize(auditEntry.NewValues),
                    AffectedColumns = auditEntry.ChangedColumns.Count == 0 ? null : JsonSerializer.Serialize(auditEntry.ChangedColumns),
                    UserId = auditEntry.UserId,
                    Timestamp = auditEntry.Timestamp,
                    IpAddress = auditEntry.IpAddress,
                    UserAgent = auditEntry.UserAgent,
                    SessionId = auditEntry.SessionId,
                    Action = GetCurrentAction()
                };

                AuditTrails.Add(auditTrail);
            }

            await base.SaveChangesAsync();
        }
    }

    private string GetCurrentUser()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "SYSTEM";
    }

    private string? GetIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].FirstOrDefault();
    }

    private string? GetSessionId()
    {
        return _httpContextAccessor.HttpContext?.Session?.Id;
    }

    private string? GetCurrentAction()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Request?.RouteValues != null)
        {
            var controller = context.Request.RouteValues["controller"]?.ToString();
            var action = context.Request.RouteValues["action"]?.ToString();
            return controller != null && action != null ? $"{controller}/{action}" : null;
        }
        return null;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Your existing entity configurations - keeping them exactly as they are
        modelBuilder.Entity<AccountLogin>(entity =>
        {
            entity.ToTable("account_login");

            entity.HasKey(e => e.login_id);
            entity.Property(e => e.login_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.user_name)
                .HasMaxLength(120)
                .IsUnicode(false);
            entity.Property(e => e.password)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<HrEmployee>(entity =>
        {
            entity.ToTable("hr_employee");
            entity.HasKey(e => e.emp_uid);
            entity.Property(e => e.emp_uid)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.emp_id)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.emp_name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.emp_Gender)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.emp_Grade)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(e => e.org_department)
                  .WithMany(mb => mb.HrEmployees)  // specify navigation collection
                  .HasForeignKey(m => m.dept_id);

            entity.HasOne(e => e.org_plant)
                  .WithMany(mb => mb.HrEmployees)  // specify navigation collection
                  .HasForeignKey(m => m.plant_id);
            entity.Property(e => e.emp_blood_Group)
                .HasMaxLength(10)
                .IsUnicode(false);
            // Add unique constraint for emp_id
            entity.HasIndex(e => e.emp_id)
                  .IsUnique()
                  .HasDatabaseName("IX_HrEmployee_EmpId_Unique");
        });

        modelBuilder.Entity<HrEmployeeDependent>(entity =>
        {
            entity.ToTable("hr_employee_dependent");
            entity.HasKey(e => e.emp_dep_id);
            entity.Property(e => e.emp_dep_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.HasOne(e => e.HrEmployee)
                  .WithMany(mb => mb.HrEmployeeDependents)  // specify navigation collection
                  .HasForeignKey(m => m.emp_uid);
            entity.Property(e => e.dep_name)
                .HasMaxLength(80)
                .IsUnicode(false);

            entity.Property(e => e.gender)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.relation)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.is_active)
                .HasColumnType("bit")
                .IsRequired();
            entity.Property(e => e.marital_status)
                .HasColumnType("bit")
                .IsRequired();
        });

        modelBuilder.Entity<med_ambulance_master>(entity =>
        {
            entity.HasKey(e => e.amb_id).HasName("PK__med_ambu__9FDA4AE611BCF6CF");

            entity.ToTable("med_ambulance_master");

            entity.Property(e => e.provider)
                .HasMaxLength(120)
                .IsUnicode(false);
            entity.Property(e => e.vehicle_no)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.vehicle_type)
                .HasMaxLength(15)
                .IsUnicode(false);
            // Add unique constraint for vehicle_no
            entity.HasIndex(e => e.vehicle_no)
                  .IsUnique()
                  .HasDatabaseName("IX_MedAmbulanceMaster_VehicleNo_Unique");
        });

        modelBuilder.Entity<MedBase>(entity =>
        {
            entity.ToTable("med_base");

            entity.HasKey(e => e.BaseId);
            entity.Property(e => e.BaseId)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.BaseDesc)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.BaseName)
                .HasMaxLength(120)
                .IsUnicode(false);
            // Add unique constraint for BaseName
            entity.HasIndex(e => e.BaseName)
                  .IsUnique()
                  .HasDatabaseName("IX_MedBase_BaseName_Unique");
        });

        modelBuilder.Entity<MedCategory>(entity =>
        {

            entity.ToTable("med_category");
            entity.HasKey(e => e.MedCatId);
            entity.Property(e => e.MedCatId)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.Description)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.MedCatName)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Remarks)
                .HasMaxLength(250)
                .IsUnicode(false);
            // Add unique constraint for MedCatName
            entity.HasIndex(e => e.MedCatName)
                  .IsUnique()
                  .HasDatabaseName("IX_MedCategory_MedCatName_Unique");
        });

        modelBuilder.Entity<MedDisease>(entity =>
        {
            entity.ToTable("med_disease");     // ← map to your real table
            entity.HasKey(e => e.DiseaseId);
            entity.Property(e => e.DiseaseId)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.DiseaseName)
                  .IsRequired()
                  .HasMaxLength(120);
            entity.Property(e => e.DiseaseDesc)
                  .HasMaxLength(250);
            // Add unique constraint for DiseaseName
            entity.HasIndex(e => e.DiseaseName)
                  .IsUnique()
                  .HasDatabaseName("IX_MedDisease_DiseaseName_Unique");
        });

        modelBuilder.Entity<MedDiagnosis>(entity =>
        {
            entity.ToTable("med_diagnosis");     // ← map to your real table
            entity.HasKey(e => e.diag_id);
            entity.Property(e => e.diag_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.diag_name)
                  .IsRequired()
                  .HasMaxLength(120);
            entity.Property(e => e.diag_desc)
                  .HasMaxLength(250);
            // Add unique constraint for diag_name
            entity.HasIndex(e => e.diag_name)
                  .IsUnique()
                  .HasDatabaseName("IX_MedDiagnosis_DiagName_Unique");
        });

        modelBuilder.Entity<MedExamCategory>(entity =>
        {
            entity.ToTable("med_exam_category");
            entity.HasKey(e => e.CatId);
            entity.Property(e => e.CatId)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.AnnuallyRule)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.CatName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MonthsSched)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remarks)
                .HasMaxLength(250)
                .IsUnicode(false);
            // Add composite unique constraint for cat_name, years_freq, annually_rule, and months_sched combination
            entity.HasIndex(e => new { e.CatName, e.YearsFreq, e.AnnuallyRule, e.MonthsSched })
                  .IsUnique()
                  .HasDatabaseName("IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSched_Unique");
        });

        modelBuilder.Entity<MedMaster>(entity =>
        {
            //entity.HasKey(e => e.MedItemId).HasName("PK__med_mast__3FD153F2E2B8E821");

            entity.ToTable("med_master");
            entity.HasKey(e => e.MedItemId);
            entity.Property(e => e.MedItemId)
                  .HasColumnName("med_item_id")
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.MedBase)
                  .WithMany(mb => mb.MedMasters)  // specify navigation collection
                  .HasForeignKey(m => m.BaseId);

            entity.Property(e => e.CompanyName)
                .HasMaxLength(120)
                .IsUnicode(false);
            entity.Property(e => e.MedItemName)
                .HasMaxLength(120)
                .IsUnicode(false);
            //entity.Property(e => e.Potency)
            //    .HasMaxLength(40)
            //    .IsUnicode(false);
            // Add composite unique constraint for med_item_name, base_id, and company_name combination
            entity.HasIndex(e => new { e.MedItemName, e.BaseId, e.CompanyName })
                  .IsUnique()
                  .HasDatabaseName("IX_MedMaster_MedItemNameBaseIdCompanyName_Unique");
        });

        modelBuilder.Entity<MedRefHospital>(entity =>
        {
            entity.ToTable("med_ref_hospital");
            entity.HasKey(e => e.hosp_id);
            entity.Property(e => e.hosp_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();

            // Add composite unique constraint for hosp_name and hosp_code combination
            entity.HasIndex(e => new { e.hosp_name, e.hosp_code })
                  .IsUnique()
                  .HasDatabaseName("IX_MedRefHospital_HospNameCode_Unique");

        });

        modelBuilder.Entity<org_department>(entity =>
        {
            entity.HasKey(e => e.dept_id).HasName("PK__org_depa__DCA65974896CEDB8");

            entity.ToTable("org_department");

            entity.Property(e => e.Remarks)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.dept_description)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.dept_name)
                .HasMaxLength(25)
                .IsUnicode(false);
            // Add unique constraint for dept_name
            entity.HasIndex(e => e.dept_name)
                  .IsUnique()
                  .HasDatabaseName("IX_OrgDepartment_DeptName_Unique");
        });

        modelBuilder.Entity<org_plant>(entity =>
        {
            entity.HasKey(e => e.plant_id).HasName("PK__org_plan__A576B3B47C5A3448");

            entity.ToTable("org_plant");

            entity.Property(e => e.Description)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.plant_code)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.plant_name)
                .HasMaxLength(100)
                .IsUnicode(false);
            // Add unique constraint for plant_code
            entity.HasIndex(e => e.plant_code)
                  .IsUnique()
                  .HasDatabaseName("IX_OrgPlant_PlantCode_Unique");
        });

        modelBuilder.Entity<SysAttachScreenRole>(entity =>
        {
            entity.ToTable("sys_attach_screen_role");
            entity.HasKey(e => e.uid);
            entity.Property(e => e.uid)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.HasIndex(r => r.role_uid).IsUnique(); // Only role_uid is unique

            entity.HasOne(e => e.SysRole)
                  .WithMany(mb => mb.SysAttachScreenRole)  // specify navigation collection
                  .HasForeignKey(m => m.role_uid);
            //entity.HasOne(e => e.sys_screen_name)
            //      .WithMany(mb => mb.SysAttachScreenRole)  // specify navigation collection
            //      .HasForeignKey(m => m.screen_uid);
        });

        modelBuilder.Entity<SysRole>(entity =>
        {
            entity.ToTable("sys_role");
            entity.HasKey(e => e.role_id);
            entity.Property(e => e.role_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.role_desc)
                .HasMaxLength(120)
                .IsUnicode(false);
            entity.Property(e => e.role_name)
                .HasMaxLength(40)
                .IsUnicode(false);
            // Add unique constraint for role_name
            entity.HasIndex(e => e.role_name)
                  .IsUnique()
                  .HasDatabaseName("IX_SysRole_RoleName_Unique");
        });

        modelBuilder.Entity<sys_screen_name>(entity =>
        {
            entity.HasKey(e => e.screen_uid).HasName("PK__sys_scre__B2C9B83A098D0056");

            entity.ToTable("sys_screen_name");

            entity.Property(e => e.screen_uid)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.screen_description)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.screen_name)
                .HasMaxLength(40)

                .IsUnicode(false);
        });

        modelBuilder.Entity<SysUser>(entity =>
        {
            entity.ToTable("sys_user");
            entity.HasKey(e => e.user_id);
            entity.Property(e => e.user_id)
                  .UseIdentityColumn()         // identity(1,1)
                  .ValueGeneratedOnAdd();
            entity.HasOne(e => e.SysRole)
                  .WithMany(mb => mb.SysUsers)  // specify navigation collection
                  .HasForeignKey(m => m.role_id);
            entity.Property(e => e.email)
                .HasMaxLength(120)
                .IsUnicode(false);
            entity.Property(e => e.adid)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.full_name)
                .HasMaxLength(80)
                .IsUnicode(false);
        });

        //----------------------------------------
        modelBuilder.Entity<MedExamHeader>(entity =>
        {
            entity.ToTable("med_exam_header");

            entity.HasKey(e => e.exam_id);

            entity.Property(e => e.exam_id)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.exam_date)
                  .HasColumnName("exam_date");


            entity.Property(e => e.food_habit)
                  .HasColumnName("food_habit")
                  .HasMaxLength(100)
                  .IsUnicode(false);



            entity.HasOne(e => e.HrEmployee)
                  .WithMany(emp => emp.MedExamHeaders)
                  .HasForeignKey(e => e.emp_uid);
        });

        modelBuilder.Entity<MedWorkHistory>(entity =>
        {
            entity.ToTable("med_work_history");

            entity.HasKey(e => e.work_uid);

            entity.Property(e => e.work_uid)
                  .UseIdentityColumn()  // identity(1,1)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.emp_uid)
                  .IsRequired();

            entity.Property(e => e.exam_id)
                  .IsRequired();

            entity.Property(e => e.job_name)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.Property(e => e.years_in_job)
                  .HasColumnType("decimal(4,1)"); // you might adjust precision as needed

            entity.Property(e => e.work_env)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.Property(e => e.ppe)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.Property(e => e.job_injuries)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.HasOne(e => e.HrEmployee)
                  .WithMany(mb => mb.MedWorkHistories)  // assuming collection property name in HrEmployee
                  .HasForeignKey(e => e.emp_uid);

            entity.HasOne(e => e.MedExamHeader)
                  .WithMany(mb => mb.MedWorkHistories)  // assuming collection property name in MedExamHeader
                  .HasForeignKey(e => e.exam_id);
        });

        modelBuilder.Entity<RefWorkArea>(entity =>
        {
            entity.ToTable("ref_work_area");

            entity.HasKey(e => e.area_uid);

            entity.Property(e => e.area_uid)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.area_code)
                  .HasMaxLength(40)
                  .IsUnicode(false);

            entity.Property(e => e.area_desc)
                  .HasMaxLength(40)
                  .IsUnicode(false);
        });


        modelBuilder.Entity<MedExamWorkArea>(entity =>
        {
            entity.ToTable("med_exam_work_area");

            entity.HasKey(e => e.work_area_uid);

            entity.Property(e => e.work_area_uid)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.MedExamHeader)
                  .WithMany(m => m.MedExamWorkAreas)
                  .HasForeignKey(e => e.exam_id);

            entity.HasOne(e => e.RefWorkArea)
                  .WithMany(r => r.MedExamWorkAreas)
                  .HasForeignKey(e => e.area_uid);
        });

        modelBuilder.Entity<RefMedCondition>(entity =>
        {
            entity.ToTable("ref_med_condition");

            entity.HasKey(e => e.cond_uid);

            entity.Property(e => e.cond_uid)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.cond_code)
                  .HasMaxLength(40)
                  .IsUnicode(false);

            entity.Property(e => e.cond_desc)
                  .HasMaxLength(40)
                  .IsUnicode(false);
        });
        modelBuilder.Entity<MedExamCondition>(entity =>
        {
            entity.ToTable("med_exam_condition");

            entity.HasKey(e => e.exam_condition_uid);

            entity.Property(e => e.exam_condition_uid)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.present)
                  .HasColumnType("bit")
                  .IsRequired();

            entity.HasOne(e => e.MedExamHeader)
                  .WithMany(m => m.MedExamConditions)
                  .HasForeignKey(e => e.exam_id);

            entity.HasOne(e => e.RefMedCondition)
                  .WithMany(r => r.MedExamConditions)
                  .HasForeignKey(e => e.cond_uid);
        });

        modelBuilder.Entity<MedGeneralExam>(entity =>
        {
            entity.ToTable("med_general_exam");

            entity.HasKey(e => e.general_exam_uid);

            entity.Property(e => e.general_exam_uid)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.emp_uid)
                  .IsRequired();

            entity.Property(e => e.exam_id)
                  .IsRequired();

            entity.Property(e => e.height_cm)
                  .HasColumnType("smallint");

            entity.Property(e => e.weight_kg)
                  .HasColumnType("smallint");

            entity.Property(e => e.abdomen)
                  .HasMaxLength(40)
                  .IsUnicode(false);

            entity.Property(e => e.pulse)
                  .HasMaxLength(20)
                  .IsUnicode(false);

            entity.Property(e => e.bp)
                  .HasMaxLength(20)
                  .IsUnicode(false);

            entity.Property(e => e.bmi)
                  .HasColumnType("decimal(5,2)");

            entity.Property(e => e.ent)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.rr)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.opthal)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.cvs)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.skin)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.cns)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.genito_urinary)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.respiratory)
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.others)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.Property(e => e.remarks)
                  .HasMaxLength(2000)
                  .IsUnicode(false);

            entity.HasOne(e => e.MedExamHeader)
                  .WithMany(m => m.MedGeneralExams)
                  .HasForeignKey(e => e.exam_id);
        });
        modelBuilder.Entity<StoreIndent>(entity =>
        {
            entity.ToTable("store_indent");
            entity.HasKey(e => e.IndentId);
            entity.Property(e => e.IndentId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.IndentType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.Comments)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.Property(e => e.ApprovedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.Property(e => e.IndentDate)
                .HasColumnType("date");

            entity.Property(e => e.CreatedDate)
                .HasColumnType("datetime2");

            entity.Property(e => e.ApprovedDate)
                .HasColumnType("datetime2");
        });
        // StoreIndentItem configuration
        modelBuilder.Entity<StoreIndentItem>(entity =>
        {
            entity.ToTable("store_indent_item");
            entity.HasKey(e => e.IndentItemId);
            entity.Property(e => e.IndentItemId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.VendorCode)
                .HasMaxLength(50)
                .IsUnicode(false);

            // Updated column mappings
            entity.Property(e => e.RaisedQuantity)
                .HasColumnName("raised_quantity")
                .IsRequired();

            entity.Property(e => e.ReceivedQuantity)
                .HasColumnName("received_quantity")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(10,2)");

            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(10,2)");

            // New fields for Store Inventory
            entity.Property(e => e.BatchNo)
                .HasColumnName("batch_no")
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.ExpiryDate)
                .HasColumnName("expiry_date")
                .HasColumnType("date");

            // Foreign key relationships
            entity.HasOne(e => e.StoreIndent)
                  .WithMany(s => s.StoreIndentItems)
                  .HasForeignKey(e => e.IndentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedMaster)
                  .WithMany()
                  .HasForeignKey(e => e.MedItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraints
            entity.HasIndex(e => new { e.IndentId, e.VendorCode })
                  .IsUnique()
                  .HasDatabaseName("IX_StoreIndentItem_IndentIdVendorCode_Unique");

            entity.HasIndex(e => new { e.IndentId, e.MedItemId })
                  .IsUnique()
                  .HasDatabaseName("IX_StoreIndentItem_IndentIdMedItemId_Unique");
        });
        // CompounderIndent configuration
        modelBuilder.Entity<CompounderIndent>(entity =>
        {
            entity.ToTable("compounder_indent");
            entity.HasKey(e => e.IndentId);
            entity.Property(e => e.IndentId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.IndentType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.Comments)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.Property(e => e.ApprovedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.Property(e => e.IndentDate)
                .HasColumnType("date");

            entity.Property(e => e.CreatedDate)
                .HasColumnType("datetime2");

            entity.Property(e => e.ApprovedDate)
                .HasColumnType("datetime2");
        });
        // CompounderIndentItem configuration
        modelBuilder.Entity<CompounderIndentItem>(entity =>
        {
            entity.ToTable("compounder_indent_item");
            entity.HasKey(e => e.IndentItemId);
            entity.Property(e => e.IndentItemId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.VendorCode)
                .HasMaxLength(50)
                .IsUnicode(false);

            // Updated column mappings
            entity.Property(e => e.RaisedQuantity)
                .HasColumnName("raised_quantity")
                .IsRequired();

            entity.Property(e => e.ReceivedQuantity)
                .HasColumnName("received_quantity")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(10,2)");

            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(10,2)");

            // New fields for Compounder Inventory - Batch, Expiry, and Available Stock tracking
            entity.Property(e => e.BatchNo)
                .HasColumnName("batch_no")
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.ExpiryDate)
                .HasColumnName("expiry_date")
                .HasColumnType("date");

            entity.Property(e => e.AvailableStock)
                .HasColumnName("available_stock");

            // Foreign key relationships
            entity.HasOne(e => e.CompounderIndent)
                  .WithMany(s => s.CompounderIndentItems)
                  .HasForeignKey(e => e.IndentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedMaster)
                  .WithMany()
                  .HasForeignKey(e => e.MedItemId)
                  .HasPrincipalKey(m => m.MedItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraints
            entity.HasIndex(e => new { e.IndentId, e.VendorCode })
                  .IsUnique()
                  .HasDatabaseName("IX_CompounderIndentItem_IndentIdVendorCode_Unique");

            entity.HasIndex(e => new { e.IndentId, e.MedItemId })
                  .IsUnique()
                  .HasDatabaseName("IX_CompounderIndentItem_IndentIdMedItemId_Unique");
        });

        // MedPrescription configuration
        modelBuilder.Entity<MedPrescription>(entity =>
        {
            entity.ToTable("med_prescription");

            entity.HasKey(e => e.PrescriptionId);
            entity.Property(e => e.PrescriptionId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.PrescriptionDate)
                  .HasColumnType("datetime2");

            entity.Property(e => e.CreatedDate)
                  .HasColumnType("datetime2");

            // NEW: Configure approval fields
            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("Approved"); // Default for regular visits

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.ApprovedDate)
                  .HasColumnType("datetime2");

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(500);

            entity.HasOne(e => e.HrEmployee)
                  .WithMany()
                  .HasForeignKey(e => e.emp_uid)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.MedExamHeader)
                  .WithMany()
                  .HasForeignKey(e => e.exam_id)
                  .OnDelete(DeleteBehavior.NoAction);

            // Create index for approval status queries
            entity.HasIndex(e => e.ApprovalStatus)
                  .HasDatabaseName("IX_MedPrescription_ApprovalStatus");
        });

        // MedPrescriptionDisease configuration
        modelBuilder.Entity<MedPrescriptionDisease>(entity =>
        {
            entity.ToTable("med_prescription_disease");

            entity.HasKey(e => e.PrescriptionDiseaseId);
            entity.Property(e => e.PrescriptionDiseaseId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.MedPrescription)
                  .WithMany(p => p.PrescriptionDiseases)
                  .HasForeignKey(e => e.PrescriptionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedDisease)
                  .WithMany()
                  .HasForeignKey(e => e.DiseaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint to prevent duplicate disease for same prescription
            entity.HasIndex(e => new { e.PrescriptionId, e.DiseaseId })
                  .IsUnique()
                  .HasDatabaseName("IX_MedPrescriptionDisease_PrescriptionIdDiseaseId_Unique");
        });

        // MedPrescriptionMedicine configuration
        modelBuilder.Entity<MedPrescriptionMedicine>(entity =>
        {
            entity.ToTable("med_prescription_medicine");

            entity.HasKey(e => e.PrescriptionMedicineId);
            entity.Property(e => e.PrescriptionMedicineId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.MedPrescription)
                  .WithMany(p => p.PrescriptionMedicines)
                  .HasForeignKey(e => e.PrescriptionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedMaster)
                  .WithMany()
                  .HasForeignKey(e => e.MedItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint to prevent duplicate medicine for same prescription
            entity.HasIndex(e => new { e.PrescriptionId, e.MedItemId })
                  .IsUnique()
                  .HasDatabaseName("IX_MedPrescriptionMedicine_PrescriptionIdMedItemId_Unique");
        });

        // OtherPatient configuration
        modelBuilder.Entity<OtherPatient>(entity =>
        {
            entity.ToTable("other_patient"); // or "OtherPatients" - check your actual table name

            entity.HasKey(e => e.PatientId);
            entity.Property(e => e.PatientId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CreatedDate)
                  .HasColumnType("datetime2");

            // Unique constraint for TreatmentId
            entity.HasIndex(e => e.TreatmentId)
                  .IsUnique()
                  .HasDatabaseName("IX_OtherPatient_TreatmentId_Unique");
        });

        // OthersDiagnosis configuration (updated with new fields)
        modelBuilder.Entity<OthersDiagnosis>(entity =>
        {
            entity.ToTable("others_diagnosis"); // Make sure this matches your actual table name

            entity.HasKey(e => e.DiagnosisId);
            entity.Property(e => e.DiagnosisId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.VisitDate)
                  .HasColumnType("datetime2");

            entity.Property(e => e.LastVisitDate)
                  .HasColumnType("datetime2");

            entity.Property(e => e.CreatedDate)
                  .HasColumnType("datetime2");

            // NEW: Visit Type and Approval Fields Configuration
            entity.Property(e => e.VisitType)
                  .HasMaxLength(50)
                  .HasDefaultValue("Regular Visitor")
                  .IsRequired();

            entity.Property(e => e.ApprovalStatus)
                  .HasMaxLength(50)
                  .HasDefaultValue("Approved")
                  .IsRequired();

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.ApprovedDate)
                  .HasColumnType("datetime2");

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(500);

            entity.Property(e => e.BloodPressure)
                  .HasMaxLength(20);

            entity.Property(e => e.PulseRate)
                  .HasMaxLength(20);

            entity.Property(e => e.Sugar)
                  .HasMaxLength(20);

            entity.Property(e => e.Remarks)
                  .HasMaxLength(1000);

            entity.Property(e => e.DiagnosedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasOne(e => e.Patient)
                  .WithMany(p => p.Diagnoses)
                  .HasForeignKey(e => e.PatientId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Create indexes for approval queries
            entity.HasIndex(e => e.ApprovalStatus)
                  .HasDatabaseName("IX_OthersDiagnosis_ApprovalStatus");

            entity.HasIndex(e => e.VisitType)
                  .HasDatabaseName("IX_OthersDiagnosis_VisitType");

            entity.HasIndex(e => new { e.ApprovalStatus, e.VisitType })
                  .HasDatabaseName("IX_OthersDiagnosis_ApprovalStatus_VisitType");
        });

        // OthersDiagnosisDisease configuration
        modelBuilder.Entity<OthersDiagnosisDisease>(entity =>
        {
            entity.ToTable("others_diagnosis_disease");

            entity.HasKey(e => e.DiagnosisDiseaseId);
            entity.Property(e => e.DiagnosisDiseaseId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.OthersDiagnosis)
                  .WithMany(d => d.DiagnosisDiseases)
                  .HasForeignKey(e => e.DiagnosisId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedDisease)
                  .WithMany()
                  .HasForeignKey(e => e.DiseaseId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint to prevent duplicate disease for same diagnosis
            entity.HasIndex(e => new { e.DiagnosisId, e.DiseaseId })
                  .IsUnique()
                  .HasDatabaseName("IX_OthersDiagnosisDisease_DiagnosisIdDiseaseId_Unique");
        });

        // OthersDiagnosisMedicine configuration
        modelBuilder.Entity<OthersDiagnosisMedicine>(entity =>
        {
            entity.ToTable("others_diagnosis_medicine");

            entity.HasKey(e => e.DiagnosisMedicineId);
            entity.Property(e => e.DiagnosisMedicineId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.HasOne(e => e.OthersDiagnosis)
                  .WithMany(d => d.DiagnosisMedicines)
                  .HasForeignKey(e => e.DiagnosisId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MedMaster)
                  .WithMany()
                  .HasForeignKey(e => e.MedItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint to prevent duplicate medicine for same diagnosis
            entity.HasIndex(e => new { e.DiagnosisId, e.MedItemId })
                  .IsUnique()
                  .HasDatabaseName("IX_OthersDiagnosisMedicine_DiagnosisIdMedItemId_Unique");
        });

        modelBuilder.Entity<ExpiredMedicine>(entity =>
        {
            entity.ToTable("expired_medicine");
            entity.HasKey(e => e.ExpiredMedicineId);
            entity.Property(e => e.ExpiredMedicineId)
                  .HasColumnName("expired_medicine_id")
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CompounderIndentItemId)
                  .HasColumnName("compounder_indent_item_id")
                  .IsRequired();

            entity.Property(e => e.MedicineName)
                  .HasColumnName("medicine_name")
                  .HasMaxLength(120)
                  .IsUnicode(false)
                  .IsRequired();

            entity.Property(e => e.CompanyName)
                  .HasColumnName("company_name")
                  .HasMaxLength(120)
                  .IsUnicode(false);

            entity.Property(e => e.BatchNumber)
                  .HasColumnName("batch_number")
                  .HasMaxLength(50)
                  .IsUnicode(false)
                  .IsRequired();

            entity.Property(e => e.VendorCode)
                  .HasColumnName("vendor_code")
                  .HasMaxLength(50)
                  .IsUnicode(false)
                  .IsRequired();

            entity.Property(e => e.ExpiryDate)
                  .HasColumnName("expiry_date")
                  .HasColumnType("date")
                  .IsRequired();

            entity.Property(e => e.QuantityExpired)
                  .HasColumnName("quantity_expired")
                  .IsRequired();

            entity.Property(e => e.IndentId)
                  .HasColumnName("indent_id")
                  .IsRequired();

            entity.Property(e => e.IndentNumber)
                  .HasColumnName("indent_number")
                  .HasMaxLength(50)
                  .IsUnicode(false);

            entity.Property(e => e.UnitPrice)
                  .HasColumnName("unit_price")
                  .HasColumnType("decimal(10,2)");

            entity.Property(e => e.TotalValue)
                  .HasColumnName("total_value")
                  .HasColumnType("decimal(10,2)");

            entity.Property(e => e.DetectedDate)
                  .HasColumnName("detected_date")
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("GETDATE()")
                  .IsRequired();

            entity.Property(e => e.DetectedBy)
                  .HasColumnName("detected_by")
                  .HasMaxLength(100)
                  .IsUnicode(false);

            entity.Property(e => e.Status)
                  .HasColumnName("status")
                  .HasMaxLength(30)
                  .IsUnicode(false)
                  .HasDefaultValue("Pending Disposal")
                  .IsRequired();

            entity.Property(e => e.BiomedicalWasteIssuedDate)
                  .HasColumnName("biomedical_waste_issued_date")
                  .HasColumnType("datetime2");

            entity.Property(e => e.BiomedicalWasteIssuedBy)
                  .HasColumnName("biomedical_waste_issued_by")
                  .HasMaxLength(100)
                  .IsUnicode(false);

            // NEW: Type of Medicine configuration
            entity.Property(e => e.TypeOfMedicine)
                  .HasColumnName("type_of_medicine")
                  .HasMaxLength(30)
                  .IsUnicode(false)
                  .HasDefaultValue("Select Type of Medicine")
                  .IsRequired();

            // Foreign key relationship
            entity.HasOne(e => e.CompounderIndentItem)
                  .WithMany()
                  .HasForeignKey(e => e.CompounderIndentItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint
            entity.HasIndex(e => e.CompounderIndentItemId)
                  .IsUnique()
                  .HasDatabaseName("UK_ExpiredMedicine_CompounderIndentItem");

            // Performance indexes
            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_ExpiredMedicine_Status");

            entity.HasIndex(e => e.ExpiryDate)
                  .HasDatabaseName("IX_ExpiredMedicine_ExpiryDate");

            entity.HasIndex(e => e.DetectedDate)
                  .HasDatabaseName("IX_ExpiredMedicine_DetectedDate");

            entity.HasIndex(e => e.MedicineName)
                  .HasDatabaseName("IX_ExpiredMedicine_MedicineName");

            entity.HasIndex(e => e.BatchNumber)
                  .HasDatabaseName("IX_ExpiredMedicine_BatchNumber");

            entity.HasIndex(e => e.VendorCode)
                  .HasDatabaseName("IX_ExpiredMedicine_VendorCode");

            // NEW: Index for Type of Medicine
            entity.HasIndex(e => e.TypeOfMedicine)
                  .HasDatabaseName("IX_ExpiredMedicine_TypeOfMedicine");

            // Composite index for common queries
            entity.HasIndex(e => new { e.Status, e.ExpiryDate })
                  .HasDatabaseName("IX_ExpiredMedicine_Status_ExpiryDate");

            // Check constraints (handled by database)
            // Status constraint: 'Pending Disposal' or 'Issued to Biomedical Waste'
            // Quantity constraint: quantity_expired > 0
            // TypeOfMedicine constraint: 'Solid', 'Liquid', or 'Gel'
        });

        // NEW: Audit Trail Configuration
        modelBuilder.Entity<AuditTrail>(entity =>
        {
            entity.ToTable("audit_trails");
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.AuditId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.TableName)
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.Operation)
                  .HasMaxLength(50)
                  .IsRequired();
            entity.Property(e => e.PrimaryKey)
                  .IsRequired();
            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.Timestamp)
                  .HasColumnType("datetime2")
                  .IsRequired();
            entity.Property(e => e.Reason)
                  .HasMaxLength(500);
            entity.Property(e => e.IpAddress)
                  .HasMaxLength(100);
            entity.Property(e => e.UserAgent)
                  .HasMaxLength(500);
            entity.Property(e => e.SessionId)
                  .HasMaxLength(100);
            entity.Property(e => e.Action)
                  .HasMaxLength(200);

            // Create indexes for better performance
            entity.HasIndex(e => new { e.TableName, e.Timestamp })
                  .HasDatabaseName("IX_audit_trails_TableName_Timestamp");
            entity.HasIndex(e => new { e.UserId, e.Timestamp })
                  .HasDatabaseName("IX_audit_trails_UserId_Timestamp");
            entity.HasIndex(e => new { e.Operation, e.Timestamp })
                  .HasDatabaseName("IX_audit_trails_Operation_Timestamp");
        });

        // NEW: User Action Log Configuration
        modelBuilder.Entity<UserActionLog>(entity =>
        {
            entity.ToTable("user_action_logs");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId)
                  .UseIdentityColumn()
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.Action)
                  .HasMaxLength(100)
                  .IsRequired();
            entity.Property(e => e.Controller)
                  .HasMaxLength(100);
            entity.Property(e => e.Description)
                  .HasMaxLength(200);
            entity.Property(e => e.Timestamp)
                  .HasColumnType("datetime2")
                  .IsRequired();
            entity.Property(e => e.IpAddress)
                  .HasMaxLength(100);
            entity.Property(e => e.UserAgent)
                  .HasMaxLength(500);
            entity.Property(e => e.SessionId)
                  .HasMaxLength(100);
            entity.Property(e => e.Result)
                  .HasMaxLength(50);
            entity.Property(e => e.ErrorMessage)
                  .HasMaxLength(500);

            // Create indexes for better performance
            entity.HasIndex(e => new { e.UserId, e.Timestamp })
                  .HasDatabaseName("IX_user_action_logs_UserId_Timestamp");
            entity.HasIndex(e => new { e.Action, e.Timestamp })
                  .HasDatabaseName("IX_user_action_logs_Action_Timestamp");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

// Supporting classes for audit functionality
public class AuditEntry
{
    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public EntityEntry Entry { get; }
    public string UserId { get; set; } = null!;
    public string TableName { get; set; } = null!;
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<string> ChangedColumns { get; } = new();
    public AuditType AuditType { get; set; }
    public List<PropertyEntry> TemporaryProperties { get; } = new();
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
}

public enum AuditType
{
    None = 0,
    Create = 1,
    Update = 2,
    Delete = 3
}