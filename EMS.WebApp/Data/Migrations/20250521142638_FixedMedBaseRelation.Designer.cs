﻿// <auto-generated />
using System;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EMS.WebApp.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250521142638_FixedMedBaseRelation")]
    partial class FixedMedBaseRelation
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("EMS.WebApp.Data.MedBase", b =>
                {
                    b.Property<int>("BaseId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("base_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("BaseId"));

                    b.Property<string>("BaseDesc")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("base_desc");

                    b.Property<string>("BaseName")
                        .IsRequired()
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("base_name");

                    b.HasKey("BaseId");

                    b.ToTable("med_base", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedCategory", b =>
                {
                    b.Property<int>("MedCatId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("medcat_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("MedCatId"));

                    b.Property<string>("Description")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("description");

                    b.Property<string>("MedCatName")
                        .IsRequired()
                        .HasMaxLength(80)
                        .IsUnicode(false)
                        .HasColumnType("varchar(80)")
                        .HasColumnName("medcat_name");

                    b.Property<string>("Remarks")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("remarks");

                    b.HasKey("MedCatId");

                    b.ToTable("med_category", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedDisease", b =>
                {
                    b.Property<int>("DiseaseId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("disease_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DiseaseId"));

                    b.Property<string>("DiseaseDesc")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)")
                        .HasColumnName("disease_desc");

                    b.Property<string>("DiseaseName")
                        .IsRequired()
                        .HasMaxLength(120)
                        .HasColumnType("nvarchar(120)")
                        .HasColumnName("disease_name");

                    b.HasKey("DiseaseId");

                    b.ToTable("med_disease", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedExamCategory", b =>
                {
                    b.Property<int>("CatId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("cat_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("CatId"));

                    b.Property<string>("AnnuallyRule")
                        .IsRequired()
                        .HasMaxLength(10)
                        .IsUnicode(false)
                        .HasColumnType("varchar(10)")
                        .HasColumnName("annually_rule");

                    b.Property<string>("CatName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasColumnType("varchar(100)")
                        .HasColumnName("cat_name");

                    b.Property<string>("MonthsSched")
                        .IsRequired()
                        .HasMaxLength(50)
                        .IsUnicode(false)
                        .HasColumnType("varchar(50)")
                        .HasColumnName("months_sched");

                    b.Property<string>("Remarks")
                        .HasMaxLength(200)
                        .IsUnicode(false)
                        .HasColumnType("varchar(200)")
                        .HasColumnName("remarks");

                    b.Property<byte>("YearsFreq")
                        .HasColumnType("tinyint")
                        .HasColumnName("years_freq");

                    b.HasKey("CatId");

                    b.ToTable("med_exam_category", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedMaster", b =>
                {
                    b.Property<int>("MedItemId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("med_item_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("MedItemId"));

                    b.Property<int?>("BaseId")
                        .HasColumnType("int")
                        .HasColumnName("base_id");

                    b.Property<string>("CompanyName")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("company_name");

                    b.Property<string>("MedItemName")
                        .IsRequired()
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)")
                        .HasColumnName("med_item_name");

                    b.Property<string>("Potency")
                        .HasMaxLength(40)
                        .IsUnicode(false)
                        .HasColumnType("varchar(40)")
                        .HasColumnName("potency");

                    b.Property<int>("ReorderLimit")
                        .HasColumnType("int")
                        .HasColumnName("reorder_limit");

                    b.Property<int?>("SafeDose")
                        .HasColumnType("int")
                        .HasColumnName("safe_dose");

                    b.HasKey("MedItemId");

                    b.HasIndex("BaseId");

                    b.ToTable("med_master", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedRefHospital", b =>
                {
                    b.Property<int>("hosp_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasColumnName("hosp_id");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("hosp_id"));

                    b.Property<string>("address")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("address");

                    b.Property<string>("contact_person_email_id")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("contact_person_email_id");

                    b.Property<string>("contact_person_name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("contact_person_name");

                    b.Property<string>("description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("description");

                    b.Property<string>("hosp_code")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("hosp_code");

                    b.Property<string>("hosp_name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("hosp_name");

                    b.Property<long?>("mobile_number_1")
                        .HasColumnType("bigint")
                        .HasColumnName("mobile_number_1");

                    b.Property<long?>("mobile_number_2")
                        .HasColumnType("bigint")
                        .HasColumnName("mobile_number_2");

                    b.Property<long?>("phone_number_1")
                        .HasColumnType("bigint")
                        .HasColumnName("phone_number_1");

                    b.Property<long?>("phone_number_2")
                        .HasColumnType("bigint")
                        .HasColumnName("phone_number_2");

                    b.Property<string>("speciality")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("speciality");

                    b.Property<string>("tax_category")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("tax_category");

                    b.Property<string>("vendor_code")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("vendor_code");

                    b.Property<string>("vendor_name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("vendor_name");

                    b.HasKey("hosp_id");

                    b.ToTable("med_ref_hospital", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.hr_employee", b =>
                {
                    b.Property<string>("emp_id")
                        .HasMaxLength(10)
                        .IsUnicode(false)
                        .HasColumnType("varchar(10)");

                    b.Property<short>("dept_id")
                        .HasColumnType("smallint");

                    b.Property<DateOnly>("emp_DOB")
                        .HasColumnType("date");

                    b.Property<string>("emp_Gender")
                        .IsRequired()
                        .HasMaxLength(1)
                        .IsUnicode(false)
                        .HasColumnType("char(1)")
                        .IsFixedLength();

                    b.Property<string>("emp_Grade")
                        .IsRequired()
                        .HasMaxLength(10)
                        .IsUnicode(false)
                        .HasColumnType("varchar(10)");

                    b.Property<string>("emp_name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasColumnType("varchar(100)");

                    b.HasKey("emp_id")
                        .HasName("PK__hr_emplo__1299A8610F1C30AD");

                    b.ToTable("hr_employee", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.hr_employee_dependent", b =>
                {
                    b.Property<int>("emp_dep_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("emp_dep_id"));

                    b.Property<DateOnly?>("dep_dob")
                        .HasColumnType("date");

                    b.Property<string>("dep_name")
                        .IsRequired()
                        .HasMaxLength(80)
                        .IsUnicode(false)
                        .HasColumnType("varchar(80)");

                    b.Property<string>("emp_id")
                        .IsRequired()
                        .HasMaxLength(10)
                        .IsUnicode(false)
                        .HasColumnType("varchar(10)");

                    b.Property<string>("gender")
                        .IsRequired()
                        .HasMaxLength(1)
                        .IsUnicode(false)
                        .HasColumnType("char(1)")
                        .IsFixedLength();

                    b.Property<bool>("is_active")
                        .HasColumnType("bit");

                    b.Property<bool?>("marital_status")
                        .HasColumnType("bit");

                    b.Property<string>("relation")
                        .IsRequired()
                        .HasMaxLength(20)
                        .IsUnicode(false)
                        .HasColumnType("varchar(20)");

                    b.HasKey("emp_dep_id")
                        .HasName("PK__hr_emplo__F9EA96E640CC1C95");

                    b.ToTable("hr_employee_dependent", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.med_ambulance_master", b =>
                {
                    b.Property<int>("amb_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("amb_id"));

                    b.Property<bool>("is_active")
                        .HasColumnType("bit");

                    b.Property<byte>("max_capacity")
                        .HasColumnType("tinyint");

                    b.Property<string>("provider")
                        .IsRequired()
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)");

                    b.Property<string>("vehicle_no")
                        .IsRequired()
                        .HasMaxLength(20)
                        .IsUnicode(false)
                        .HasColumnType("varchar(20)");

                    b.Property<string>("vehicle_type")
                        .HasMaxLength(15)
                        .IsUnicode(false)
                        .HasColumnType("varchar(15)");

                    b.HasKey("amb_id")
                        .HasName("PK__med_ambu__9FDA4AE611BCF6CF");

                    b.ToTable("med_ambulance_master", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.org_department", b =>
                {
                    b.Property<short>("dept_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("smallint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<short>("dept_id"));

                    b.Property<string>("Remarks")
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("dept_description")
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("dept_name")
                        .IsRequired()
                        .HasMaxLength(25)
                        .IsUnicode(false)
                        .HasColumnType("varchar(25)");

                    b.HasKey("dept_id")
                        .HasName("PK__org_depa__DCA65974896CEDB8");

                    b.ToTable("org_department", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.org_plant", b =>
                {
                    b.Property<short>("plant_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("smallint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<short>("plant_id"));

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .IsUnicode(false)
                        .HasColumnType("varchar(200)");

                    b.Property<string>("plant_code")
                        .IsRequired()
                        .HasMaxLength(10)
                        .IsUnicode(false)
                        .HasColumnType("varchar(10)");

                    b.Property<string>("plant_name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasColumnType("varchar(100)");

                    b.HasKey("plant_id")
                        .HasName("PK__org_plan__A576B3B47C5A3448");

                    b.ToTable("org_plant", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.sys_attach_screen_role", b =>
                {
                    b.Property<int>("role_uid")
                        .HasColumnType("int");

                    b.Property<int>("screen_uid")
                        .HasColumnType("int");

                    b.Property<int>("uid")
                        .HasColumnType("int");

                    b.ToTable("sys_attach_screen_role", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.sys_role", b =>
                {
                    b.Property<short>("role_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("smallint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<short>("role_id"));

                    b.Property<string>("role_desc")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)");

                    b.Property<string>("role_name")
                        .IsRequired()
                        .HasMaxLength(40)
                        .IsUnicode(false)
                        .HasColumnType("varchar(40)");

                    b.HasKey("role_id")
                        .HasName("PK__sys_role__760965CC3DA5ABD8");

                    b.ToTable("sys_role", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.sys_screen_name", b =>
                {
                    b.Property<int>("screen_uid")
                        .HasColumnType("int");

                    b.Property<string>("screen_description")
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)");

                    b.Property<string>("screen_name")
                        .IsRequired()
                        .HasMaxLength(40)
                        .IsUnicode(false)
                        .HasColumnType("varchar(40)");

                    b.HasKey("screen_uid")
                        .HasName("PK__sys_scre__B2C9B83A098D0056");

                    b.ToTable("sys_screen_name", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.sys_user", b =>
                {
                    b.Property<int>("user_id")
                        .HasColumnType("int");

                    b.Property<string>("email")
                        .IsRequired()
                        .HasMaxLength(120)
                        .IsUnicode(false)
                        .HasColumnType("varchar(120)");

                    b.Property<string>("full_name")
                        .IsRequired()
                        .HasMaxLength(80)
                        .IsUnicode(false)
                        .HasColumnType("varchar(80)");

                    b.Property<bool>("is_active")
                        .HasColumnType("bit");

                    b.Property<int>("role_id")
                        .HasColumnType("int");

                    b.HasKey("user_id")
                        .HasName("PK__sys_user__B9BE370F9ECB6AC8");

                    b.ToTable("sys_user", (string)null);
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedMaster", b =>
                {
                    b.HasOne("EMS.WebApp.Data.MedBase", "MedBase")
                        .WithMany("MedMasters")
                        .HasForeignKey("BaseId");

                    b.Navigation("MedBase");
                });

            modelBuilder.Entity("EMS.WebApp.Data.MedBase", b =>
                {
                    b.Navigation("MedMasters");
                });
#pragma warning restore 612, 618
        }
    }
}
