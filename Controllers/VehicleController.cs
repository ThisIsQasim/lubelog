using CarCareTracker.External.Interfaces;
using CarCareTracker.Models;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using CarCareTracker.Helper;
using CsvHelper;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using CarCareTracker.MapProfile;

namespace CarCareTracker.Controllers
{
    [Authorize]
    public class VehicleController : Controller
    {
        private readonly ILogger<VehicleController> _logger;
        private readonly IVehicleDataAccess _dataAccess;
        private readonly INoteDataAccess _noteDataAccess;
        private readonly IServiceRecordDataAccess _serviceRecordDataAccess;
        private readonly IGasRecordDataAccess _gasRecordDataAccess;
        private readonly ICollisionRecordDataAccess _collisionRecordDataAccess;
        private readonly ITaxRecordDataAccess _taxRecordDataAccess;
        private readonly IReminderRecordDataAccess _reminderRecordDataAccess;
        private readonly IUpgradeRecordDataAccess _upgradeRecordDataAccess;
        private readonly IWebHostEnvironment _webEnv;
        private readonly bool _useDescending;
        private readonly IConfiguration _config;
        private readonly IFileHelper _fileHelper;
        private readonly IGasHelper _gasHelper;
        private readonly IReminderHelper _reminderHelper;
        private readonly IReportHelper _reportHelper;

        public VehicleController(ILogger<VehicleController> logger,
            IFileHelper fileHelper,
            IGasHelper gasHelper,
            IReminderHelper reminderHelper,
            IReportHelper reportHelper,
            IVehicleDataAccess dataAccess,
            INoteDataAccess noteDataAccess,
            IServiceRecordDataAccess serviceRecordDataAccess,
            IGasRecordDataAccess gasRecordDataAccess,
            ICollisionRecordDataAccess collisionRecordDataAccess,
            ITaxRecordDataAccess taxRecordDataAccess,
            IReminderRecordDataAccess reminderRecordDataAccess,
            IUpgradeRecordDataAccess upgradeRecordDataAccess,
            IWebHostEnvironment webEnv,
            IConfiguration config)
        {
            _logger = logger;
            _dataAccess = dataAccess;
            _noteDataAccess = noteDataAccess;
            _fileHelper = fileHelper;
            _gasHelper = gasHelper;
            _reminderHelper = reminderHelper;
            _reportHelper = reportHelper;
            _serviceRecordDataAccess = serviceRecordDataAccess;
            _gasRecordDataAccess = gasRecordDataAccess;
            _collisionRecordDataAccess = collisionRecordDataAccess;
            _taxRecordDataAccess = taxRecordDataAccess;
            _reminderRecordDataAccess = reminderRecordDataAccess;
            _upgradeRecordDataAccess = upgradeRecordDataAccess;
            _webEnv = webEnv;
            _config = config;
            _useDescending = bool.Parse(config[nameof(UserConfig.UseDescending)]);
        }
        [HttpGet]
        public IActionResult Index(int vehicleId)
        {
            var data = _dataAccess.GetVehicleById(vehicleId);
            return View(data);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult AddVehiclePartialView()
        {
            return PartialView("_VehicleModal", new Vehicle());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetEditVehiclePartialViewById(int vehicleId)
        {
            var data = _dataAccess.GetVehicleById(vehicleId);
            return PartialView("_VehicleModal", data);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveVehicle(Vehicle vehicleInput)
        {
            try
            {
                //move image from temp folder to images folder.
                vehicleInput.ImageLocation = _fileHelper.MoveFileFromTemp(vehicleInput.ImageLocation, "images/");
                //save vehicle.
                var result = _dataAccess.SaveVehicle(vehicleInput);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Saving Vehicle");
                return Json(false);
            }
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteVehicle(int vehicleId)
        {
            //Delete all service records, gas records, notes, etc.
            var result = _gasRecordDataAccess.DeleteAllGasRecordsByVehicleId(vehicleId) &&
                _serviceRecordDataAccess.DeleteAllServiceRecordsByVehicleId(vehicleId) &&
                _collisionRecordDataAccess.DeleteAllCollisionRecordsByVehicleId(vehicleId) &&
                _taxRecordDataAccess.DeleteAllTaxRecordsByVehicleId(vehicleId) &&
                _noteDataAccess.DeleteAllNotesByVehicleId(vehicleId) &&
                _reminderRecordDataAccess.DeleteAllReminderRecordsByVehicleId(vehicleId) &&
                _upgradeRecordDataAccess.DeleteAllUpgradeRecordsByVehicleId(vehicleId) &&
                _dataAccess.DeleteVehicle(vehicleId);
            return Json(result);
        }
        #region "Bulk Imports"
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetBulkImportModalPartialView(ImportMode mode)
        {
            return PartialView("_BulkDataImporter", mode);
        }
        [HttpGet]
        public IActionResult ExportFromVehicleToCsv(int vehicleId, ImportMode mode)
        {
            if (vehicleId == default)
            {
                return Json(false);
            }
            string uploadDirectory = "temp/";
            string uploadPath = Path.Combine(_webEnv.WebRootPath, uploadDirectory);
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);
            if (mode == ImportMode.ServiceRecord)
            {
                var fileNameToExport = $"temp/{Guid.NewGuid()}.csv";
                var fullExportFilePath = _fileHelper.GetFullFilePath(fileNameToExport, false);
                var vehicleRecords = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
                if (vehicleRecords.Any())
                {
                    var exportData = vehicleRecords.Select(x => new ServiceRecordExportModel { Date = x.Date.ToShortDateString(), Description = x.Description, Cost = x.Cost.ToString("C"), Notes = x.Notes, Odometer = x.Mileage.ToString() });
                    using (var writer = new StreamWriter(fullExportFilePath))
                    {
                        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            csv.WriteRecords(exportData);
                        }
                    }
                    return Json($"/{fileNameToExport}");
                }
            }
            else if (mode == ImportMode.RepairRecord)
            {
                var fileNameToExport = $"temp/{Guid.NewGuid()}.csv";
                var fullExportFilePath = _fileHelper.GetFullFilePath(fileNameToExport, false);
                var vehicleRecords = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
                if (vehicleRecords.Any())
                {
                    var exportData = vehicleRecords.Select(x => new ServiceRecordExportModel { Date = x.Date.ToShortDateString(), Description = x.Description, Cost = x.Cost.ToString("C"), Notes = x.Notes, Odometer = x.Mileage.ToString() });
                    using (var writer = new StreamWriter(fullExportFilePath))
                    {
                        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            csv.WriteRecords(exportData);
                        }
                    }
                    return Json($"/{fileNameToExport}");
                }
            }
            else if (mode == ImportMode.UpgradeRecord)
            {
                var fileNameToExport = $"temp/{Guid.NewGuid()}.csv";
                var fullExportFilePath = _fileHelper.GetFullFilePath(fileNameToExport, false);
                var vehicleRecords = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
                if (vehicleRecords.Any())
                {
                    var exportData = vehicleRecords.Select(x => new ServiceRecordExportModel { Date = x.Date.ToShortDateString(), Description = x.Description, Cost = x.Cost.ToString("C"), Notes = x.Notes, Odometer = x.Mileage.ToString() });
                    using (var writer = new StreamWriter(fullExportFilePath))
                    {
                        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            csv.WriteRecords(exportData);
                        }
                    }
                    return Json($"/{fileNameToExport}");
                }
            }
            else if (mode == ImportMode.TaxRecord)
            {
                var fileNameToExport = $"temp/{Guid.NewGuid()}.csv";
                var fullExportFilePath = _fileHelper.GetFullFilePath(fileNameToExport, false);
                var vehicleRecords = _taxRecordDataAccess.GetTaxRecordsByVehicleId(vehicleId);
                if (vehicleRecords.Any())
                {
                    var exportData = vehicleRecords.Select(x => new TaxRecordExportModel { Date = x.Date.ToShortDateString(), Description = x.Description, Cost = x.Cost.ToString("C"), Notes = x.Notes });
                    using (var writer = new StreamWriter(fullExportFilePath))
                    {
                        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            csv.WriteRecords(exportData);
                        }
                    }
                    return Json($"/{fileNameToExport}");
                }
            }
            else if (mode == ImportMode.GasRecord)
            {
                var fileNameToExport = $"temp/{Guid.NewGuid()}.csv";
                var fullExportFilePath = _fileHelper.GetFullFilePath(fileNameToExport, false);
                var vehicleRecords = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
                bool useMPG = bool.Parse(_config[nameof(UserConfig.UseMPG)]);
                bool useUKMPG = bool.Parse(_config[nameof(UserConfig.UseUKMPG)]);
                vehicleRecords = vehicleRecords.OrderBy(x => x.Date).ThenBy(x => x.Mileage).ToList();
                var convertedRecords = _gasHelper.GetGasRecordViewModels(vehicleRecords, useMPG, useUKMPG);
                var exportData = convertedRecords.Select(x => new GasRecordExportModel { Date = x.Date.ToString(), Cost = x.Cost.ToString(), FuelConsumed = x.Gallons.ToString(), FuelEconomy = x.MilesPerGallon.ToString(), Odometer = x.Mileage.ToString() });
                using (var writer = new StreamWriter(fullExportFilePath))
                {
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(exportData);
                    }
                }
                return Json($"/{fileNameToExport}");
            }
            return Json(false);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpPost]
        public IActionResult ImportToVehicleIdFromCsv(int vehicleId, ImportMode mode, string fileName)
        {
            if (vehicleId == default || string.IsNullOrWhiteSpace(fileName))
            {
                return Json(false);
            }
            var fullFileName = _fileHelper.GetFullFilePath(fileName);
            if (string.IsNullOrWhiteSpace(fullFileName))
            {
                return Json(false);
            }
            try
            {
                using (var reader = new StreamReader(fullFileName))
                {
                    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture);
                    config.MissingFieldFound = null;
                    config.HeaderValidated = null;
                    config.PrepareHeaderForMatch = args => { return args.Header.Trim().ToLower(); };
                    using (var csv = new CsvReader(reader, config))
                    {
                        csv.Context.RegisterClassMap<FuellyMapper>();
                        var records = csv.GetRecords<ImportModel>().ToList();
                        if (records.Any())
                        {
                            foreach (ImportModel importModel in records)
                            {
                                if (mode == ImportMode.GasRecord)
                                {
                                    //convert to gas model.
                                    var convertedRecord = new GasRecord()
                                    {
                                        VehicleId = vehicleId,
                                        Date = DateTime.Parse(importModel.Date),
                                        Mileage = int.Parse(importModel.Odometer, NumberStyles.Any),
                                        Gallons = decimal.Parse(importModel.FuelConsumed, NumberStyles.Any)
                                    };
                                    if (string.IsNullOrWhiteSpace(importModel.Cost) && !string.IsNullOrWhiteSpace(importModel.Price))
                                    {
                                        //cost was not given but price is.
                                        //fuelly sometimes exports CSVs without total cost.
                                        var parsedPrice = decimal.Parse(importModel.Price, NumberStyles.Any);
                                        convertedRecord.Cost = convertedRecord.Gallons * parsedPrice;
                                    }
                                    else
                                    {
                                        convertedRecord.Cost = decimal.Parse(importModel.Cost, NumberStyles.Any);
                                    }
                                    if (string.IsNullOrWhiteSpace(importModel.IsFillToFull) && !string.IsNullOrWhiteSpace(importModel.PartialFuelUp))
                                    {
                                        var parsedBool = importModel.PartialFuelUp.Trim() == "1";
                                        convertedRecord.IsFillToFull = !parsedBool;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(importModel.IsFillToFull))
                                    {
                                        var parsedBool = importModel.IsFillToFull.Trim() == "1" || importModel.IsFillToFull.Trim() == "Full";
                                        convertedRecord.IsFillToFull = parsedBool;
                                    }
                                    if (!string.IsNullOrWhiteSpace(importModel.MissedFuelUp))
                                    {
                                        var parsedBool = importModel.MissedFuelUp.Trim() == "1";
                                        convertedRecord.MissedFuelUp = parsedBool;
                                    }
                                    //insert record into db, check to make sure fuelconsumed is not zero so we don't get a divide by zero error.
                                    if (convertedRecord.Gallons > 0)
                                    {
                                        _gasRecordDataAccess.SaveGasRecordToVehicle(convertedRecord);
                                    }
                                }
                                else if (mode == ImportMode.ServiceRecord)
                                {
                                    var convertedRecord = new ServiceRecord()
                                    {
                                        VehicleId = vehicleId,
                                        Date = DateTime.Parse(importModel.Date),
                                        Mileage = int.Parse(importModel.Odometer, NumberStyles.Any),
                                        Description = string.IsNullOrWhiteSpace(importModel.Description) ? $"Service Record on {importModel.Date}" : importModel.Description,
                                        Notes = string.IsNullOrWhiteSpace(importModel.Notes) ? "" : importModel.Notes,
                                        Cost = decimal.Parse(importModel.Cost, NumberStyles.Any)
                                    };
                                    _serviceRecordDataAccess.SaveServiceRecordToVehicle(convertedRecord);
                                }
                                else if (mode == ImportMode.RepairRecord)
                                {
                                    var convertedRecord = new CollisionRecord()
                                    {
                                        VehicleId = vehicleId,
                                        Date = DateTime.Parse(importModel.Date),
                                        Mileage = int.Parse(importModel.Odometer, NumberStyles.Any),
                                        Description = string.IsNullOrWhiteSpace(importModel.Description) ? $"Repair Record on {importModel.Date}" : importModel.Description,
                                        Notes = string.IsNullOrWhiteSpace(importModel.Notes) ? "" : importModel.Notes,
                                        Cost = decimal.Parse(importModel.Cost, NumberStyles.Any)
                                    };
                                    _collisionRecordDataAccess.SaveCollisionRecordToVehicle(convertedRecord);
                                }
                                else if (mode == ImportMode.UpgradeRecord)
                                {
                                    var convertedRecord = new UpgradeRecord()
                                    {
                                        VehicleId = vehicleId,
                                        Date = DateTime.Parse(importModel.Date),
                                        Mileage = int.Parse(importModel.Odometer, NumberStyles.Any),
                                        Description = string.IsNullOrWhiteSpace(importModel.Description) ? $"Upgrade Record on {importModel.Date}" : importModel.Description,
                                        Notes = string.IsNullOrWhiteSpace(importModel.Notes) ? "" : importModel.Notes,
                                        Cost = decimal.Parse(importModel.Cost, NumberStyles.Any)
                                    };
                                    _upgradeRecordDataAccess.SaveUpgradeRecordToVehicle(convertedRecord);
                                }
                                else if (mode == ImportMode.TaxRecord)
                                {
                                    var convertedRecord = new TaxRecord()
                                    {
                                        VehicleId = vehicleId,
                                        Date = DateTime.Parse(importModel.Date),
                                        Description = string.IsNullOrWhiteSpace(importModel.Description) ? $"Tax Record on {importModel.Date}" : importModel.Description,
                                        Notes = string.IsNullOrWhiteSpace(importModel.Notes) ? "" : importModel.Notes,
                                        Cost = decimal.Parse(importModel.Cost, NumberStyles.Any)
                                    };
                                    _taxRecordDataAccess.SaveTaxRecordToVehicle(convertedRecord);
                                }
                            }
                        }
                    }
                }
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Occurred While Bulk Inserting");
                return Json(false);
            }
        }
        #endregion
        #region "Gas Records"
        [HttpGet]
        public IActionResult GetGasRecordsByVehicleId(int vehicleId)
        {
            var result = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
            //need it in ascending order to perform computation.
            result = result.OrderBy(x => x.Date).ThenBy(x => x.Mileage).ToList();
            //check if the user uses MPG or Liters per 100km.
            bool useMPG = bool.Parse(_config[nameof(UserConfig.UseMPG)]);
            bool useUKMPG = bool.Parse(_config[nameof(UserConfig.UseUKMPG)]);
            var computedResults = _gasHelper.GetGasRecordViewModels(result, useMPG, useUKMPG);
            if (_useDescending)
            {
                computedResults = computedResults.OrderByDescending(x => DateTime.Parse(x.Date)).ThenByDescending(x => x.Mileage).ToList();
            }
            var vehicleIsElectric = _dataAccess.GetVehicleById(vehicleId).IsElectric;
            var viewModel = new GasRecordViewModelContainer()
            {
                UseKwh = vehicleIsElectric,
                GasRecords = computedResults
            };
            return PartialView("_Gas", viewModel);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveGasRecordToVehicleId(GasRecordInput gasRecord)
        {
            gasRecord.Files = gasRecord.Files.Select(x => { return new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }; }).ToList();
            var result = _gasRecordDataAccess.SaveGasRecordToVehicle(gasRecord.ToGasRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddGasRecordPartialView()
        {
            return PartialView("_GasModal", new GasRecordInputContainer() { GasRecord = new GasRecordInput() });
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetGasRecordForEditById(int gasRecordId)
        {
            var result = _gasRecordDataAccess.GetGasRecordById(gasRecordId);
            var convertedResult = new GasRecordInput
            {
                Id = result.Id,
                Mileage = result.Mileage,
                VehicleId = result.VehicleId,
                Cost = result.Cost,
                Date = result.Date.ToShortDateString(),
                Files = result.Files,
                Gallons = result.Gallons,
                IsFillToFull = result.IsFillToFull,
                MissedFuelUp = result.MissedFuelUp
            };
            var vehicleIsElectric = _dataAccess.GetVehicleById(convertedResult.VehicleId).IsElectric;
            var viewModel = new GasRecordInputContainer()
            {
                UseKwh = vehicleIsElectric,
                GasRecord = convertedResult
            };
            return PartialView("_GasModal", viewModel);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteGasRecordById(int gasRecordId)
        {
            var result = _gasRecordDataAccess.DeleteGasRecordById(gasRecordId);
            return Json(result);
        }
        #endregion
        #region "Service Records"
        [HttpGet]
        public IActionResult GetServiceRecordsByVehicleId(int vehicleId)
        {
            var result = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
            if (_useDescending)
            {
                result = result.OrderByDescending(x => x.Date).ThenByDescending(x => x.Mileage).ToList();
            }
            else
            {
                result = result.OrderBy(x => x.Date).ThenBy(x => x.Mileage).ToList();
            }
            return PartialView("_ServiceRecords", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveServiceRecordToVehicleId(ServiceRecordInput serviceRecord)
        {
            //move files from temp.
            serviceRecord.Files = serviceRecord.Files.Select(x => { return new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }; }).ToList();
            var result = _serviceRecordDataAccess.SaveServiceRecordToVehicle(serviceRecord.ToServiceRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddServiceRecordPartialView()
        {
            return PartialView("_ServiceRecordModal", new ServiceRecordInput());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetServiceRecordForEditById(int serviceRecordId)
        {
            var result = _serviceRecordDataAccess.GetServiceRecordById(serviceRecordId);
            //convert to Input object.
            var convertedResult = new ServiceRecordInput
            {
                Id = result.Id,
                Cost = result.Cost,
                Date = result.Date.ToShortDateString(),
                Description = result.Description,
                Mileage = result.Mileage,
                Notes = result.Notes,
                VehicleId = result.VehicleId,
                Files = result.Files
            };
            return PartialView("_ServiceRecordModal", convertedResult);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteServiceRecordById(int serviceRecordId)
        {
            var result = _serviceRecordDataAccess.DeleteServiceRecordById(serviceRecordId);
            return Json(result);
        }
        #endregion
        #region "Collision Records"
        [HttpGet]
        public IActionResult GetCollisionRecordsByVehicleId(int vehicleId)
        {
            var result = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
            if (_useDescending)
            {
                result = result.OrderByDescending(x => x.Date).ThenByDescending(x => x.Mileage).ToList();
            }
            else
            {
                result = result.OrderBy(x => x.Date).ThenBy(x => x.Mileage).ToList();
            }
            return PartialView("_CollisionRecords", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveCollisionRecordToVehicleId(CollisionRecordInput collisionRecord)
        {
            //move files from temp.
            collisionRecord.Files = collisionRecord.Files.Select(x => { return new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }; }).ToList();
            var result = _collisionRecordDataAccess.SaveCollisionRecordToVehicle(collisionRecord.ToCollisionRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddCollisionRecordPartialView()
        {
            return PartialView("_CollisionRecordModal", new CollisionRecordInput());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetCollisionRecordForEditById(int collisionRecordId)
        {
            var result = _collisionRecordDataAccess.GetCollisionRecordById(collisionRecordId);
            //convert to Input object.
            var convertedResult = new CollisionRecordInput
            {
                Id = result.Id,
                Cost = result.Cost,
                Date = result.Date.ToShortDateString(),
                Description = result.Description,
                Mileage = result.Mileage,
                Notes = result.Notes,
                VehicleId = result.VehicleId,
                Files = result.Files
            };
            return PartialView("_CollisionRecordModal", convertedResult);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteCollisionRecordById(int collisionRecordId)
        {
            var result = _collisionRecordDataAccess.DeleteCollisionRecordById(collisionRecordId);
            return Json(result);
        }
        #endregion
        #region "Tax Records"
        [HttpGet]
        public IActionResult GetTaxRecordsByVehicleId(int vehicleId)
        {
            var result = _taxRecordDataAccess.GetTaxRecordsByVehicleId(vehicleId);
            if (_useDescending)
            {
                result = result.OrderByDescending(x => x.Date).ToList();
            }
            else
            {
                result = result.OrderBy(x => x.Date).ToList();
            }
            return PartialView("_TaxRecords", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveTaxRecordToVehicleId(TaxRecordInput taxRecord)
        {
            //move files from temp.
            taxRecord.Files = taxRecord.Files.Select(x => { return new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }; }).ToList();
            var result = _taxRecordDataAccess.SaveTaxRecordToVehicle(taxRecord.ToTaxRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddTaxRecordPartialView()
        {
            return PartialView("_TaxRecordModal", new TaxRecordInput());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetTaxRecordForEditById(int taxRecordId)
        {
            var result = _taxRecordDataAccess.GetTaxRecordById(taxRecordId);
            //convert to Input object.
            var convertedResult = new TaxRecordInput
            {
                Id = result.Id,
                Cost = result.Cost,
                Date = result.Date.ToShortDateString(),
                Description = result.Description,
                Notes = result.Notes,
                VehicleId = result.VehicleId,
                Files = result.Files
            };
            return PartialView("_TaxRecordModal", convertedResult);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteTaxRecordById(int taxRecordId)
        {
            var result = _taxRecordDataAccess.DeleteTaxRecordById(taxRecordId);
            return Json(result);
        }
        #endregion
        #region "Reports"
        [HttpGet]
        public IActionResult GetReportPartialView(int vehicleId)
        {
            //get records
            var serviceRecords = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
            var gasRecords = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
            var collisionRecords = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
            var taxRecords = _taxRecordDataAccess.GetTaxRecordsByVehicleId(vehicleId);
            var upgradeRecords = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
            var viewModel = new ReportViewModel();
            //get totalCostMakeUp
            viewModel.CostMakeUpForVehicle = new CostMakeUpForVehicle
            {
                ServiceRecordSum = serviceRecords.Sum(x => x.Cost),
                GasRecordSum = gasRecords.Sum(x => x.Cost),
                CollisionRecordSum = collisionRecords.Sum(x => x.Cost),
                TaxRecordSum = taxRecords.Sum(x => x.Cost),
                UpgradeRecordSum = upgradeRecords.Sum(x => x.Cost)
            };
            //get costbymonth
            List<CostForVehicleByMonth> allCosts = new List<CostForVehicleByMonth>();
            allCosts.AddRange(_reportHelper.GetServiceRecordSum(serviceRecords, 0));
            allCosts.AddRange(_reportHelper.GetRepairRecordSum(collisionRecords, 0));
            allCosts.AddRange(_reportHelper.GetUpgradeRecordSum(upgradeRecords, 0));
            allCosts.AddRange(_reportHelper.GetUpgradeRecordSum(upgradeRecords, 0));
            allCosts.AddRange(_reportHelper.GetGasRecordSum(gasRecords, 0));
            allCosts.AddRange(_reportHelper.GetTaxRecordSum(taxRecords, 0));
            viewModel.CostForVehicleByMonth = allCosts.GroupBy(x => x.MonthName).OrderBy(x => x.Key).Select(x => new CostForVehicleByMonth
            {
                MonthName = x.Key,
                Cost = x.Sum(y => y.Cost)
            }).ToList();
            //get reminders
            var reminders = GetRemindersAndUrgency(vehicleId, DateTime.Now);
            viewModel.ReminderMakeUpForVehicle = new ReminderMakeUpForVehicle
            {
                NotUrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.NotUrgent).Count(),
                UrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.Urgent).Count(),
                VeryUrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.VeryUrgent).Count(),
                PastDueCount = reminders.Where(x => x.Urgency == ReminderUrgency.PastDue).Count()
            };
            //populate year dropdown.
            var numbersArray = new List<int>();
            if (serviceRecords.Any())
            {
                numbersArray.Add(serviceRecords.Min(x => x.Date.Year));
            }
            if (collisionRecords.Any())
            {
                numbersArray.Add(collisionRecords.Min(x => x.Date.Year));
            }
            if (gasRecords.Any())
            {
                numbersArray.Add(gasRecords.Min(x => x.Date.Year));
            }
            if (upgradeRecords.Any())
            {
                numbersArray.Add(upgradeRecords.Min(x => x.Date.Year));
            }
            var minYear = numbersArray.Any() ? numbersArray.Min() : DateTime.Now.AddYears(-5).Year;
            var yearDifference = DateTime.Now.Year - minYear + 1;
            for (int i = 0; i < yearDifference; i++)
            {
                viewModel.Years.Add(DateTime.Now.AddYears(i * -1).Year);
            }
            return PartialView("_Report", viewModel);
        }
        [HttpGet]
        public IActionResult GetCostMakeUpForVehicle(int vehicleId, int year = 0)
        {
            var serviceRecords = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
            var gasRecords = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
            var collisionRecords = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
            var taxRecords = _taxRecordDataAccess.GetTaxRecordsByVehicleId(vehicleId);
            var upgradeRecords = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
            if (year != default)
            {
                serviceRecords.RemoveAll(x => x.Date.Year != year);
                gasRecords.RemoveAll(x => x.Date.Year != year);
                collisionRecords.RemoveAll(x => x.Date.Year != year);
                taxRecords.RemoveAll(x => x.Date.Year != year);
                upgradeRecords.RemoveAll(x => x.Date.Year != year);
            }
            var viewModel = new CostMakeUpForVehicle
            {
                ServiceRecordSum = serviceRecords.Sum(x => x.Cost),
                GasRecordSum = gasRecords.Sum(x => x.Cost),
                CollisionRecordSum = collisionRecords.Sum(x => x.Cost),
                TaxRecordSum = taxRecords.Sum(x => x.Cost),
                UpgradeRecordSum = upgradeRecords.Sum(x => x.Cost)
            };
            return PartialView("_CostMakeUpReport", viewModel);
        }
        public IActionResult GetReminderMakeUpByVehicle(int vehicleId, int daysToAdd)
        {
            var reminders = GetRemindersAndUrgency(vehicleId, DateTime.Now.AddDays(daysToAdd));
            var viewModel = new ReminderMakeUpForVehicle
            {
                NotUrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.NotUrgent).Count(),
                UrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.Urgent).Count(),
                VeryUrgentCount = reminders.Where(x => x.Urgency == ReminderUrgency.VeryUrgent).Count(),
                PastDueCount = reminders.Where(x => x.Urgency == ReminderUrgency.PastDue).Count()
            };
            return PartialView("_ReminderMakeUpReport", viewModel);
        }
        [HttpPost]
        public IActionResult GetCostByMonthByVehicle(int vehicleId, List<ImportMode> selectedMetrics, int year = 0)
        {
            List<CostForVehicleByMonth> allCosts = new List<CostForVehicleByMonth>();
            if (selectedMetrics.Contains(ImportMode.ServiceRecord))
            {
                var serviceRecords = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
                allCosts.AddRange(_reportHelper.GetServiceRecordSum(serviceRecords, year));
            }
            if (selectedMetrics.Contains(ImportMode.RepairRecord))
            {
                var repairRecords = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
                allCosts.AddRange(_reportHelper.GetRepairRecordSum(repairRecords, year));
            }
            if (selectedMetrics.Contains(ImportMode.UpgradeRecord))
            {
                var upgradeRecords = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
                allCosts.AddRange(_reportHelper.GetUpgradeRecordSum(upgradeRecords, year));
            }
            if (selectedMetrics.Contains(ImportMode.GasRecord))
            {
                var gasRecords = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
                allCosts.AddRange(_reportHelper.GetGasRecordSum(gasRecords, year));
            }
            if (selectedMetrics.Contains(ImportMode.TaxRecord))
            {
                var taxRecords = _taxRecordDataAccess.GetTaxRecordsByVehicleId(vehicleId);
                allCosts.AddRange(_reportHelper.GetTaxRecordSum(taxRecords, year));
            }
            var groupedRecord = allCosts.GroupBy(x => x.MonthName).OrderBy(x => x.Key).Select(x => new CostForVehicleByMonth
            {
                MonthName = x.Key,
                Cost = x.Sum(y => y.Cost)
            }).ToList();
            return PartialView("_GasCostByMonthReport", groupedRecord);
        }
        #endregion
        #region "Reminders"
        private int GetMaxMileage(int vehicleId)
        {
            var numbersArray = new List<int>();
            var serviceRecords = _serviceRecordDataAccess.GetServiceRecordsByVehicleId(vehicleId);
            if (serviceRecords.Any())
            {
                numbersArray.Add(serviceRecords.Max(x => x.Mileage));
            }
            var repairRecords = _collisionRecordDataAccess.GetCollisionRecordsByVehicleId(vehicleId);
            if (repairRecords.Any())
            {
                numbersArray.Add(repairRecords.Max(x => x.Mileage));
            }
            var gasRecords = _gasRecordDataAccess.GetGasRecordsByVehicleId(vehicleId);
            if (gasRecords.Any())
            {
                numbersArray.Add(gasRecords.Max(x => x.Mileage));
            }
            var upgradeRecords = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
            if (upgradeRecords.Any())
            {
                numbersArray.Add(upgradeRecords.Max(x => x.Mileage));
            }
            return numbersArray.Any() ? numbersArray.Max() : 0;
        }
        private List<ReminderRecordViewModel> GetRemindersAndUrgency(int vehicleId, DateTime dateCompare)
        {
            var currentMileage = GetMaxMileage(vehicleId);
            var reminders = _reminderRecordDataAccess.GetReminderRecordsByVehicleId(vehicleId);
            List<ReminderRecordViewModel> results = _reminderHelper.GetReminderRecordViewModels(reminders, currentMileage, dateCompare);
            return results;
        }
        [HttpGet]
        public IActionResult GetVehicleHaveUrgentOrPastDueReminders(int vehicleId)
        {
            var result = GetRemindersAndUrgency(vehicleId, DateTime.Now);
            if (result.Where(x => x.Urgency == ReminderUrgency.VeryUrgent || x.Urgency == ReminderUrgency.PastDue).Any())
            {
                return Json(true);
            }
            return Json(false);
        }
        [HttpGet]
        public IActionResult GetReminderRecordsByVehicleId(int vehicleId)
        {
            var result = GetRemindersAndUrgency(vehicleId, DateTime.Now);
            result = result.OrderByDescending(x => x.Urgency).ToList();
            return PartialView("_ReminderRecords", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveReminderRecordToVehicleId(ReminderRecordInput reminderRecord)
        {
            var result = _reminderRecordDataAccess.SaveReminderRecordToVehicle(reminderRecord.ToReminderRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpPost]
        public IActionResult GetAddReminderRecordPartialView(ReminderRecordInput? reminderModel)
        {
            if (reminderModel is not null)
            {
                return PartialView("_ReminderRecordModal", reminderModel);
            }
            else
            {
                return PartialView("_ReminderRecordModal", new ReminderRecordInput());
            }
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetReminderRecordForEditById(int reminderRecordId)
        {
            var result = _reminderRecordDataAccess.GetReminderRecordById(reminderRecordId);
            //convert to Input object.
            var convertedResult = new ReminderRecordInput
            {
                Id = result.Id,
                Date = result.Date.ToShortDateString(),
                Description = result.Description,
                Notes = result.Notes,
                VehicleId = result.VehicleId,
                Mileage = result.Mileage,
                Metric = result.Metric
            };
            return PartialView("_ReminderRecordModal", convertedResult);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteReminderRecordById(int reminderRecordId)
        {
            var result = _reminderRecordDataAccess.DeleteReminderRecordById(reminderRecordId);
            return Json(result);
        }
        #endregion
        #region "Upgrade Records"
        [HttpGet]
        public IActionResult GetUpgradeRecordsByVehicleId(int vehicleId)
        {
            var result = _upgradeRecordDataAccess.GetUpgradeRecordsByVehicleId(vehicleId);
            if (_useDescending)
            {
                result = result.OrderByDescending(x => x.Date).ThenByDescending(x => x.Mileage).ToList();
            }
            else
            {
                result = result.OrderBy(x => x.Date).ThenBy(x => x.Mileage).ToList();
            }
            return PartialView("_UpgradeRecords", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveUpgradeRecordToVehicleId(UpgradeRecordInput upgradeRecord)
        {
            //move files from temp.
            upgradeRecord.Files = upgradeRecord.Files.Select(x => { return new UploadedFiles { Name = x.Name, Location = _fileHelper.MoveFileFromTemp(x.Location, "documents/") }; }).ToList();
            var result = _upgradeRecordDataAccess.SaveUpgradeRecordToVehicle(upgradeRecord.ToUpgradeRecord());
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddUpgradeRecordPartialView()
        {
            return PartialView("_UpgradeRecordModal", new UpgradeRecordInput());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetUpgradeRecordForEditById(int upgradeRecordId)
        {
            var result = _upgradeRecordDataAccess.GetUpgradeRecordById(upgradeRecordId);
            //convert to Input object.
            var convertedResult = new UpgradeRecordInput
            {
                Id = result.Id,
                Cost = result.Cost,
                Date = result.Date.ToShortDateString(),
                Description = result.Description,
                Mileage = result.Mileage,
                Notes = result.Notes,
                VehicleId = result.VehicleId,
                Files = result.Files
            };
            return PartialView("_UpgradeRecordModal", convertedResult);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteUpgradeRecordById(int upgradeRecordId)
        {
            var result = _upgradeRecordDataAccess.DeleteUpgradeRecordById(upgradeRecordId);
            return Json(result);
        }
        #endregion
        #region "Notes"
        [HttpGet]
        public IActionResult GetNotesByVehicleId(int vehicleId)
        {
            var result = _noteDataAccess.GetNotesByVehicleId(vehicleId);
            return PartialView("_Notes", result);
        }
        [Authorize(Roles = $"{nameof(UserModel.CanAdd)},{nameof(UserModel.CanEdit)}")]
        [HttpPost]
        public IActionResult SaveNoteToVehicleId(Note note)
        {
            var result = _noteDataAccess.SaveNoteToVehicle(note);
            return Json(result);
        }
        [Authorize(Roles = nameof(UserModel.CanAdd))]
        [HttpGet]
        public IActionResult GetAddNotePartialView()
        {
            return PartialView("_NoteModal", new Note());
        }
        [Authorize(Roles = nameof(UserModel.CanEdit))]
        [HttpGet]
        public IActionResult GetNoteForEditById(int noteId)
        {
            var result = _noteDataAccess.GetNoteById(noteId);
            return PartialView("_NoteModal", result);
        }
        [Authorize(Roles = nameof(UserModel.CanDelete))]
        [HttpPost]
        public IActionResult DeleteNoteById(int noteId)
        {
            var result = _noteDataAccess.DeleteNoteById(noteId);
            return Json(result);
        }
        #endregion
    }
}
