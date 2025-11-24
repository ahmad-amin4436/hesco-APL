using Dapper;
using HESCO.Models;
using HESCO.Models.Projects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using System.Data;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Runtime.Intrinsics.X86;

namespace HESCO.Controllers
{
    public class MeterController : Controller
    {
        private readonly IConfiguration _configuration;
        private const string CountQuery = " m.meter_msn";
        private const string LimitQuery = " LIMIT @start,@pageSize ";
        private const string SelectQuery = @"
                        m.id AS MeterId, 
                        m.meter_msn AS MSN, 
                        m.description AS Description, 
                        m.meter_type AS MeterType, 
                        m.comments AS Comments, 
                        m.status, 
                        m.map_flag AS MapFlag,
                        m.created_at AS CreatedAt, 
                        m.created_by AS CreatedBy,
                        u1.username AS CreatedByUsername,
                        m_m.meter_mode AS MeterMode,
                        p.name AS ProjectName";
        public MeterController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<IActionResult> GetMeterFilteredSuggestions(string searchTerm, string filterType)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 4)
                return Json(new { suggestions = new List<string>() });

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "";
                switch (filterType)
                {
                    case "msn":
                        query = "SELECT DISTINCT meter_msn from meters WHERE meter_msn LIKE @SearchTerm LIMIT 10";
                        break;

                    case "metertype":
                        query = "SELECT DISTINCT meter_type FROM meters WHERE meter_type LIKE @SearchTerm LIMIT 10";
                        break;

                }

                var suggestions = await db.QueryAsync<string>(query, new { SearchTerm = $"%{searchTerm}%" });
                return Json(new { suggestions });
            }
        }
        [AuthorizeUserEx]
        public async Task<IActionResult> ViewMeterList(string? status, string ProjectNameFilter, string? mapflag, string? msnFilter, string? createdByFilter, DateTime? createdAtFilter, int? page, int? pageSize)
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                dbMaster.Open();
                // Get the currently connected database name
                string currentDbQuery = "SELECT DATABASE();";
                string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);
                string masterDbName = await dbMaster.ExecuteScalarAsync<string>(currentDbQuery);

                if (string.IsNullOrEmpty(currentDbName))
                {
                    return BadRequest("Unable to retrieve the database name.");
                }
                // Get db_id from project_databases based on the current database name
                string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                if (!dbId.HasValue)
                {
                    return BadRequest("Database ID not found in project_databases table.");
                }
                pageSize = pageSize ?? 25; // Default to 25 if not provided
                page = page ?? 1;
                var start = (page - 1) * pageSize;
                // Removing leading commas from filter values
                msnFilter = msnFilter?.TrimStart(',');
                createdByFilter = createdByFilter?.TrimStart(',');
                status = status?.TrimStart(',');
                mapflag = mapflag?.TrimStart(',');
                ProjectNameFilter = ProjectNameFilter?.TrimStart(',');

                // Constructing SQL conditions
                string msnCondition = !string.IsNullOrEmpty(msnFilter) ? $"m.meter_msn IN ({msnFilter})" : "1=1";
                string CreatedByCondition = !string.IsNullOrEmpty(createdByFilter) ? $"m.created_by IN ({createdByFilter})" : "1=1";
                string statusCondition = !string.IsNullOrEmpty(status) ? $"m.status IN ({status})" : "1=1";
                string mapflagCondition = !string.IsNullOrEmpty(mapflag) ? $"m.map_flag IN ({mapflag})" : "1=1";
                string createdDateCondition = createdAtFilter.HasValue ? $"m.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND m.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                string projectNameCondition = !string.IsNullOrEmpty(ProjectNameFilter)
               ? $"m.project_id IN ({string.Join(",", ProjectNameFilter.Split(',').Select(x => $"'{x.Trim()}'"))})"
               : "1=1";
                // Fetch data for dropdowns
                string msnQuery = "SELECT DISTINCT meter_msn AS value, meter_msn AS text FROM meters";
                var msnData = await dbDefault.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");

                // Get projects based on the retrieved db_id
                string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
                var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");

                string CreatedByQuery = "SELECT DISTINCT m.created_by AS Value, u1.username AS Text FROM meters m LEFT JOIN user u1 ON m.created_by = u1.Id";
                var CreatedByData = await dbDefault.QueryAsync<DTODropdown>(CreatedByQuery);
                ViewBag.SelectCreatedBy = new SelectList(CreatedByData.Distinct(), "Value", "Text");
                string mapflagQuery = @"
                 SELECT DISTINCT 
                     map_flag AS Value, 
                     CASE 
                         WHEN map_flag = 0 THEN 'No'                          
                         ELSE 'Yes' 
                     END AS Text 
                 FROM 
                     meters 
                 ORDER BY 
                     map_flag ASC";
                var mapflagData = await dbDefault.QueryAsync<DTODropdown>(mapflagQuery);
                ViewBag.SelectMapFlag = new SelectList(mapflagData.Distinct(), "Value", "Text");

                string statusQuery = @"
                SELECT DISTINCT 
                    status AS Value, 
                    CASE 
                        WHEN status = 0 THEN 'In-Active' 
                        ELSE 'Active' 
                    END AS Text 
                FROM 
                    meters
                ORDER BY 
                    status ASC";
                var statusData = await dbDefault.QueryAsync<DTODropdown>(statusQuery);
                ViewBag.SelectStatus = new SelectList(statusData.Distinct(), "Value", "Text");

                var msnA = string.IsNullOrEmpty(msnFilter) ? "1" : msnFilter;
                var statusA = string.IsNullOrEmpty(status) ? "1" : status;
                var mapflagA = string.IsNullOrEmpty(mapflag) ? "1" : mapflag;
                var createdByA = string.IsNullOrEmpty(createdByFilter) ? "1" : createdByFilter;
                var createdDateA = createdAtFilter.HasValue ? $"m.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND m.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var ProjectNameA = string.IsNullOrEmpty(ProjectNameFilter) ? "1" : ProjectNameFilter;

                // Query for meters
                string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                 FROM meters m 
                LEFT JOIN `{currentDbName}`.`user` u1 ON m.created_by = u1.Id
                LEFT JOIN `{masterDbName}`.`projects` p ON m.project_id = p.Id
                LEFT JOIN `{currentDbName}`.`meter_mode` m_m ON m_m.id = p.metermode_id
                WHERE
                    {msnCondition} AND
                    {CreatedByCondition} AND
                    {statusCondition} AND
                    {projectNameCondition} AND
                    {createdDateCondition} AND
                    {mapflagCondition}
                ORDER BY m.created_at DESC";
                var countQueryFinal = string.Format($"select count(*) from ({GeneralQuery})a", CountQuery, msnA, createdByA, statusA, createdDateA, mapflagA,ProjectNameA);
                var totalRecords = await dbDefault.ExecuteScalarAsync<int>(countQueryFinal, new { msnFilter, status, createdByFilter, createdAtFilter, ProjectNameFilter, mapflag,start, pageSize });
                var msnQueryFinal = string.Format(GeneralQuery + LimitQuery, SelectQuery, msnA, createdByA, statusA, createdDateA, mapflagA,ProjectNameA);
                var meters = await dbDefault.QueryAsync<MeterDataViewModel>(msnQueryFinal, new { msnFilter, status, createdByFilter, createdAtFilter, ProjectNameFilter, mapflag,start, pageSize });
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                // Preserve filter values
                ViewData["CreatedBy"] = createdByFilter;
                ViewData["Status"] = status;
                ViewData["MSN"] = msnFilter;
                ViewData["Project"] = ProjectNameFilter;
                ViewData["page"] = page.Value;
                ViewData["pageSize"] = pageSize;
                ViewData["MapFlag"] = mapflag;
                ViewData["totalRecords"] = totalRecords;
                ViewData["totalPages"] = totalPages;
                ViewBag.CreatedDate = createdAtFilter?.ToString("yyyy-MM-dd");
                ViewBag.PageSize = pageSize;
                return View(meters);
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetMeterModeByProject(int projectId)
        {
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbMaster.Open();
                string query = @"SELECT metermode_id FROM projects WHERE id = @ProjectId";
                int? meterMode = await dbMaster.ExecuteScalarAsync<int?>(query, new { ProjectId = projectId });

                if (!meterMode.HasValue)
                {
                    return Json(new { success = false });
                }

                return Json(new { success = true, meterMode = meterMode.Value });
            }
        }

        public async Task<IActionResult> ExportToExcel(string? status,string ProjectNameFilter, string? mapflag, string? msnFilter, string? createdByFilter, DateTime? createdAtFilter, int? page, int? pageSize)
        {// Constructing SQL conditions
            string msnCondition = !string.IsNullOrEmpty(msnFilter) ? $"m.meter_msn IN ({msnFilter})" : "1=1";
            string CreatedByCondition = !string.IsNullOrEmpty(createdByFilter) ? $"m.created_by IN ({createdByFilter})" : "1=1";
            string statusCondition = !string.IsNullOrEmpty(status) ? $"m.status IN ({status})" : "1=1";
            string mapflagCondition = !string.IsNullOrEmpty(mapflag) ? $"m.map_flag IN ({mapflag})" : "1=1";
            string createdDateCondition = createdAtFilter.HasValue ? $"m.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND m.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            string projectNameCondition = !string.IsNullOrEmpty(ProjectNameFilter)
            ? $"m.project_id IN ({string.Join(",", ProjectNameFilter.Split(',').Select(x => $"'{x.Trim()}'"))})"
            : "1=1";
            // Query for meter
            string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                 FROM meters m 
                LEFT JOIN user u1 ON m.created_by = u1.Id
                LEFT JOIN projects p ON m.project_id = p.Id
                WHERE
                    {msnCondition} AND
                    {CreatedByCondition} AND
                    {statusCondition} AND
                    {projectNameCondition} AND     
                    {createdDateCondition} AND
                    {mapflagCondition}
                ORDER BY m.created_at DESC";
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var msnA = string.IsNullOrEmpty(msnFilter) ? "1" : msnFilter;
                var statusA = string.IsNullOrEmpty(status) ? "1" : status;
                var mapflagA = string.IsNullOrEmpty(mapflag) ? "1" : mapflag;
                var createdByA = string.IsNullOrEmpty(createdByFilter) ? "1" : createdByFilter;
                var createdDateA = createdAtFilter.HasValue ? $"m.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND m.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var ProjectNameA = string.IsNullOrEmpty(ProjectNameFilter) ? "1" : ProjectNameFilter;


                var msnQueryFinal = string.Format(GeneralQuery , msnA, createdByA, statusA, createdDateA, mapflagA,ProjectNameA);
                var meters = await db.QueryAsync<MeterDataViewModel>(msnQueryFinal, new { msnFilter, status, createdByFilter, createdAtFilter, mapflag, ProjectNameFilter, pageSize });
          
                var fileDownloadName = "MeterList.xlsx";

                // Define a custom folder to save the file (you can choose a folder on the D drive or any location accessible to your application)
                var downloadFolderPath = Path.Combine("D:", "MyAppDownloads");  // Or specify your own folder path

                // Ensure the folder exists
                if (!Directory.Exists(downloadFolderPath))
                {
                    Directory.CreateDirectory(downloadFolderPath);
                }
                using (var package = CreateExcelPackage(meters))
                {
                    var filePath = Path.Combine(downloadFolderPath, fileDownloadName);
                    package.SaveAs(new FileInfo(filePath)); // Save file to the custom location

                    // Now return the file to the browser for inline display
                    var fileBytes = System.IO.File.ReadAllBytes(filePath);
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileDownloadName);
                }
                //using (var package = CreateExcelPackage(ComplaintData))
                //{
                //    var filePath = Path.Combine(downloadFolderPath, fileDownloadName);
                //    package.SaveAs(new FileInfo(filePath)); // Save file to the custom location
                //}


                //// Return the file to the user as a downloadable file
                //return File($"~/{fileDownloadName}", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileDownloadName);
                ////var fileDownloadName = "ComplaintList.xlsx";

                //using (var package = CreateExcelPackage(ComplaintData))
                //{
                //    package.SaveAs(new FileInfo(Path.Combine(_webHostEnvironment.WebRootPath, fileDownloadName)));
                //}
                //return File($"~/{fileDownloadName}", XlsxContentType, fileDownloadName);
            }
        }


        private ExcelPackage CreateExcelPackage(IEnumerable<MeterDataViewModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            var package = new ExcelPackage();
            package.Workbook.Properties.Title = "METER LIST";
            package.Workbook.Properties.Author = "";
            package.Workbook.Properties.Subject = "METER LIST";

            var worksheet = package.Workbook.Worksheets.Add("METER LIST");

            int row = 1;
            worksheet.Cells[row, 1].Value = $"METER LIST                                                                 DATE: {DateTime.Now:dd-MM-yyyy}";
            worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells[row, 1, row, 7].Merge = true;

            row++;

            // Header row

            worksheet.Cells[row, 1].Value = "MSN";
            worksheet.Cells[row, 2].Value = "Description";
            worksheet.Cells[row, 3].Value = "Meter Type";
            worksheet.Cells[row, 4].Value = "Comments";
            worksheet.Cells[row, 5].Value = "STATUS";
            worksheet.Cells[row, 6].Value = "Map Flag";
            worksheet.Cells[row, 7].Value = "Project Name";
            worksheet.Cells[row, 1, row, 7].Style.Font.Bold = true;
            row++;



            foreach (var item in data)
            {
                worksheet.Cells[row, 1].Value = item.MSN;
                worksheet.Cells[row, 2].Value = item.Description;
                worksheet.Cells[row, 3].Value = item.MeterType;
                worksheet.Cells[row, 4].Value = item.Comments;
                worksheet.Cells[row, 5].Value = item.MeterStatusDisplay;
                worksheet.Cells[row, 6].Value = item.MapFlagDisplay;
                worksheet.Cells[row, 7].Value = item.ProjectName;

                // Apply borders around each row
                var borderCells = worksheet.Cells[row, 1, row, 7];
                borderCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                row++;
            }

            // Apply borders around the entire table
            var fullTable = worksheet.Cells[1, 1, row - 1, 7];
            fullTable.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

            // AutoFitColumns
            worksheet.Cells[1, 1, row, 7].AutoFitColumns();

            return package;
        }
        //private ExcelPackage CreateExcelPackage(IEnumerable<MeterDataViewModel> data)
        //{
        //    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //    var package = new ExcelPackage();
        //    package.Workbook.Properties.Title = "METER LIST";
        //    package.Workbook.Properties.Author = "";
        //    package.Workbook.Properties.Subject = "METER LIST";

        //    var worksheet = package.Workbook.Worksheets.Add("METER LIST");

        //    int row = 1;
        //    worksheet.Cells[row, 1].Value = $"METER LIST                                                                 DATE: {DateTime.Now:dd-MM-yyyy}";
        //    worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        //    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
        //    worksheet.Cells[row, 1].Style.Font.Bold = true;
        //    worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        //    worksheet.Cells[row, 1, row, 6].Merge = true;

        //    row++;

        //    // Header row

        //    worksheet.Cells[row, 1].Value = "MSN";
        //    worksheet.Cells[row, 2].Value = "Description";
        //    worksheet.Cells[row, 3].Value = "Meter Type";
        //    worksheet.Cells[row, 4].Value = "Comments"; 
        //    worksheet.Cells[row, 5].Value = "STATUS";
        //    worksheet.Cells[row, 6].Value = "Map Flag";
        //    worksheet.Cells[row, 1, row, 6].Style.Font.Bold = true;
        //    row++;



        //    foreach (var item in data)
        //    {
        //        worksheet.Cells[row, 1].Value = item.MSN;
        //        worksheet.Cells[row, 2].Value = item.Description;
        //        worksheet.Cells[row, 3].Value = item.MeterType;
        //        worksheet.Cells[row, 4].Value = item.Comments;
        //        worksheet.Cells[row, 5].Value = item.MeterStatusDisplay;
        //        worksheet.Cells[row, 6].Value = item.MapFlagDisplay;

        //        // Apply borders around each row
        //        var borderCells = worksheet.Cells[row, 1, row, 6];
        //        borderCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //        borderCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //        borderCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //        borderCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

        //        row++;
        //    }

        //    // Apply borders around the entire table
        //    var fullTable = worksheet.Cells[1, 1, row - 1, 6];
        //    fullTable.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //    fullTable.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //    fullTable.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
        //    fullTable.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

        //    // AutoFitColumns
        //    worksheet.Cells[1, 1, row, 6].AutoFitColumns();

        //    return package;
        //}
        //public IActionResult ViewMeterList(string search)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        var sql = @"
        //        SELECT 
        //            m.id AS MeterId, 
        //            m.meter_msn AS MSN, 
        //            m.description AS Description, 
        //            m.meter_type AS MeterType, 
        //            m.comments AS Comments, 
        //            m.status, 
        //            m.created_at AS CreatedAt, 
        //            m.created_by AS CreatedBy,
        //            u1.username AS CreatedByUsername 
        //        FROM meters m 
        //        LEFT JOIN user u1 ON m.created_by = u1.Id";

        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            // Use a parameterized query to prevent SQL injection
        //            sql += " WHERE m.meter_msn LIKE @search";
        //            search = "%" + search.Trim(); 
        //        }

        //        var meters = db.Query<MeterDataViewModel>(sql, new { search }).ToList();
        //        return View(meters);
        //    }
        //}

        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateMeter()
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                dbMaster.Open();
                // Get the currently connected database name
                string currentDbQuery = "SELECT DATABASE();";
                string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

                if (string.IsNullOrEmpty(currentDbName))
                {
                    return BadRequest("Unable to retrieve the database name.");
                }

                // Get db_id from project_databases based on the current database name
                string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                if (!dbId.HasValue)
                {
                    return BadRequest("Database ID not found in project_databases table.");
                }

                // Get projects based on the retrieved db_id
                string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
                var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> CreateMeter(MeterData meterData)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                ModelState.AddModelError("", "User is not authenticated.");
                return View(meterData); // Return with error message if user is not authenticated
            }
            meterData.CreatedBy = userId.Value;
            meterData.CreatedAt = DateTime.Now;
            string meter_type = "";
            if (!string.IsNullOrWhiteSpace(meterData.MeterTypeText))
            {
                meter_type = meterData.MeterTypeText;
            }
            else
            {
                meter_type = meterData.MeterType;
            }
            if (ModelState.IsValid)
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    // Check for existing MSN
                    var existingMsn = db.QuerySingleOrDefault<int?>(
                        "SELECT COUNT(*) FROM meters WHERE meter_msn = @Msn",
                        new { Msn = meterData.MSN });

                    if (existingMsn.HasValue && existingMsn.Value > 0)
                    {
                        ModelState.AddModelError("MSN", "The entered MSN already exists.");
                        return View(meterData);
                    }

                    var sql = "INSERT INTO meters (meter_msn, description, meter_type, comments, created_by, created_at,project_id,meter_mode) " +
                              "VALUES (@MSN, @Description, @MeterType, @Comments, @CreatedBy, @CreatedAt,@Project,@meter_mode)";

                    db.Execute(sql, new
                    {
                        MSN = meterData.MSN,
                        Description = meterData.Description,
                        MeterType = meter_type,
                        Comments = meterData.Comments,
                        CreatedBy = meterData.CreatedBy,
                        CreatedAt = meterData.CreatedAt,
                        Project = meterData.Project,
                        meter_mode = meterData.MeterMode
                    });

                    return RedirectToAction("ViewMeterList");
                }
            }

            return View(meterData);
        }


        [HttpGet]
        public IActionResult GetMeterType(string prefix)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var meterType = db.QueryFirstOrDefault<string>("SELECT type FROM meter_model WHERE prefix = @Prefix", new { Prefix = prefix });
                return Json(meterType);
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public IActionResult EditMeterDetails(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var sql = "SELECT id AS MeterId, meter_msn AS MSN, description AS Description,meter_type AS MeterType, comments AS Comments FROM meters WHERE id = @Id";

                var meter = db.QuerySingleOrDefault<MeterData>(sql, new { Id = id });

                if (meter == null)
                {
                    return NotFound();
                }

                return View(meter); // Return the view with the meter data
            }
        }
        [HttpPost]
        public IActionResult EditMeterDetails(MeterData meterData)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            meterData.UpdatedBy = userId.Value;
            if (ModelState.IsValid)
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var sql = "UPDATE meters SET meter_msn = @MSN, description = @Description, " +
                              "meter_type = @MeterType, comments = @Comments,updated_at = @UpdatedAt,updated_by = @UpdatedBy WHERE id = @MeterId";

                    db.Execute(sql, new
                    {
                        MSN = meterData.MSN,
                        Description = meterData.Description,
                        MeterType = meterData.MeterType,
                        Comments = meterData.Comments,
                        MeterId = meterData.MeterId,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy=meterData.UpdatedBy

                    });

                    return RedirectToAction("ViewMeterList");
                }
            }

            return View(meterData); // Return the view with validation errors
        }
        [AuthorizeUserEx]
        public async Task<IActionResult> ViewMeterDetails(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                db.Open();
                dbMaster.Open();
                string currentDbQuery = "SELECT DATABASE();";
                string masterDbName = await dbMaster.ExecuteScalarAsync<string>(currentDbQuery);
                var sql = $@"
                SELECT 
                    m.id AS MeterId, 
                    m.meter_msn AS MSN, 
                    m.description AS Description, 
                    m.meter_type AS MeterType, 
                    m.comments AS Comments, 
                    m.status AS Status, 
                    m.map_flag AS MapFlag,
                    m.created_at AS CreatedAt, 
                    m.created_by AS CreatedBy,
                    m.updated_by AS UpdatedBy,
                    m.updated_at AS UpdatedAt,
                    u1.username AS CreatedByUsername,
                    u2.username AS UpdatedByUsername,
                    p.name AS ProjectName
                FROM meters m 
                    LEFT JOIN user u1 ON m.created_by = u1.Id
                    LEFT JOIN user u2 ON m.updated_by = u2.Id
                    LEFT JOIN `{masterDbName}`.`projects` p ON m.project_id = p.Id
                WHERE
                    m.id = @Id";

                var meter = await db.QuerySingleOrDefaultAsync<MeterDataViewModel>(sql, new { Id = id });

                if (meter == null)
                {
                    return NotFound();
                }

                return View(meter);
            }
        }
        [AuthorizeUserEx]
        public IActionResult ConfirmDelete(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var sql = "SELECT id AS MeterId, meter_msn AS MSN FROM meters WHERE id = @Id";
                var meter = db.QuerySingleOrDefault<MeterDataViewModel>(sql, new { Id = id });

                if (meter == null)
                {
                    return NotFound();
                }

                return View(meter); 
            }
        }
        [AuthorizeUserEx]
        [HttpPost]
        public IActionResult DeleteMeter(int id)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Execute the DELETE command
                var sql = "DELETE FROM meters WHERE id = @Id";
                var rowsAffected = db.Execute(sql, new { Id = id });

                // Check if the deletion was successful
                if (rowsAffected > 0)
                {
                    return RedirectToAction("ViewMeterList"); 
                }
                else
                {
                    return NotFound();  // Return Not Found if the meter was not found
                }
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> UploadMeterExcel()
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                dbMaster.Open();
                // Get the currently connected database name
                string currentDbQuery = "SELECT DATABASE();";
                string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

                if (string.IsNullOrEmpty(currentDbName))
                {
                    return BadRequest("Unable to retrieve the database name.");
                }

                // Get db_id from project_databases based on the current database name
                string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                if (!dbId.HasValue)
                {
                    return BadRequest("Database ID not found in project_databases table.");
                }

                // Get projects based on the retrieved db_id
                string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
                var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
            }
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetMeterModel(int projectId)
        {
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                dbMaster.Open();
                // Load projects
                string projectQuery = @"SELECT 
                                    id AS Value, 
                                    name AS Text, 
                                    metermode_id AS MeterModeId 
                                FROM projects WHERE id = @ProjectID";

                var projects = (await dbMaster.QueryAsync<ProjectData>(projectQuery, new { ProjectID = projectId }))
                               .ToList();

                // If any project has metermode_id = 1, load meter models
                if (projects.Any(p => p.MeterModeId == 1))
                {
                    string meterQuery = "SELECT id AS Value, type AS Text FROM meter_model;";
                    var meterModels = (await dbDefault.QueryAsync<DTODropdown>(meterQuery)).ToList();

                    var ddl_MeterModel = new SelectList(meterModels, "Value", "Text");
                    return Json(new
                    {
                        success = true,
                        models = meterModels.Select(m => new
                        {
                            value = m.Value,
                            text = m.Text
                        })
                    });
                }
            }
            return Json(new
            {
                success = false              
            });

        }
        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "MeterTemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "MeterTemplate.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpPost]
        public async Task<IActionResult> UploadMeterExcel(MeterData meterData, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("UploadMeterExcel", new { error = "No file selected" });
            }
            var meters = new List<MeterData>();
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                string unauthorizedMessage = "User is not authenticated. Redirecting to login page...";
                string loginUrl = Url.Action("LoginUser", "Account"); // Replace with your login URL

                // Return an HTML response that includes the unauthorized message and JavaScript redirection
                string htmlContent = $@"
                <html>
                    <body>
                        <h3>{unauthorizedMessage}</h3>
                        <script>
                            setTimeout(function() {{
                                window.location.href = '{loginUrl}';
                            }}, 1000); // Redirects after 1 seconds
                        </script>
                    </body>
                </html>";

                return Content(htmlContent, "text/html");
            }
            var UserId = userId.Value;
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets.First();
                    var rowCount = worksheet.Dimension.Rows;
                    var existingMsnSet = new HashSet<string>();
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        for (int row = 2; row <= rowCount; row++) // Skip header row
                        {
                            var msn = worksheet.Cells[row, 1].Text;
                            var description = worksheet.Cells[row, 2].Text;

                            if (!string.IsNullOrWhiteSpace(msn) && !string.IsNullOrWhiteSpace(description))
                            {
                                if (existingMsnSet.Contains(msn))
                                    continue;

                                existingMsnSet.Add(msn);

                                // Check if MSN already exists in the DB
                                var exists = await db.QuerySingleOrDefaultAsync<int>(
                                    "SELECT COUNT(*) FROM meters WHERE meter_msn = @Msn", new { Msn = msn });

                                if (exists > 0)
                                    continue;

                                var insertQuery = @"INSERT INTO meters 
                            (meter_msn, description, created_at, created_by, meter_type, project_id,meter_mode) 
                            VALUES 
                            (@Msn, @Description, @CreatedAt, @CreatedBy, @MeterType, @Project,@meter_mode)";
                                if (!string.IsNullOrWhiteSpace(meterData.MeterType))
                                {
                                    

                                    await db.ExecuteAsync(insertQuery, new
                                    {
                                        Msn = msn,
                                        Description = description,
                                        CreatedAt = DateTime.Now,
                                        CreatedBy = UserId,
                                        MeterType = meterData.MeterType,
                                        Project = meterData.Project,
                                        meter_mode = meterData.MeterMode
                                    });
                                }
                                else
                                {
                                    var meterType = await GetExcelMeterType(msn.Substring(0, 4));

                                    // Insert record immediately
                                 
                                    await db.ExecuteAsync(insertQuery, new
                                    {
                                        Msn = msn,
                                        Description = description,
                                        CreatedAt = DateTime.Now,
                                        CreatedBy = UserId,
                                        MeterType = meterType,
                                        Project = meterData.Project,
                                        meter_mode = meterData.MeterMode
                                    });
                                }   
                            }
                        }
                    }
                }
            }

            return RedirectToAction("ViewMeterList");
        }

        private async Task<string> GetExcelMeterType(string prefix)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var meterType = await db.QueryFirstOrDefaultAsync<string>(
                    "SELECT type FROM meter_model WHERE prefix = @Prefix", new { Prefix = prefix });
                return meterType;
            }
        }
    }
}
