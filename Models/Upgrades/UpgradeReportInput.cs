﻿namespace CarCareTracker.Models
{
    public class UpgradeRecordInput
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string Date { get; set; }
        public int Mileage { get; set; }
        public string Description { get; set; }
        public decimal Cost { get; set; }
        public string Notes { get; set; }
        public List<UploadedFiles> Files { get; set; } = new List<UploadedFiles>();
        public UpgradeRecord ToUpgradeRecord() { return new UpgradeRecord { Id = Id, VehicleId = VehicleId, Date = DateTime.Parse(Date), Cost = Cost, Mileage = Mileage, Description = Description, Notes = Notes, Files = Files }; }
    }
}
