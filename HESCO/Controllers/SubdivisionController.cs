using System.Data;
using Dapper;
using HESCO.Models.SubDivision;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OfficeOpenXml;

namespace HESCO.Controllers
{
    public class SubdivisionController : Controller
    {
        private readonly IConfiguration _configuration;
        public SubdivisionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet]
        public IActionResult ImportSubdivision()
        {
            return View();
        }
        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "SubdivisionTemp.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "SubdivisionTemp.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpPost]
        public async Task<IActionResult> ImportSubdivision(MeterSubdivision subdivisionViewModel, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("ImportSubdivision", new { error = "No file selected" });
            }

            var SubdivisionData = new List<MeterSubdivision>();
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
                        var SubDivisionCode = worksheet.Cells[row, 1].Text.Trim();
                        var SubDivisionName = worksheet.Cells[row, 2].Text.Trim(); // First column
                       
                        //var CircleName = worksheet.Cells[row, 3].Text.Trim();
                        //var CircleCode = worksheet.Cells[row, 4].Text.Trim();
                        //var DivisionName = worksheet.Cells[row, 5].Text.Trim();
                        //var DivisionCode = worksheet.Cells[row, 6].Text.Trim();
                        //var DISCOName = worksheet.Cells[row, 7].Text.Trim();

                        var missingFields = new List<string>(); // Store missing field names
                        if (string.IsNullOrWhiteSpace(SubDivisionCode)) missingFields.Add("SubDivision Code");
                        if (string.IsNullOrWhiteSpace(SubDivisionName)) missingFields.Add("SubDivision Name");
                        
                        //if (string.IsNullOrWhiteSpace(CircleName)) missingFields.Add("Circle Name");
                        //if (string.IsNullOrWhiteSpace(CircleCode)) missingFields.Add("Circle Code");
                        //if (string.IsNullOrWhiteSpace(DivisionName)) missingFields.Add("Division Name");
                        //if (string.IsNullOrWhiteSpace(DivisionCode)) missingFields.Add("Division Code");
                        //if (string.IsNullOrWhiteSpace(DISCOName)) missingFields.Add("DISCO Name");

                        // Skip row if any field is missing and store the details
                        if (missingFields.Any())
                        {
                            skippedRows.Add($"Row {row}: Missing fields - {string.Join(", ", missingFields)}");
                            continue;
                        }

                        // Check for duplicate within the Excel file
                        if (existingImsiSet.Contains(SubDivisionCode))
                        {
                            skippedRows.Add($"Row {row}: Duplicate SubDivision Code '{SubDivisionCode}' in the Excel file.");
                            continue;
                        }

                        existingImsiSet.Add(SubDivisionCode);

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

                        SubdivisionData.Add(new MeterSubdivision
                        {
                            SubDivisionCode = SubDivisionCode,
                            SubDivisionName = SubDivisionName,
                            
                            //DivisionName = DivisionName,
                            //DivisionCode = DivisionCode,
                            //CircleName = CircleName,
                            //CircleCode = CircleCode,
                            //DISCOName = DISCOName,
                            //Project = subdivisionViewModel.Project
                        });
                    }
                }
            }

            // Insert valid records into the database
            if (SubdivisionData.Any())
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var insertQuery = @"INSERT INTO meter_subdivision 
                               (subdiv_code, subdiv_name) 
                               VALUES (@SubDivisionCode, @SubDivisionName)";

                    await db.ExecuteAsync(insertQuery, SubdivisionData);
                }
            }
            // ✅ If there are skipped rows, show the message on the same view
            if (skippedRows.Any())
            {
                TempData["SkippedRows"] = string.Join("<br/>", skippedRows);
                return View(); // Stay on the same page
            }


            return RedirectToAction("");
        }
    }
}
