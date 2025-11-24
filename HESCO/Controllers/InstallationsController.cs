
using System.Data;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using HESCO.Models.Installations;
using MySql.Data.MySqlClient;
using Dapper;

namespace HESCO.Controllers
{
    public class InstallationsController : Controller
    {
        private readonly IConfiguration _configuration;
        public InstallationsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet]
        public IActionResult ImportExcelInstallation()
        {
            return View();
        }
        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "MeterInstallation.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "MeterInstallation.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpPost]
        public async Task<IActionResult> ImportExcelInstallation(MeterInstallation meterinstallation, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("ImportExcelInstallation", new { error = "No file selected" });
            }

            var meterinstallationdata = new List<MeterInstallation>();
            var skippedRows = new List<string>(); // Stores skipped row details
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.First();
                    var rowCount = worksheet.Dimension.Rows;
                    var existingImsiSet = new HashSet<string>();

                    for (int row = 2; row <= rowCount; row++) // Assuming first row is header
                    {
                        var ReferenceNo = worksheet.Cells[row, 1].Text.Trim();
                        var Msn = worksheet.Cells[row, 2].Text.Trim(); // First column
                        var Address = worksheet.Cells[row, 3].Text.Trim();
                        var Telco = worksheet.Cells[row, 4].Text.Trim();
                        var SimNo = worksheet.Cells[row, 5].Text.Trim();
                        var SimId = worksheet.Cells[row, 6].Text.Trim();
                        var SubDivisionCode = worksheet.Cells[row, 7].Text.Trim();
                        var SubDivisionName = worksheet.Cells[row, 8].Text.Trim();


                        var missingFields = new List<string>(); // Store missing field names
                        if (string.IsNullOrWhiteSpace(ReferenceNo)) missingFields.Add("ReferenceNo");
                        if (string.IsNullOrWhiteSpace(Msn)) missingFields.Add("Msn");
                        if (string.IsNullOrWhiteSpace(Address)) missingFields.Add("Address");
                        if (string.IsNullOrWhiteSpace(Telco)) missingFields.Add("Telco");
                        if (string.IsNullOrWhiteSpace(SimNo)) missingFields.Add("SimNo");
                        if (string.IsNullOrWhiteSpace(SimId)) missingFields.Add("SimId");
                        if (string.IsNullOrWhiteSpace(SubDivisionCode)) missingFields.Add("SubDivisionCode");
                        if (string.IsNullOrWhiteSpace(SubDivisionName)) missingFields.Add("SubDivisionName");

                        // Skip row if any field is missing and store the details
                        if (missingFields.Any())
                        {
                            skippedRows.Add($"Row {row}: Missing fields - {string.Join(", ", missingFields)}");
                            continue;
                        }

                        // Check for duplicate within the Excel file
                        if (existingImsiSet.Contains(ReferenceNo))
                        {
                            skippedRows.Add($"Row {row}: Duplicate SubDivision Code '{ReferenceNo}' in the Excel file.");
                            continue;
                        }

                        existingImsiSet.Add(ReferenceNo);

                        // Check if the subdivision already exists in the database
                        //using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        //{
                        //    var existingImsiCount = await db.QuerySingleOrDefaultAsync<int>(
                        //        "SELECT COUNT(*) FROM meter_subdivision WHERE subdiv_code = @SubDivisionCode",
                        //        new { SubDivisionCode });

                        //    if (existingImsiCount > 0)
                        //    {
                        //        skippedRows.Add($"Row {row}: SubDivision Code '{SubDivisionCode}' already exists in the database.");
                        //        continue; // Skip already existing Subdivision in the database
                        //    }
                        //}

                        meterinstallationdata.Add(new MeterInstallation
                        {
                            ReferenceNo = ReferenceNo,
                            Msn = Msn,
                            Address = Address,
                            Telco = Telco,
                            SimNo = SimNo,
                            SimId = SimId,
                            SubDivisionCode = SubDivisionCode,
                            SubDivisionName = SubDivisionName
                            //DISCOName = DISCOName,
                            //Project = subdivisionViewModel.Project
                        });
                    }
                }
            }

            // Insert valid records into the database
            if (meterinstallationdata.Any())
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var insertQuery = @"INSERT INTO meter_installation 
                               (reference_no, msn, address, telco, sim_no, sim_id, subdiv_code, subdiv_name) 
                               VALUES (@ReferenceNo, @Msn, @Address, @Telco, @SimNo, @SimId, @SubDivisionCode, @SubDivisionName)";

                    await db.ExecuteAsync(insertQuery, meterinstallationdata);
                }
            }
            // ✅ If there are skipped rows, show the message on the same view
            if (skippedRows.Any())
            {
                TempData["SkippedRows"] = string.Join("<br/>", skippedRows);
                return View(); // Stay on the same page
            }


            return RedirectToAction("ImportExcelInstallation");
        }
    }


}

