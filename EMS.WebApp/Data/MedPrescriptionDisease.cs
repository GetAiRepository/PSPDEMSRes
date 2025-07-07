using EMS.WebApp.Data.Migrations;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class MedPrescriptionDisease
    {
        [Key]
        public int PrescriptionDiseaseId { get; set; }

        public int PrescriptionId { get; set; }
        public int DiseaseId { get; set; }

        // Navigation properties
        public virtual MedPrescription? MedPrescription { get; set; }
        public virtual MedDisease? MedDisease { get; set; }
    }
}
