using CarCareTracker.External.Interfaces;
using CarCareTracker.Models;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using CarCareTracker.Helper;
using Microsoft.AspNetCore.Authorization;

namespace CarCareTracker.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IVehicleDataAccess _dataAccess;
        private readonly IFileHelper _fileHelper;
        private readonly IConfiguration _config;

        public HomeController(ILogger<HomeController> logger, IVehicleDataAccess dataAccess, IFileHelper fileHelper, IConfiguration configuration)
        {
            _logger = logger;
            _dataAccess = dataAccess;
            _fileHelper = fileHelper;
            _config = configuration;
        }

        public IActionResult Index(string tab = "garage")
        {
            return View(model: tab);
        }
        public IActionResult Garage()
        {
            var vehiclesStored = _dataAccess.GetVehicles();
            return PartialView("_GarageDisplay", vehiclesStored);
        }
        [Authorize(Roles = nameof(Models.User.IsRootUser))]
        public IActionResult Settings()
        {
            var userConfig = new UserConfig
            {
                EnableCsvImports = bool.Parse(_config[nameof(UserConfig.EnableCsvImports)]),
                UseDarkMode = bool.Parse(_config[nameof(UserConfig.UseDarkMode)]),
                UseMPG = bool.Parse(_config[nameof(UserConfig.UseMPG)]),
                UseDescending = bool.Parse(_config[nameof(UserConfig.UseDescending)]),
                EnableAuth = bool.Parse(_config[nameof(UserConfig.EnableAuth)]),
                HideZero = bool.Parse(_config[nameof(UserConfig.HideZero)]),
                UseUKMPG = bool.Parse(_config[nameof(UserConfig.UseUKMPG)])
            };
            return PartialView("_Settings", userConfig);
        }
        [Authorize(Roles = nameof(Models.User.IsRootUser))]
        [HttpPost]
        public IActionResult WriteToSettings(UserConfig userConfig)
        {
            try
            {
                if (!System.IO.File.Exists(StaticHelper.UserConfigPath))
                {
                    //if file doesn't exist it might be because it's running on a mounted volume in docker.
                    System.IO.File.WriteAllText(StaticHelper.UserConfigPath, System.Text.Json.JsonSerializer.Serialize(new UserConfig()));
                }
                var configFileContents = System.IO.File.ReadAllText(StaticHelper.UserConfigPath);
                var existingUserConfig = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(configFileContents);
                if (existingUserConfig is not null)
                {
                    //copy over settings that are off limits on the settings page.
                    userConfig.EnableAuth = existingUserConfig.EnableAuth;
                    userConfig.UserNameHash = existingUserConfig.UserNameHash;
                    userConfig.UserPasswordHash = existingUserConfig.UserPasswordHash;
                }
                else
                {
                    userConfig.EnableAuth = false;
                    userConfig.UserNameHash = string.Empty;
                    userConfig.UserPasswordHash = string.Empty;
                }
                System.IO.File.WriteAllText(StaticHelper.UserConfigPath, System.Text.Json.JsonSerializer.Serialize(userConfig));
                return Json(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on saving config file.");
            }
            return Json(false);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
