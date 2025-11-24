using Dapper;
using HESCO.Models.Projects;
using HESCO.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using System.Data;

namespace lescomm.Controllers
{
    public class ProjectController : Controller
    {
        private readonly IConfiguration _configuration;
        private const string CountQuery = " p.name";
        private const string LimitQuery = " LIMIT @start,@pageSize ";
        private const string SelectQuery = @"
                        p.id, 
                        p.name AS ProjectName,                        
                        p.created_at AS CreatedAt,
p.created_by AS CreatedByUsername";
        public ProjectController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<IEnumerable<MeterMode>> GetMeterModesAsync()
        {
            using (var conn = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id, meter_mode FROM meter_mode ORDER BY meter_mode;";
                return await conn.QueryAsync<MeterMode>(query);
            }
        }

        public async Task<IActionResult> GetFilteredSuggestions(string searchTerm, string filterType)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 4)
                return Json(new { suggestions = new List<string>() });

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                string query = "";
                switch (filterType)
                {
                    case "projectName":
                        query = "SELECT DISTINCT name from projects WHERE name LIKE @SearchTerm LIMIT 10";
                        break;
                   
                }

                var suggestions = await db.QueryAsync<string>(query, new { SearchTerm = $"%{searchTerm}%" });
                return Json(new { suggestions });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewProjectList(string? projectName, string? createdByFilter, DateTime? createdAtFilter, int? page, int? pageSize)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                pageSize = pageSize ?? 25; // Default to 25 if not provided
                page = page ?? 1;
                var start = (page - 1) * pageSize;
                // Removing leading commas from filter values
                projectName = projectName?.TrimStart(',');
                createdByFilter = createdByFilter?.TrimStart(',');

                // Constructing SQL conditions
                string projectNameCondition = !string.IsNullOrEmpty(projectName)
               ? $"p.name IN ({string.Join(",", projectName.Split(',').Select(x => $"'{x.Trim()}'"))})"
               : "1=1";
                string CreatedByCondition = !string.IsNullOrEmpty(createdByFilter) ? $"p.created_by IN ({createdByFilter})" : "1=1";
               
                string createdDateCondition = createdAtFilter.HasValue ? $"p.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND p.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";


                //string CreatedByQuery = "SELECT DISTINCT p.created_by AS Value, u1.username AS Text FROM projects p LEFT JOIN user u1 ON p.created_by = u1.Id";
                //var CreatedByData = await db.QueryAsync<DTODropdown>(CreatedByQuery);
                //ViewBag.SelectCreatedBy = new SelectList(CreatedByData.Distinct(), "Value", "Text");

                var projectNameA = string.IsNullOrEmpty(projectName) ? "1" : projectName;                
                var createdByA = string.IsNullOrEmpty(createdByFilter) ? "1" : createdByFilter;
                var createdDateA = createdAtFilter.HasValue ? $"p.created_at >= '{createdAtFilter.Value:yyyy-MM-dd} 00:00:00' AND p.created_at <= '{createdAtFilter.Value:yyyy-MM-dd} 23:59:59'" : "1=1";

                // Query for projects
                string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                 FROM projects p 
                WHERE
                    {projectNameCondition} AND
                    {CreatedByCondition} AND
                    {createdDateCondition} 
                ORDER BY p.created_at DESC";
                var countQueryFinal = string.Format($"select count(*) from ({GeneralQuery})a", CountQuery, projectNameA, createdByA, createdDateA);
                var totalRecords = await db.ExecuteScalarAsync<int>(countQueryFinal, new { projectName, createdByFilter, createdAtFilter, start, pageSize });
                var projectQueryFinal = string.Format(GeneralQuery + LimitQuery, SelectQuery, projectNameA, createdByA, createdDateA);
                var projects = await db.QueryAsync<ProjectData>(projectQueryFinal, new { projectName, createdByFilter, createdAtFilter, start, pageSize });
               
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                // Preserve filter values
                ViewData["CreatedBy"] = createdByFilter;
                ViewData["Project"] = projectName;
                ViewData["page"] = page.Value;
                ViewData["pageSize"] = pageSize;
                ViewData["totalRecords"] = totalRecords;
                ViewData["totalPages"] = totalPages;
                ViewBag.CreatedDate = createdAtFilter?.ToString("yyyy-MM-dd");
                ViewBag.PageSize = pageSize;
                return View(projects);
            }
        }      
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateProject()
        {
            var meterModes = await GetMeterModesAsync();

            ViewBag.MeterModes = meterModes.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Meter_Mode
            }).ToList();
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> CreateProject(ProjectData ProjectData)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                ModelState.AddModelError("", "User is not authenticated.");
                return View(ProjectData); // Return with error message if user is not authenticated
            }
            ProjectData.CreatedBy = userId.Value;
            ProjectData.CreatedAt = DateTime.Now;
            
            
            if (ModelState.IsValid)
            {
                using (IDbConnection db1 = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
                {
                    string currentDbQuery = "SELECT DATABASE();";
                    string currentDbName = await db1.ExecuteScalarAsync<string>(currentDbQuery);

                    if (string.IsNullOrEmpty(currentDbName))
                    {
                        return BadRequest("Unable to retrieve the database name.");
                    }
                    // Get db_id from project_databases based on the current database name
                    string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                    int? dbId = await db.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                    if (!dbId.HasValue)
                    {
                        return BadRequest("Database ID not found in project_databases table.");
                    }                  
                
                    // Check for existing MSN
                    var existingMsn = db.QuerySingleOrDefault<int?>(
                        "SELECT COUNT(*) FROM projects WHERE name = @ProjectName",
                        new { ProjectName = ProjectData.ProjectName });

                    if (existingMsn.HasValue && existingMsn.Value > 0)
                    {
                        ModelState.AddModelError("ProjectName", "The entered Project Name already exists.");
                        return View(ProjectData);
                    }

                    // Insert into projects table
                    string insertProjectQuery = @"
                    INSERT INTO projects (name, created_at, created_by,db_id,metermode_id)
                    VALUES (@ProjectName, @CreatedAt, @CreatedBy,@dbId,@metermode_id);
                    SELECT LAST_INSERT_ID();"; // Fetch last inserted ID

                    int projectId = db.ExecuteScalar<int>(insertProjectQuery, new
                    {
                        ProjectName = ProjectData.ProjectName,
                        CreatedBy = ProjectData.CreatedBy,
                        CreatedAt = ProjectData.CreatedAt,
                        dbId= dbId,
                        metermode_id = ProjectData.MeterModeId
                    });
                    // Insert into ProjectAttributes table
                    string insertAttributesQuery = @"
                    INSERT INTO project_attributes (project_id, isimei, isimsi, isbarcode)
                    VALUES (@ProjectId, @IsIMEI, @IsIMSI, @IsBarcode)";

                    db.Execute(insertAttributesQuery, new
                    {
                        ProjectId = projectId,
                        IsIMEI = ProjectData.IsIMEI,
                        IsIMSI = ProjectData.IsIMSI,
                        IsBarcode = ProjectData.IsBarcode
                    });

                    return RedirectToAction("ViewProjectList");
                }
            }

            return View(ProjectData);
        }
    }
}
