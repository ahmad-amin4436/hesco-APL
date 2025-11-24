using Dapper;
using Google.Protobuf.Reflection;
using HESCO.Models;
using HESCO.Models.Complaint;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using MySql.Data.MySqlClient.Authentication;
using Newtonsoft.Json;

//using NuGet.Protocol.Plugins;
using OfficeOpenXml;
using Org.BouncyCastle.Asn1.X500;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using logModel = HESCO.Models.Complaint.logModel;
using LogViewModel = HESCO.Models.Complaint.LogViewModel;

namespace HESCO.Controllers
{
    public class ComplaintController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly PdfService _pdfService;
        private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string CountQuery = " c.meter_msn ";
        private const string FaultyMetersCountQuery = " mf.msn ";
        private const string LimitQuery = " LIMIT @start,@fpageSize ";
        private const string FaultyLimitQuery = " LIMIT @start,@pageSize ";
        //c.executed_description AS Executed_Description, 
        //                d.description AS ExecutedDescriptionText,       
        private const string FaultyMetersSelectQuery = @"
                        mf.id AS Id,
                        mf.msn AS MSN,
                        mf.opened_at AS OpenedAt,
                        u1.username AS OpenedBy, 
                        u2.name AS AssignedTo,
                        mf.open_description AS OpenDescription,
                        mf.data_reset AS DataReset,
                        mf.received AS Received,
                        mf.remarks AS Remarks,
                        mf.dispatched_remarks AS Dispatched_Remarks,
                        c.removed_date AS RemovedDate,
                        mf.status";
        private const string SelectQuery = @"
                        c.id AS Id, 
c.ref_number AS Reference_No, 
                        c.meter_msn AS MSN, 
 c.subdiv_name AS SubDiv,
                        c.open_description AS Open_Description,
                        CASE
                            WHEN d.description IS NOT NULL THEN d.description
                            ELSE c.executed_description
                        END AS ExecutedDescriptionText,                      
                        c.opened_at AS OpenedAt,
                        c.close_description AS Close_Description, 
                        c.closed_at AS ClosedAt,                        
                        c.execution_date AS Execution_Date,
                        c.status, 
                        c.is_faulty AS Is_Faulty,
                        c.is_priority AS Priority,
                        c.longitude AS Longitude,
                        c.latitude AS Latitude,
                        u1.username AS OpenedByUsername,
                        u2.username AS ClosedByUsername,
                        u3.username AS AssignedToUsername,
                        CASE
                            WHEN c.status = 1 THEN c.execution_date
                            WHEN c.status = 2 THEN c.replaced_date
                            WHEN c.status = 3 THEN c.execution_date
                            WHEN c.status = 4 THEN c.mute_date
                            WHEN c.status = 5 THEN c.to_be_check_date
                            WHEN c.status = 6 THEN c.toberemoved_date
                            WHEN c.status = 7 THEN c.removed_date
                            WHEN c.status = 8 THEN c.returned_to_subdiv_date
                            WHEN c.status = 9 THEN c.not_under_warranty_date
                            WHEN c.status = 10 THEN c.with_subdiv_date
 WHEN c.status = 11 THEN c.under_warranty_date
                            ELSE NULL
                        END AS DisplayDate";
        public ComplaintController(IConfiguration configuration, IWebHostEnvironment webHostEnvironment, PdfService pdfService)
        {
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
            _pdfService = pdfService;
        }
        //[HttpGet]
        //public async Task<IActionResult> GetMSNByReferenceNo(string referenceNo)
        //{
        //    if (string.IsNullOrEmpty(referenceNo))
        //        return BadRequest("ReferenceNo is required.");

        //    string msn = null;
        //    string query = @"
        //    SELECT COALESCE(new_msn, meter_msn) AS msn  
        //    FROM meter_complaint  
        //    WHERE ref_number = @referenceNo    
        //    LIMIT 1";


        //    try
        //    {
        //        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //        {
        //            using (MySqlCommand command = new MySqlCommand(query, connection))
        //            {
        //                command.Parameters.AddWithValue("@referenceNo", referenceNo);
        //                await connection.OpenAsync();
        //                var result = await command.ExecuteScalarAsync();
        //                msn = result != null ? result.ToString() : null;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        return StatusCode(500, "Internal Server Error");
        //    }

        //    return Json(new { msn });
        //}

        //[HttpGet]
        //public async Task<IActionResult> UpdateInstallationLocation()
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        string query = "SELECT ref_number as Value, ref_number as Text FROM meter_complaint";
        //        var refNumberData = await db.QueryAsync<DTODropdown>(query);
        //        ViewBag.SelectRefNumber = new SelectList(refNumberData, "Value", "Text");
        //    }
        //    return View();
        //}
        //[HttpPost]
        //public async Task<IActionResult> UpdateInstallationLocation(UpdateInstallationLocationModel updateInstallationLocationModel)
        //{
        //    var userId = HttpContext.Session.GetInt32("UserId");

        //    if (!userId.HasValue)
        //    {
        //        string unauthorizedMessage = "User is not authenticated. Redirecting to login page...";
        //        string loginUrl = Url.Action("LoginUser", "Account"); // Replace with your login URL

        //        // Return an HTML response that includes the unauthorized message and JavaScript redirection
        //        string htmlContent = $@"
        //        <html>
        //            <body>
        //                <h3>{unauthorizedMessage}</h3>
        //                <script>
        //                    setTimeout(function() {{
        //                        window.location.href = '{loginUrl}';
        //                    }}, 1000); // Redirects after 1 second
        //                </script>
        //            </body>
        //        </html>";

        //        return Content(htmlContent, "text/html");
        //    }

        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        db.Open();
        //        // Start a transaction to ensure the update is atomic
        //        using (var transaction = db.BeginTransaction())
        //        {
        //            try
        //            {
                       
        //                // Create the SQL query to update the meter_complaint table
        //                string updateQuery = @"
        //                UPDATE meter_complaint 
        //                SET latitude = @Latitude, 
        //                    longitude = @Longitude, 
        //                    location_updated_at = @UpdatedAt, 
        //                    location_updated_by = @UpdatedBy
        //                WHERE ref_number = @ReferenceNo";

        //                // Execute the update query using Dapper within the transaction
        //                var result = await db.ExecuteAsync(updateQuery, new
        //                {
        //                    Latitude = updateInstallationLocationModel.Latitude,
        //                    Longitude = updateInstallationLocationModel.Longitude,
        //                    UpdatedBy = userId.Value,
        //                    UpdatedAt = DateTime.Now,
        //                    ReferenceNo = updateInstallationLocationModel.ReferenceNo
        //                }, transaction);

        //                // Commit the transaction if successful
        //                if (result > 0)
        //                {
        //                    transaction.Commit();
        //                    return RedirectToAction("ViewComplaint");
        //                }
        //                else
        //                {
        //                    // Failure: Handle as needed
        //                    TempData["ErrorMessage"] = "Failed to update the location. Please try again.";
        //                    transaction.Rollback();  // Rollback transaction if update failed
        //                    return View(updateInstallationLocationModel);
        //                }
        //            }
        //            catch (Exception)
        //            {
        //                // In case of any exception, roll back the transaction
        //                transaction.Rollback();
        //                TempData["ErrorMessage"] = "An error occurred while updating the location.";
        //                return View(updateInstallationLocationModel);
        //            }
        //        }
        //    }
        //}

        public async Task<IActionResult> GetUserIdFromSession()
        {
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

            // If userId is valid, return it as a JSON or other IActionResult type
            return Json(new { userId = userId.Value });
        }
        [HttpGet]
        public async Task<IActionResult> GetMSNsByProjectId(string query, int projectId)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string msnQuery = "SELECT Id as Value, meter_msn as Text FROM meters WHERE meter_msn LIKE @SearchTerm AND project_id = @ProjectId LIMIT 10";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery, new { SearchTerm = "%" + query + "%", ProjectId = projectId });
                return Json(msnData);
            }
        }
        //[HttpGet]
        //public async Task<IActionResult> GetMSNs(string query)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        string referenceNoQuery = "SELECT Id as Value, meter_msn as Text FROM meters WHERE meter_msn LIKE @SearchTerm LIMIT 10";
        //        var referenceNoData = await db.QueryAsync<DTODropdown>(referenceNoQuery, new { SearchTerm = "%" + query + "%" });
        //        return Json(referenceNoData);
        //    }
        //}
        //[HttpGet]
        //public async Task<IActionResult> GetMSNAndSubDivByReferenceNo(int referenceNoId)
        //{
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        string query = @"
        //        SELECT i.meter_msn, i.m_sub_div, s.name AS subDivName, i.m_sub_div AS subDivCode
        //        FROM installations i
        //        LEFT JOIN survey_hesco_subdivision s ON i.m_sub_div = s.sub_div_code
        //        WHERE i.Id = @ReferenceNoId";

        //        var result = await db.QueryFirstOrDefaultAsync(query, new { ReferenceNoId = referenceNoId });

        //        if (result != null)
        //        {
        //            return Json(new { msn = result.meter_msn, subDivName = result.subDivName, subDivCode = result.subDivCode });
        //        }
        //        else
        //        {
        //            return Json(new { msn = "", subDivName = "", subDivCode = "" });
        //        }
        //    }
        //}
        [HttpGet]
        public async Task<IActionResult> AddLetter()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string msnQuery = "SELECT DISTINCT meter_msn AS value, meter_msn AS text FROM meter_complaint where status=3";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> AddLetter(AddLetterModel letterModel, List<IFormFile> ComplaintImages)
        {
            // Check if MSN exists in meter_complaints table and fetch complaintId and subDiv
            //string fetchComplaintQuery = @"
            //SELECT id, sub_div 
            //FROM meter_complaint 
            //WHERE meter_msn = @Msn";
            string fetchComplaintQuery = @"
            SELECT id 
            FROM meter_complaint 
            WHERE meter_msn = @Msn
            ORDER BY opened_at DESC
            LIMIT 1";

            int complaintId = 0;
            //int? subDiv = null;

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var result = await db.QueryFirstOrDefaultAsync<dynamic>(fetchComplaintQuery, new { Msn = letterModel.MSN });

                if (result != null)
                {
                    complaintId = result.id;
                    //subDiv = int.TryParse(result.sub_div.ToString(), out int parsedSubDiv) ? parsedSubDiv : (int?)null;
                }
                else
                {
                    // Handle the case where the MSN doesn't exist in the meter_complaints table
                    TempData["ErrorMessage"] = "MSN not found in the meter_complaints table.";
                    return View(letterModel);
                }
            }

            // If no complaint data found, exit early
            if (complaintId == 0)
            {
                TempData["ErrorMessage"] = "Unable to fetch complaint details based on MSN.";
                return View(letterModel);
            }

            //// Save files to the server
            //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/complaintImages");

            //foreach (var file in ComplaintImages)
            //{
            //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            //    // Save the file to the server
            //    using (var fileStream = new FileStream(filePath, FileMode.Create))
            //    {
            //        await file.CopyToAsync(fileStream);
            //    }
            var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\complaintImages";
            // List of allowed extensions
            var allowedExtensions = new List<string> { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xml" };

            // Iterate through the uploaded files
            foreach (var file in ComplaintImages)
            {
                // Get the file extension
                var extension = Path.GetExtension(file.FileName).ToLower();

                // Validate the file size and extension
                if (file.Length > 0 && allowedExtensions.Contains(extension))
                {
                    // Generate a unique file name with a timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: yyyyMMdd_HHmmssfff
                    var uniqueFileName = $"{timestamp}{extension}"; // e.g., 20250117_153045123.png

                    // Combine the directory path with the unique file name
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save the file to the specified directory
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    // Save the file information in the complaint_images table
                    string insertImageQuery = @"
                INSERT INTO complaint_images (complaint_id, file_path)
                VALUES (@ComplaintId, @FileName)";

                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(insertImageQuery, new
                        {
                            ComplaintId = complaintId,
                            FileName = uniqueFileName
                        });
                    }
                }
            }

            // Redirect to ViewComplaint after saving the files
            return RedirectToAction("ViewComplaint");
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> AddFaultyMeterLetter()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string msnQuery = "SELECT DISTINCT msn AS value, msn AS text FROM meters_faulty";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> AddFaultyMeterLetter(AddLetterModel letterModel, List<IFormFile> FaultyImages)
        {
            // Check if MSN exists in meter_complaints table and fetch complaintId and subDiv
            string fetchFaultyMeterQuery = @"
            SELECT id 
            FROM meters_faulty 
            WHERE msn = @Msn
            ORDER BY opened_at DESC
            LIMIT 1";

            int FaultId = 0;
            //int? subDiv = null;

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var result = await db.QueryFirstOrDefaultAsync<dynamic>(fetchFaultyMeterQuery, new { Msn = letterModel.MSN });

                if (result != null)
                {
                    FaultId = result.id;
                    //subDiv = int.TryParse(result.sub_div.ToString(), out int parsedSubDiv) ? parsedSubDiv : (int?)null;
                }
                else
                {
                    // Handle the case where the MSN doesn't exist in the meters_faulty table
                    TempData["ErrorMessage"] = "MSN not found in the meters_faulty table.";
                    return View(letterModel);
                }
            }

            // If no complaint data found, exit early
            if (FaultId == 0)
            {
                TempData["ErrorMessage"] = "Unable to fetch faulty meter details based on MSN.";
                return View(letterModel);
            }

            //// Save files to the server
            //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/FaultImages");
            ////var uploadsFolder = Path.Combine("D:\\xampp\\htdocs\\lesco\\backend\\web\\uploads\\meters_complaint");
            //foreach (var file in FaultyImages)
            //{
            //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            //    using (var fileStream = new FileStream(filePath, FileMode.Create))
            //    {
            //        await file.CopyToAsync(fileStream);
            //    }
            var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\FaultImages";
            // List of allowed extensions
            var allowedExtensions = new List<string> { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xml" };

            // Iterate through the uploaded files
            foreach (var file in FaultyImages)
            {
                // Get the file extension
                var extension = Path.GetExtension(file.FileName).ToLower();

                // Validate the file size and extension
                if (file.Length > 0 && allowedExtensions.Contains(extension))
                {
                    // Generate a unique file name with a timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: yyyyMMdd_HHmmssfff
                    var uniqueFileName = $"{timestamp}{extension}"; // e.g., 20250117_153045123.png

                    // Combine the directory path with the unique file name
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save the file to the specified directory
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    // Save the file information in the complaint_images table
                    string insertImageQuery = @"
                        INSERT INTO faulty_images ( fault_id, file_path)
                        VALUES (@FaultId, @FileName)";

                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(insertImageQuery, new
                        {
                            FaultId = FaultId,
                            FileName = uniqueFileName
                        });
                    }
                }
            }

            // Redirect to ViewFaultyMeters after saving the files
            return RedirectToAction("ViewFaultyMeters");
        }

        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateComplaint()
        {

            //using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            //{
            //    dbDefault.Open();
            //    dbMaster.Open();
            //    // Get the currently connected database name
            //    string currentDbQuery = "SELECT DATABASE();";
            //    string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

            //    if (string.IsNullOrEmpty(currentDbName))
            //    {
            //        return BadRequest("Unable to retrieve the database name.");
            //    }

            //    // Get db_id from project_databases based on the current database name
            //    string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
            //    int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

            //    if (!dbId.HasValue)
            //    {
            //        return BadRequest("Database ID not found in project_databases table.");
            //    }

            //    // Get projects based on the retrieved db_id
            //    string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
            //    var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

            //    ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
            //}
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

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT user_role FROM user WHERE id = @UserId";
                var userRole = await db.ExecuteScalarAsync<int>(query, new { UserId = userId.Value });

                if (userRole == 14)
                {
                    return RedirectToAction("CreateFieldStaffComplaint");
                }
            }


            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetReferenceNos(string query)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string referenceNoQuery = @"
            SELECT Id AS Value, reference_no AS Text 
            FROM meter_installation 
            WHERE reference_no LIKE @SearchTerm 
            LIMIT 10";

                var referenceNoData = await db.QueryAsync<DTODropdown>(
                    referenceNoQuery,
                    new { SearchTerm = "%" + query + "%" });

                return Json(referenceNoData);
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetTelcos(string query = "")
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string telcoQuery = @"
            SELECT id AS Value, name AS Text 
            FROM telco
            WHERE name LIKE @SearchTerm
            LIMIT 10";

                var telcoData = await db.QueryAsync<DTODropdown>(
                    telcoQuery,
                    new { SearchTerm = "%" + query + "%" });

                return Json(telcoData);
            }
        }

        public async Task<IActionResult> GetMSNAndAddressByReferenceNo(int referenceNoId)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = @"
        SELECT msn, address, subdiv_code, subdiv_name
        FROM meter_installation
        WHERE id = @ReferenceNoId";

                var result = await db.QueryFirstOrDefaultAsync(query, new { ReferenceNoId = referenceNoId });

                if (result != null)
                {
                    return Json(new { msn = result.msn, address = result.address, subdiv_code = result.subdiv_code, subdiv_name = result.subdiv_name });
                }
                else
                {
                    return Json(new { msn = "", address = "" });
                }
            }
        }

        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateFieldStaffComplaint()
        {           
            ComplaintData complaint = new();
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
            ViewBag.SignedInUserId = userId.Value;
            using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                dbDefault.Open();
                //dbMaster.Open();
                //// Get the currently connected database name
                //string currentDbQuery = "SELECT DATABASE();";
                //string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

                //if (string.IsNullOrEmpty(currentDbName))
                //{
                //    return BadRequest("Unable to retrieve the database name.");
                //}

                //// Get db_id from project_databases based on the current database name
                //string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                //int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                //if (!dbId.HasValue)
                //{
                //    return BadRequest("Database ID not found in project_databases table.");
                //}

                //// Get projects based on the retrieved db_id
                //string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
                //var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

                //ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");

                string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
                var descriptionData = await dbDefault.QueryAsync<DTODropdown>(descriptionQuery);
                ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");
            }
            //using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //{
            //    string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
            //    var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
            //    ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");

            //    string projectQuery = "SELECT Id as value, name as text FROM projects";
            //    var projectData = (await db.QueryAsync<DTODropdown>(projectQuery)).Distinct().ToList();
            //    ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
            //}
            return View(complaint);
        }
        [HttpPost]
        public async Task<IActionResult> CreateFieldStaffComplaint(ComplaintData complaintData, List<IFormFile> ComplaintImages, string? OtherExecutedDescription)
        {
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
            complaintData.OpenedAt = DateTime.Now;
            complaintData.OpenedBy = userId.Value;
            complaintData.AssignedTo = userId.Value;

            string checkQuery = @"
            SELECT COUNT(*) 
            FROM meter_complaint 
            WHERE meter_msn = @MSN AND status IN (0,1,2,4,5,6,7,8,9,10,11)";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                //using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
                {
                    
                    using (MySqlCommand command = new MySqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MSN", complaintData.MSN);
                        connection.Open();
                        var existingComplaintCount = Convert.ToInt32(await command.ExecuteScalarAsync());

                        if (existingComplaintCount > 0)
                        {
                            ModelState.AddModelError(string.Empty, "Complaint already registered against this MSN.");
                            // Get the currently connected database name
                            //string currentDbQuery = "SELECT DATABASE();";
                            //string currentDbName = await connection.ExecuteScalarAsync<string>(currentDbQuery);

                            //if (string.IsNullOrEmpty(currentDbName))
                            //{
                            //    return BadRequest("Unable to retrieve the database name.");
                            //}

                            //// Get db_id from project_databases based on the current database name
                            //string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
                            //int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

                            //if (!dbId.HasValue)
                            //{
                            //    return BadRequest("Database ID not found in project_databases table.");
                            //}

                            //// Get projects based on the retrieved db_id
                            //string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
                            //var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

                            //ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
                            string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
                            var descriptionData = await connection.QueryAsync<DTODropdown>(descriptionQuery);
                            ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");
                            return View(complaintData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error while checking existing complaints.");
            }
            int complaintId = 0;
            string insertQuery = @"
            INSERT INTO meter_complaint ( ref_number, meter_msn, address, sub_div, subdiv_name, executed_description, opened_at, execution_date, opened_by, assigned_to, status, reported_by, open_description, is_faulty, is_priority, replaced_date, toberemoved_date, project_id, new_imsi, old_imsi,telco_old,telco_new, remarks)
            VALUES (@Reference_No, @MSN, @Address, @SubDivisionCode, @SubDivisionName, @Executed_Description, @OpenedAt, @Execution_Date, @OpenedBy, @AssignedTo, @Status, @ComplaintReportedBy, @Open_Description, @IsFaulty, @Priority, @Replaced_Date, @TobeRemovedDate, @Project, @New_Imsi, @Old_Imsi, @Telco_Old, @Telco_New, @Remarks);
            SELECT LAST_INSERT_ID();"; // Fetch last inserted complaintId
            switch (complaintData.Status)
            {
                case 0: // Open
                    complaintData.Status = 0; // Open                   
                    break;

                case 1: // Executed
                    complaintData.Execution_Date = DateTime.Now;
                    if (!string.IsNullOrEmpty(OtherExecutedDescription))
                    {
                        complaintData.Executed_Description = OtherExecutedDescription;
                    }
                    complaintData.Status = 1;
                    break;

                case 2: // Reinstalled/Replaced
                    complaintData.Replaced_Date = DateTime.Now;
                    complaintData.Status = 2;
                    break;

                case 6: // To be Removed
                    complaintData.TobeRemovedDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(OtherExecutedDescription))
                    {
                        complaintData.Executed_Description = OtherExecutedDescription;
                    }
                    complaintData.Status = 6;
                    complaintData.IsFaulty = 1;
                    complaintData.Priority = 1;
                    break;

                // Add more cases if necessary
                default:
                    ModelState.AddModelError(string.Empty, "Invalid status provided.");
                    return View(complaintData);
            }
            string username;
            string project;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                username = await db.QueryFirstOrDefaultAsync<string>(
                    "SELECT username FROM user WHERE id = @UserId",
                    new { UserId = userId.Value });
                project = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT project_id FROM meters WHERE meter_msn = @MSN",
                new { MSN = complaintData.MSN });
            }
            complaintData.Project = project;

            if (complaintData.ComplaintReportedBy == "0")
            {
                complaintData.ComplaintReportedBy = username;
            }
            if (ModelState.IsValid)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Reference_No", complaintData.Reference_No);
                            command.Parameters.AddWithValue("@MSN", complaintData.MSN);
                            command.Parameters.AddWithValue("@Address", complaintData.Address);
                            command.Parameters.AddWithValue("@SubDivisionCode", complaintData.SubDivisionCode);
                            command.Parameters.AddWithValue("@SubDivisionName", complaintData.SubDivisionName);
                            command.Parameters.AddWithValue("@Executed_Description", complaintData.Executed_Description);
                            command.Parameters.AddWithValue("@OpenedAt", complaintData.OpenedAt);
                            command.Parameters.AddWithValue("@Execution_Date", complaintData.Execution_Date);
                            command.Parameters.AddWithValue("@OpenedBy", complaintData.OpenedBy);
                            command.Parameters.AddWithValue("@AssignedTo", complaintData.AssignedTo);
                            command.Parameters.AddWithValue("@Status", complaintData.Status);
                            //command.Parameters.AddWithValue("@SubDiv", complaintData.SubDiv);
                            command.Parameters.AddWithValue("@ComplaintReportedBy", complaintData.ComplaintReportedBy);
                            command.Parameters.AddWithValue("@Open_Description", complaintData.Open_Description);
                            command.Parameters.AddWithValue("@IsFaulty", complaintData.IsFaulty);
                            command.Parameters.AddWithValue("@Priority", complaintData.Priority);
                            command.Parameters.AddWithValue("@Replaced_Date", complaintData.Replaced_Date);
                            command.Parameters.AddWithValue("@TobeRemovedDate", complaintData.TobeRemovedDate);
                            command.Parameters.AddWithValue("@Project", complaintData.Project);
                            command.Parameters.AddWithValue("@old_imsi", complaintData.Old_Imsi);
                            command.Parameters.AddWithValue("@new_imsi", complaintData.New_Imsi);
                            command.Parameters.AddWithValue("@telco_old", complaintData.Telco_Old);
                            command.Parameters.AddWithValue("@telco_new", complaintData.Telco_New);
                            command.Parameters.AddWithValue("@Remarks", complaintData.Remarks);
                            connection.Open();
                            complaintId = Convert.ToInt32(await command.ExecuteScalarAsync()); // Fetch generated complaintId
                        }
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Internal server error while processing complaint.");
                }


                // Now insert logs using the correct complaintId
                    string insertLogQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime, complaint_id)
                VALUES (@userId, @Action, @ActionDate, @complaintId)";

                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = complaintData.Status switch
                    {
                        0 => "Complaint Opened",
                        1 => "Executed",
                        2 => "Replaced/Reinstalled",
                        6 => "To be Removed",
                        _ => "Unknown Action"
                    },
                    complaintId = complaintId // Now correctly assigned
                };

                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(insertLogQuery, connection))
                    {
                        command.Parameters.AddWithValue("@userId", logModel.userId);
                        command.Parameters.AddWithValue("@complaintId", complaintId);
                        command.Parameters.AddWithValue("@Action", logModel.Action);
                        command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                        connection.Open();
                        await command.ExecuteNonQueryAsync();
                    }
                }
                if (!string.IsNullOrEmpty(complaintData.URL))
                {
                    string LocationQuery = @"
                     INSERT INTO meter_location ( location_url, location_at,location_by,complaint_id)
                     VALUES (@URL,@LocationAt,@LocationBy,@complaintId)";
                    LocationModel locationModel = new()
                    {
                        URL = complaintData.URL,
                        LocationBy = userId.Value,
                        LocationAt = DateTime.Now,
                        complaintId = complaintId
                    };
                    if (ModelState.IsValid)
                    {
                        try
                        {
                            using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                using (MySqlCommand command = new MySqlCommand(LocationQuery, connection))
                                {
                                    command.Parameters.AddWithValue("@URL", complaintData.URL);
                                    command.Parameters.AddWithValue("@complaintId", locationModel.complaintId);
                                    command.Parameters.AddWithValue("@LocationBy", locationModel.LocationBy);
                                    command.Parameters.AddWithValue("@LocationAt", locationModel.LocationAt);
                                    connection.Open();
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            return StatusCode(500, "Internal server error while Adding Location.");
                        }
                    }
                    string logQuery = @"
                         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                         VALUES (@userId,@Action,@ActionDate,@complaintId)";
                    logModel logModel1 = new()
                    {
                        userId = userId.Value,
                        ActionDate = DateTime.Now,
                        Action = "Location Added",
                        complaintId = complaintId
                    };
                    if (ModelState.IsValid)
                    {
                        try
                        {
                            using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                using (MySqlCommand command = new MySqlCommand(logQuery, connection))
                                {
                                    command.Parameters.AddWithValue("@userId", logModel1.userId);
                                    command.Parameters.AddWithValue("@complaintId", logModel1.complaintId);
                                    command.Parameters.AddWithValue("@Action", logModel1.Action);
                                    command.Parameters.AddWithValue("@ActionDate", logModel1.ActionDate);
                                    connection.Open();
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            return StatusCode(500, "Internal server error while Adding Location Logs.");
                        }
                    }
                }

                if (ComplaintImages != null && ComplaintImages.Count > 0)
                {
                    var allowedExtensions = new[] { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", "xls", ".xlsx", ".xml" };
                    //var uploadPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/complaintImages");
                    ////var uploadPath = @"D:\xampp\htdocs\lesco\backend\web\uploads\meters_complaint";
                    //if (!Directory.Exists(uploadPath))
                    //{
                    //    Directory.CreateDirectory(uploadPath);
                    //}
                    //foreach (var file in ComplaintImages)
                    //{
                    //    var extension = Path.GetExtension(file.FileName).ToLower();

                    //    if (file.Length > 0 && allowedExtensions.Contains(extension))
                    //    {
                    //        //var fileName = Path.GetFileName(file.FileName);
                    //        //var filePath = Path.Combine(uploadPath, fileName);

                    //        //using (var stream = new FileStream(filePath, FileMode.Create))
                    //        //{
                    //        //    await file.CopyToAsync(stream);
                    //        //}
                    //        var fileName = Path.GetFileName(file.FileName);
                    //        var filePath = Path.Combine(uploadPath, fileName);

                    //        // Overwrite the existing file if it has the same name
                    //        using (var stream = new FileStream(filePath, FileMode.Create))
                    //        {
                    //            await file.CopyToAsync(stream);
                    //        }

                    // Set the upload path to your desired directory
                    var uploadPath = @"D:\xampp\htdocs\HESCOPhotos\complaintImages";

                    // Ensure the directory exists
                    if (!Directory.Exists(uploadPath))
                    {
                        Directory.CreateDirectory(uploadPath);
                    }
                    // Iterate through the uploaded files
                    foreach (var file in ComplaintImages)
                    {
                        // Get the file extension
                        var extension = Path.GetExtension(file.FileName).ToLower();

                        // Validate the file size and extension
                        if (file.Length > 0 && allowedExtensions.Contains(extension))
                        {
                            // Generate a unique file name with a timestamp
                            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: 20250117_153045123
                            var fileName = $"{timestamp}{extension}"; // Combine timestamp with extension

                            // Combine the root directory path and file name
                            var filePath = Path.Combine(uploadPath, fileName);

                            // Save the file to the specified directory
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            // Iterate through the uploaded files
                            //foreach (var file in ComplaintImages)
                            //{
                            //    // Get the file extension
                            //    var extension = Path.GetExtension(file.FileName).ToLower();

                            //    // Validate the file size and extension
                            //    if (file.Length > 0 && allowedExtensions.Contains(extension))
                            //    {
                            //        // Generate the file name
                            //        var fileName = Path.GetFileName(file.FileName);

                            //        // Combine the path and file name
                            //        var filePath = Path.Combine(uploadPath, fileName);

                            //        // Save the file to the specified directory
                            //        using (var stream = new FileStream(filePath, FileMode.Create))
                            //        {
                            //            await file.CopyToAsync(stream);
                            //        }                                                



                            string insertImageQuery = @"
                                        INSERT INTO complaint_images (complaint_id, file_path)
                                        VALUES (@ComplaintId, @FileName)";

                            using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                using (MySqlCommand command = new MySqlCommand(insertImageQuery, connection))
                                {
                                    //command.Parameters.AddWithValue("@SubDiv", complaintData.SubDiv);
                                    command.Parameters.AddWithValue("@ComplaintId", complaintId);
                                    command.Parameters.AddWithValue("@FileName", fileName);
                                    connection.Open();
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, $"The file {file.FileName} has an invalid extension.");
                            return View(complaintData);
                        }
                    }
                }
            
            return RedirectToAction("ViewComplaint");
        }
            //string insertQuery = @"
            //INSERT INTO meter_complaint ( meter_msn, executed_description,opened_at, execution_date, opened_by,assigned_to, sub_div, status, reported_by,open_description,is_faulty,is_priority,replaced_date,toberemoved_date,project_id)
            //VALUES ( @MSN, @Executed_Description,@OpenedAt, @Execution_Date, @OpenedBy,@AssignedTo, @SubDiv, @Status,@ComplaintReportedBy,@Open_Description,@IsFaulty,@Priority,@Replaced_Date,@TobeRemovedDate,@Project)";
           
        //switch (complaintData.Status)
            //{
            //    case 0: // Open
            //        complaintData.Status = 0; // Open
            //        string insertQuery2 = @"
            //         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
            //         VALUES (@userId,@Action,@ActionDate,@complaintId)";
            //        logModel logModel = new()
            //        {
            //            userId = userId.Value,
            //            ActionDate = DateTime.Now,
            //            Action = "Complaint Opened",
            //            complaintId = complaintData.ComplaintId
            //        };
            //        if (ModelState.IsValid)
            //        {
            //            try
            //            {
            //                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //                {
            //                    using (MySqlCommand command = new MySqlCommand(insertQuery2, connection))
            //                    {
            //                        command.Parameters.AddWithValue("@userId", logModel.userId);
            //                        command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
            //                        command.Parameters.AddWithValue("@Action", logModel.Action);
            //                        command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
            //                        connection.Open();
            //                        await command.ExecuteNonQueryAsync();
            //                    }
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                Console.WriteLine(ex.Message);
            //                return StatusCode(500, "Internal server error while processing complaint.");
            //            }
            //        }
            //        break;

            //    case 1: // Executed
            //        complaintData.Execution_Date = DateTime.Now;
            //        if (!string.IsNullOrEmpty(OtherExecutedDescription))
            //        {
            //            complaintData.Executed_Description = OtherExecutedDescription;
            //        }
            //        complaintData.Status = 1;
            //        string insertQuery3 = @"
            //         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
            //         VALUES (@userId,@Action,@ActionDate,@complaintId)";
            //        logModel logModel1 = new()
            //        {
            //            userId = userId.Value,
            //            ActionDate = DateTime.Now,
            //            Action = "Executed",
            //            complaintId = complaintData.ComplaintId
            //        };
            //        if (ModelState.IsValid)
            //        {
            //            try
            //            {
            //                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //                {
            //                    using (MySqlCommand command = new MySqlCommand(insertQuery3, connection))
            //                    {
            //                        command.Parameters.AddWithValue("@userId", logModel1.userId);
            //                        command.Parameters.AddWithValue("@complaintId", logModel1.complaintId);
            //                        command.Parameters.AddWithValue("@Action", logModel1.Action);
            //                        command.Parameters.AddWithValue("@ActionDate", logModel1.ActionDate);
            //                        connection.Open();
            //                        await command.ExecuteNonQueryAsync();
            //                    }
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                Console.WriteLine(ex.Message);
            //                return StatusCode(500, "Internal server error while processing complaint.");
            //            }
            //        }
            //        break;

            //    case 2: // Reinstalled/Replaced
            //        complaintData.Replaced_Date = DateTime.Now;
            //        complaintData.Status = 2;
            //        string insertQuery4 = @"
            //         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
            //         VALUES (@userId,@Action,@ActionDate,@complaintId)";
            //        logModel logModel2 = new()
            //        {
            //            userId = userId.Value,
            //            ActionDate = DateTime.Now,
            //            Action = "Replaced/Reinstalled",
            //            complaintId = complaintData.ComplaintId
            //        };
            //        if (ModelState.IsValid)
            //        {
            //            try
            //            {
            //                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //                {
            //                    using (MySqlCommand command = new MySqlCommand(insertQuery4, connection))
            //                    {
            //                        command.Parameters.AddWithValue("@userId", logModel2.userId);
            //                        command.Parameters.AddWithValue("@complaintId", logModel2.complaintId);
            //                        command.Parameters.AddWithValue("@Action", logModel2.Action);
            //                        command.Parameters.AddWithValue("@ActionDate", logModel2.ActionDate);
            //                        connection.Open();
            //                        await command.ExecuteNonQueryAsync();
            //                    }
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                Console.WriteLine(ex.Message);
            //                return StatusCode(500, "Internal server error while processing complaint.");
            //            }
            //        }
            //        break;

            //    case 6: // To be Removed
            //        complaintData.TobeRemovedDate = DateTime.Now;
            //        if (!string.IsNullOrEmpty(OtherExecutedDescription))
            //        {
            //            complaintData.Executed_Description = OtherExecutedDescription;
            //        }
            //        complaintData.Status = 6;
            //        complaintData.IsFaulty = 1;
            //        complaintData.Priority = 1;
            //        string insertQuery5 = @"
            //         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
            //         VALUES (@userId,@Action,@ActionDate,@complaintId)";
            //        logModel logModel3 = new()
            //        {
            //            userId = userId.Value,
            //            ActionDate = DateTime.Now,
            //            Action = "To be Removed",
            //            complaintId = complaintData.ComplaintId
            //        };
            //        if (ModelState.IsValid)
            //        {
            //            try
            //            {
            //                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //                {
            //                    using (MySqlCommand command = new MySqlCommand(insertQuery5, connection))
            //                    {
            //                        command.Parameters.AddWithValue("@userId", logModel3.userId);
            //                        command.Parameters.AddWithValue("@complaintId", logModel3.complaintId);
            //                        command.Parameters.AddWithValue("@Action", logModel3.Action);
            //                        command.Parameters.AddWithValue("@ActionDate", logModel3.ActionDate);
            //                        connection.Open();
            //                        await command.ExecuteNonQueryAsync();
            //                    }
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                Console.WriteLine(ex.Message);
            //                return StatusCode(500, "Internal server error while processing complaint.");
            //            }
            //        }
            //        break;

            //    // Add more cases if necessary
            //    default:
            //        ModelState.AddModelError(string.Empty, "Invalid status provided.");
            //        return View(complaintData);
            //}

            ////string insertQuery = @"
            ////INSERT INTO meter_complaint (ref_number, meter_msn, executed_description,opened_at, execution_date, opened_by,assigned_to, sub_div, status, reported_by)
            ////VALUES (@Reference_No, @MSN, @Executed_Description,@OpenedAt, @Execution_Date, @OpenedBy,@AssignedTo, @SubDiv, 1,@ComplaintReportedBy)"; // Setting status to 1

            //// Fetch username from database based on userId
            //string username;
            //using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //{
            //    username = await db.QueryFirstOrDefaultAsync<string>(
            //        "SELECT username FROM user WHERE id = @UserId",
            //        new { UserId = userId.Value });
            //}

            //// Save username in ComplaintReportedBy if it's empty or default
            //if (string.IsNullOrEmpty(complaintData.ComplaintReportedBy))
            //{
            //    complaintData.ComplaintReportedBy = username;
            //}
            //if (ModelState.IsValid)
            //{
            //    try
            //    {
            //        int complaintId;
            //        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //        {
            //            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
            //            {
            //                //command.Parameters.AddWithValue("@Reference_No", complaintData.Reference_No);
            //                command.Parameters.AddWithValue("@MSN", complaintData.MSN);
            //                command.Parameters.AddWithValue("@Executed_Description", complaintData.Executed_Description);
            //                command.Parameters.AddWithValue("@OpenedAt", complaintData.OpenedAt);
            //                command.Parameters.AddWithValue("@Execution_Date", complaintData.Execution_Date);
            //                command.Parameters.AddWithValue("@OpenedBy", complaintData.OpenedBy);
            //                command.Parameters.AddWithValue("@AssignedTo", complaintData.AssignedTo);
            //                command.Parameters.AddWithValue("@Status", complaintData.Status);
            //                //command.Parameters.AddWithValue("@SubDiv", complaintData.SubDiv);
            //                command.Parameters.AddWithValue("@ComplaintReportedBy", complaintData.ComplaintReportedBy);
            //                command.Parameters.AddWithValue("@Open_Description", complaintData.Open_Description);
            //                command.Parameters.AddWithValue("@IsFaulty", complaintData.IsFaulty);
            //                command.Parameters.AddWithValue("@Priority", complaintData.Priority);
            //                command.Parameters.AddWithValue("@Replaced_Date", complaintData.Replaced_Date);
            //                command.Parameters.AddWithValue("@TobeRemovedDate", complaintData.TobeRemovedDate);
            //                command.Parameters.AddWithValue("@Project", complaintData.Project);
            //                connection.Open();
            //                await command.ExecuteNonQueryAsync();

            //                // Get the last inserted complaint ID
            //                complaintId = (int)command.LastInsertedId;
            //            }
            //        }

            //        if (ComplaintImages != null && ComplaintImages.Count > 0)
            //        {
            //            var allowedExtensions = new[] { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", "xls", ".xlsx", ".xml" };
            //            //var uploadPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/complaintImages");
            //            ////var uploadPath = @"D:\xampp\htdocs\lesco\backend\web\uploads\meters_complaint";
            //            //if (!Directory.Exists(uploadPath))
            //            //{
            //            //    Directory.CreateDirectory(uploadPath);
            //            //}
            //            //foreach (var file in ComplaintImages)
            //            //{
            //            //    var extension = Path.GetExtension(file.FileName).ToLower();

            //            //    if (file.Length > 0 && allowedExtensions.Contains(extension))
            //            //    {
            //            //        //var fileName = Path.GetFileName(file.FileName);
            //            //        //var filePath = Path.Combine(uploadPath, fileName);

            //            //        //using (var stream = new FileStream(filePath, FileMode.Create))
            //            //        //{
            //            //        //    await file.CopyToAsync(stream);
            //            //        //}
            //            //        var fileName = Path.GetFileName(file.FileName);
            //            //        var filePath = Path.Combine(uploadPath, fileName);

            //            //        // Overwrite the existing file if it has the same name
            //            //        using (var stream = new FileStream(filePath, FileMode.Create))
            //            //        {
            //            //            await file.CopyToAsync(stream);
            //            //        }

            //            // Set the upload path to your desired directory
            //            var uploadPath = @"D:\xampp\htdocs\HESCOPhotos\complaintImages";

            //            // Ensure the directory exists
            //            if (!Directory.Exists(uploadPath))
            //            {
            //                Directory.CreateDirectory(uploadPath);
            //            }
            //            // Iterate through the uploaded files
            //            foreach (var file in ComplaintImages)
            //            {
            //                // Get the file extension
            //                var extension = Path.GetExtension(file.FileName).ToLower();

            //                // Validate the file size and extension
            //                if (file.Length > 0 && allowedExtensions.Contains(extension))
            //                {
            //                    // Generate a unique file name with a timestamp
            //                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: 20250117_153045123
            //                    var fileName = $"{timestamp}{extension}"; // Combine timestamp with extension

            //                    // Combine the root directory path and file name
            //                    var filePath = Path.Combine(uploadPath, fileName);

            //                    // Save the file to the specified directory
            //                    using (var stream = new FileStream(filePath, FileMode.Create))
            //                    {
            //                        await file.CopyToAsync(stream);
            //                    }
            //                    // Iterate through the uploaded files
            //                    //foreach (var file in ComplaintImages)
            //                    //{
            //                    //    // Get the file extension
            //                    //    var extension = Path.GetExtension(file.FileName).ToLower();

            //                    //    // Validate the file size and extension
            //                    //    if (file.Length > 0 && allowedExtensions.Contains(extension))
            //                    //    {
            //                    //        // Generate the file name
            //                    //        var fileName = Path.GetFileName(file.FileName);

            //                    //        // Combine the path and file name
            //                    //        var filePath = Path.Combine(uploadPath, fileName);

            //                    //        // Save the file to the specified directory
            //                    //        using (var stream = new FileStream(filePath, FileMode.Create))
            //                    //        {
            //                    //            await file.CopyToAsync(stream);
            //                    //        }                                                



            //                    string insertImageQuery = @"
            //                    INSERT INTO complaint_images (sub_div, complaint_id, file_path)
            //                    VALUES (@SubDiv, @ComplaintId, @FileName)";

            //                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //                    {
            //                        using (MySqlCommand command = new MySqlCommand(insertImageQuery, connection))
            //                        {
            //                            //command.Parameters.AddWithValue("@SubDiv", complaintData.SubDiv);
            //                            command.Parameters.AddWithValue("@ComplaintId", complaintId);
            //                            command.Parameters.AddWithValue("@FileName", fileName);
            //                            connection.Open();
            //                            await command.ExecuteNonQueryAsync();
            //                        }
            //                    }
            //                }
            //                else
            //                {
            //                    ModelState.AddModelError(string.Empty, $"The file {file.FileName} has an invalid extension.");
            //                    return View(complaintData);
            //                }
            //            }
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex.Message);
            //        return StatusCode(500, "Internal server error while processing complaint.");
            //    }

            //    return RedirectToAction("ViewComplaint");
            //}
            ViewBag.SignedInUserId = userId.Value;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
                var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
                ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");
            }
            return View(complaintData);
        }

        [HttpPost]
        public async Task<IActionResult> CreateComplaint(ComplaintData complaintData, List<IFormFile> ComplaintImages)
        {
            //var userId = HttpContext.Session.GetInt32("UserId");

            //if (!userId.HasValue)
            //{
            //    return Unauthorized("User is not authenticated.");
            //}
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

            complaintData.OpenedAt = DateTime.Now;
            complaintData.OpenedBy = userId.Value;

            string checkQuery = @"
            SELECT COUNT(*) 
            FROM meter_complaint 
            WHERE meter_msn = @MSN AND status IN (0,1,2,4,5,6,7,8,9,10,11)";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MSN", complaintData.MSN);
                        connection.Open();
                        var existingComplaintCount = Convert.ToInt32(await command.ExecuteScalarAsync());

                        if (existingComplaintCount > 0)
                        {
                            ModelState.AddModelError(string.Empty, "Complaint already registered against this MSN.");
                            return View(complaintData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error while checking existing complaints.");
            }

            string insertQuery = @"
            INSERT INTO meter_complaint (ref_number, meter_msn, address, sub_div, subdiv_name, open_description, opened_at, opened_by, reported_by,project_id)
            VALUES (@Reference_No, @MSN, @Address, @SubDivisionCode, @SubDivisionName, @Open_Description, @OpenedAt, @OpenedBy, @ComplaintReportedBy,@Project)";
            // Fetch username from database based on userId
            string username;
            string project;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                username = await db.QueryFirstOrDefaultAsync<string>(
                    "SELECT username FROM user WHERE id = @UserId",
                    new { UserId = userId.Value });
                project = await db.QueryFirstOrDefaultAsync<string>(
              "SELECT project_id FROM meters WHERE meter_msn = @MSN",
              new { MSN = complaintData.MSN });
            }
            complaintData.Project = project;

            if (complaintData.ComplaintReportedBy == "0")
            {
                complaintData.ComplaintReportedBy = username;
            }
            if (ModelState.IsValid)
            {
                try
                {
                    int complaintId;
                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Reference_No", complaintData.Reference_No);
                            command.Parameters.AddWithValue("@MSN", complaintData.MSN);
                            command.Parameters.AddWithValue("@Address", complaintData.Address);
                            command.Parameters.AddWithValue("@SubDivisionCode", complaintData.SubDivisionCode);
                            command.Parameters.AddWithValue("@SubDivisionName", complaintData.SubDivisionName);
                            command.Parameters.AddWithValue("@Open_Description", complaintData.Open_Description);
                            command.Parameters.AddWithValue("@OpenedAt", complaintData.OpenedAt);
                            command.Parameters.AddWithValue("@OpenedBy", complaintData.OpenedBy);
                            command.Parameters.AddWithValue("@Project", complaintData.Project);
                            command.Parameters.AddWithValue("@ComplaintReportedBy", complaintData.ComplaintReportedBy);
                            connection.Open();
                            await command.ExecuteNonQueryAsync();

                            // Get the last inserted complaint ID
                            complaintId = (int)command.LastInsertedId;
                        }
                    }

                    if (ComplaintImages != null && ComplaintImages.Count > 0)
                    {
                        var allowedExtensions = new[] { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", "xls", ".xlsx", ".xml" };
                        //var uploadPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/complaintImages");

                        //// Ensure the directory exists
                        //if (!Directory.Exists(uploadPath))
                        //{
                        //    Directory.CreateDirectory(uploadPath);
                        //}


                        //foreach (var file in ComplaintImages)
                        //{
                        //    var extension = Path.GetExtension(file.FileName).ToLower();

                        //    if (file.Length > 0 && allowedExtensions.Contains(extension))
                        //    {
                        //        var fileName = Path.GetFileName(file.FileName);
                        //        var filePath = Path.Combine(uploadPath, fileName);
                        //        // Overwrite the existing file if it has the same name
                        //        using (var stream = new FileStream(filePath, FileMode.Create))
                        //        {
                        //            await file.CopyToAsync(stream);
                        //        }
                        // Set the upload path to your desired directory
                        var uploadPath = @"D:\xampp\htdocs\HESCOPhotos\complaintImages";

                        // Ensure the directory exists
                        if (!Directory.Exists(uploadPath))
                        {
                            Directory.CreateDirectory(uploadPath);
                        }

                        // Iterate through the uploaded files
                        foreach (var file in ComplaintImages)
                        {
                            // Get the file extension
                            var extension = Path.GetExtension(file.FileName).ToLower();

                            // Validate the file size and extension
                            if (file.Length > 0 && allowedExtensions.Contains(extension))
                            {
                                // Generate a unique file name with a timestamp
                                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: 20250117_153045123
                                var fileName = $"{timestamp}{extension}"; // Combine timestamp with extension

                                // Combine the root directory path and file name
                                var filePath = Path.Combine(uploadPath, fileName);

                                // Save the file to the specified directory
                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                string insertImageQuery = @"
                                INSERT INTO complaint_images (complaint_id, file_path)
                                VALUES ( @ComplaintId, @FileName)";

                                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                                {
                                    using (MySqlCommand command = new MySqlCommand(insertImageQuery, connection))
                                    {
                                        //command.Parameters.AddWithValue("@SubDiv", complaintData.SubDiv);
                                        command.Parameters.AddWithValue("@ComplaintId", complaintId);
                                        command.Parameters.AddWithValue("@FileName", fileName);
                                        connection.Open();
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            else
                            {
                                ModelState.AddModelError(string.Empty, $"The file {file.FileName} has an invalid extension.");
                                return View(complaintData);
                            }
                        }
                    }
                    string Query = @"
                     INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                     VALUES (@userId,@Action,@ActionDate,@complaintId)";
                    logModel logModel = new()
                    {
                        userId = userId.Value,
                        ActionDate = DateTime.Now,
                        Action = "Complaint Opened",
                        complaintId = complaintId
                    };
                    if (ModelState.IsValid)
                    {
                        try
                        {
                            using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                using (MySqlCommand command = new MySqlCommand(Query, connection))
                                {
                                    command.Parameters.AddWithValue("@userId", logModel.userId);
                                    command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                    command.Parameters.AddWithValue("@Action", logModel.Action);
                                    command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                    connection.Open();
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            return StatusCode(500, "Internal server error while processing complaint.");
                        }
                    }
                    if (!string.IsNullOrEmpty(complaintData.URL))
                    {
                        string LocationQuery = @"
                     INSERT INTO meter_location ( location_url, location_at,location_by,complaint_id)
                     VALUES (@URL,@LocationAt,@LocationBy,@complaintId)";
                        LocationModel locationModel = new()
                        {
                            URL = complaintData.URL,
                            LocationBy = userId.Value,
                            LocationAt = DateTime.Now,
                            complaintId = complaintId
                        };
                        if (ModelState.IsValid)
                        {
                            try
                            {
                                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                                {
                                    using (MySqlCommand command = new MySqlCommand(LocationQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@URL", complaintData.URL);
                                        command.Parameters.AddWithValue("@complaintId", locationModel.complaintId);
                                        command.Parameters.AddWithValue("@LocationBy", locationModel.LocationBy);
                                        command.Parameters.AddWithValue("@LocationAt", locationModel.LocationAt);
                                        connection.Open();
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return StatusCode(500, "Internal server error while Adding Location.");
                            }
                        }
                        string logQuery = @"
                         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                         VALUES (@userId,@Action,@ActionDate,@complaintId)";
                        logModel logModel1 = new()
                        {
                            userId = userId.Value,
                            ActionDate = DateTime.Now,
                            Action = "Location Added",
                            complaintId = complaintId
                        };
                        if (ModelState.IsValid)
                        {
                            try
                            {
                                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                                {
                                    using (MySqlCommand command = new MySqlCommand(logQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@userId", logModel1.userId);
                                        command.Parameters.AddWithValue("@complaintId", logModel1.complaintId);
                                        command.Parameters.AddWithValue("@Action", logModel1.Action);
                                        command.Parameters.AddWithValue("@ActionDate", logModel1.ActionDate);
                                        connection.Open();
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return StatusCode(500, "Internal server error while Adding Location Logs.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Internal server error while processing complaint.");
                }

                return RedirectToAction("ViewComplaint");
            }
           
            return View(complaintData);
        }

        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateBulkComplaint()
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
        public async Task<IActionResult> ImportFromExcel(ComplaintData complaintData ,IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToAction("CreateBulkComplaint", new { error = "No file selected" });
            }

            var complaints = new List<ComplaintData>();
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
            var existingMsns = new HashSet<string>();

            ExcelPackage.License.SetNonCommercialOrganization("Accurate");
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var rowCount = worksheet.Dimension.Rows;

                    using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();
                        for (var row = 2; row <= rowCount; row++)
                        {
                            var meterMsn = worksheet.Cells[row, 1].Text?.Trim();
                            var dateText = worksheet.Cells[row, 2].Text?.Trim();
                            var openDescription = worksheet.Cells[row, 3].Text?.Trim();

                            // ❗ Require both MSN and Open Description
                            if (string.IsNullOrWhiteSpace(meterMsn) || string.IsNullOrWhiteSpace(openDescription))
                            {
                                continue; // Skip this row
                            }

                            // Skip if already processed in this batch
                            if (existingMsns.Contains(meterMsn)) continue;

                            // Check if MSN already exists in DB
                            string checkQuery = @"
                            SELECT COUNT(*)
                            FROM meter_complaint
                            WHERE meter_msn = @MSN AND status IN (0,1,2,4,5,6,7,8,9,10,11)";

                            using (var command = new MySqlCommand(checkQuery, connection))
                            {
                                command.Parameters.AddWithValue("@MSN", meterMsn);
                                var count = Convert.ToInt32(await command.ExecuteScalarAsync());

                                if (count > 0)
                                {
                                    existingMsns.Add(meterMsn);
                                    continue; // Skip this row
                                }
                            }

                            // Parse optional date
                            DateTime? lastCommunicationTime = null;
                            if (DateTime.TryParse(dateText, out DateTime parsedDate))
                            {
                                lastCommunicationTime = parsedDate;
                            }

                            complaints.Add(new ComplaintData
                            {
                                MSN = meterMsn,
                                lastCommunicationTime = lastCommunicationTime,
                                Open_Description = openDescription,
                                OpenedAt = DateTime.Now,
                                OpenedBy = UserId,
                                Project = complaintData.Project
                            });
                        }
                        //for (var row = 2; row <= rowCount; row++)
                        //{
                        //    var meterMsn = worksheet.Cells[row, 1].Text;

                        //    // Check if the first cell (MSN) is empty, skip the row
                        //    if (string.IsNullOrWhiteSpace(meterMsn))
                        //    {
                        //        continue;
                        //    }

                        //    // Skip checking if MSN is already processed in this batch
                        //    if (existingMsns.Contains(meterMsn)) continue;

                        //    // Check if the MSN exists with status 0 or 1
                        //    string checkQuery = @"
                        //    SELECT COUNT(*)
                        //    FROM meter_complaint
                        //    WHERE meter_msn = @MSN AND status IN (0,1,2,4,5,6,7,8,9,10,11)";

                        //    using (var command = new MySqlCommand(checkQuery, connection))
                        //    {
                        //        command.Parameters.AddWithValue("@MSN", meterMsn);
                        //        var count = Convert.ToInt32(await command.ExecuteScalarAsync());

                        //        if (count > 0)
                        //        {
                        //            existingMsns.Add(meterMsn);
                        //            continue; // Skip this row
                        //        }
                        //    }

                        //    // Continue processing and adding the complaint if not skipped
                        //    var refNumber = worksheet.Cells[row, 2].Text;
                        //    DateTime? lastCommunicationTime = null;
                        //    if (DateTime.TryParse(worksheet.Cells[row, 3].Text, out DateTime parsedDate))
                        //    {
                        //        lastCommunicationTime = parsedDate;
                        //    }
                        //    var openDescription = worksheet.Cells[row, 4].Text;

                        //    complaints.Add(new ComplaintData
                        //    {
                        //        MSN = meterMsn,
                        //        //Reference_No = refNumber,
                        //        lastCommunicationTime = lastCommunicationTime,
                        //        //SubDiv = subDiv,
                        //        Open_Description = openDescription,
                        //        OpenedAt = DateTime.Now,
                        //        OpenedBy = UserId,
                        //        Project = complaintData.Project
                        //    });
                        //}
                    }
                }
            }

            // Insert all the valid complaints into the database
            if (complaints.Count > 0)
            {
                using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await connection.OpenAsync();
                    foreach (var complaint in complaints)
                    {
                        // Insert each complaint into the database
                        var insertQuery = "INSERT INTO meter_complaint (meter_msn, open_description, opened_by, opened_at, last_comm_time,project_id) VALUES ( @MSN, @Open_Description, @OpenedBy, @OpenedAt, @lastCommunicationTime,@Project)";
                        using (var command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@MSN", complaint.MSN);
                            //command.Parameters.AddWithValue("@Reference_No", complaint.Reference_No);
                            command.Parameters.AddWithValue("@LastCommunicationTime", complaint.lastCommunicationTime.HasValue ? (object)complaint.lastCommunicationTime.Value : DBNull.Value);
                            //command.Parameters.AddWithValue("@SubDiv", complaint.SubDiv);
                            command.Parameters.AddWithValue("@Open_Description", complaint.Open_Description);
                            command.Parameters.AddWithValue("@OpenedAt", complaint.OpenedAt);
                            command.Parameters.AddWithValue("@OpenedBy", complaint.OpenedBy);
                            command.Parameters.AddWithValue("@Project", complaint.Project);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            return RedirectToAction("ViewComplaint");
        }

        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "ComplaintTemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "ComplaintTemplate.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        public IActionResult DownloadBulkCloseTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "BulkCloseComplaintTemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "BulkCloseComplaintTemplate.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        public async Task<IActionResult> GetComplaintFilteredSuggestions(string searchTerm, string filterType)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
                return Json(new { suggestions = new List<string>() });

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "";

                switch (filterType)
                {
                    case "msn":
                        query = "SELECT DISTINCT meter_msn FROM meter_complaint WHERE meter_msn LIKE @SearchTerm LIMIT 10";
                        break;

                    case "openDescription":
                        query = "SELECT DISTINCT open_description FROM meter_complaint WHERE open_description LIKE @SearchTerm LIMIT 10";
                        break;

                    case "issueDescription":
                        query = @"
                SELECT DISTINCT 
                    CASE 
                        WHEN d.description IS NOT NULL THEN d.description 
                        ELSE c.executed_description 
                    END AS suggestion 
                FROM meter_complaint c
                LEFT JOIN meter_description d ON c.executed_description = d.id
                WHERE (d.description LIKE @SearchTerm OR c.executed_description LIKE @SearchTerm) 
                LIMIT 10";
                        break;

                    case "assignedTo":
                        query = "SELECT DISTINCT username FROM user WHERE user_role = 14 AND username LIKE @SearchTerm LIMIT 10";
                        break;

                    case "status":
                        query = @"
                WITH StatusList AS (
                    SELECT 0 AS status, 'Open' AS Text
                    UNION ALL SELECT 1, 'Executed'
                    UNION ALL SELECT 2, 'Reinstalled/Replaced'
                    UNION ALL SELECT 3, 'Close'
                    UNION ALL SELECT 4, 'Mute'
                    UNION ALL SELECT 5, 'To be check'
                    UNION ALL SELECT 6, 'To be Removed'
                    UNION ALL SELECT 7, 'Removed from site'
                    UNION ALL SELECT 8, 'Returned to Subdivision'
                    UNION ALL SELECT 9, 'Not Under Warranty'
                    UNION ALL SELECT 10, 'With Sub-Division'
UNION ALL SELECT 11, 'Under Warranty'



                )
                SELECT sl.Text 
                FROM StatusList sl
                LEFT JOIN (SELECT DISTINCT status FROM meter_complaint) mc
                ON sl.status = mc.status
                WHERE sl.Text LIKE @SearchTerm
                ORDER BY sl.status ASC
                LIMIT 10";
                        break;

                    case "isFaulty":
                        query = "SELECT DISTINCT CASE WHEN is_faulty = 'Yes' THEN '1' ELSE '0' END FROM meter_complaint WHERE is_faulty  LIKE @SearchTerm LIMIT 10";
                        break;
                }

                var suggestions = await db.QueryAsync<string>(query, new { SearchTerm = $"%{searchTerm}%" });
                return Json(new { suggestions });
            }
        }

        [AuthorizeUserEx]
        public async Task<IActionResult> ViewComplaint(string? frefno, string? fmsn, string? fsdn, string? fat, string? fopenDescription, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize)
        {
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
            ViewBag.SignedInUserId = userId.Value;
            //ViewBag.status=
            //using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //{
            //    string sqlQuery = "SELECT id FROM user WHERE user_role = @UserRole";
            //    var parameters = new { UserRole = 1 };
            //    ViewBag.AllowedUserIds = db.Query<int>(sqlQuery, parameters).ToList();
            //}
            ViewBag.AllowedUserIds = new[] { "12" };
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                //page = page ?? 1;
                //var pageSize = 25;
                //var start = (page - 1) * pageSize;
                fpageSize = fpageSize ?? 50; // Default to 50 if not provided
                fpage = fpage ?? 1;
                var start = (fpage - 1) * fpageSize;
                // Removing leading commas from filter values
                //frefno = frefno?.TrimStart(',');
                frefno = frefno?.Trim();
                //fcustomerId = fcustomerId?.TrimStart(',');
                fat = fat?.TrimStart(',');
                fsdn = fsdn?.TrimStart(',');
                fissueDescription = fissueDescription?.Trim();
                fstatus = fstatus?.TrimStart(',');
                fisFaulty = fisFaulty?.TrimStart(',');
                fopenDescription = fopenDescription?.TrimStart(',');

                // Constructing SQL conditions
                string frefnoCondition = !string.IsNullOrEmpty(frefno)
     ? $"c.ref_number IN ({string.Join(",", frefno.Split(',').Select(x => $"'{x.Trim().Replace("'", "''")}'"))})"
     : "1=1";

             
                string openDescriptionCondition = !string.IsNullOrEmpty(fopenDescription)
                ? $"c.open_description IN ({string.Join(",", fopenDescription.Split(',').Select(x => $"'{x.Trim()}'"))})"
                : "1=1";
                string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"c.meter_msn IN ({fmsn})" : "1=1";
                string fatCondition = !string.IsNullOrEmpty(fat) ? $"c.assigned_to IN ({fat})" : "1=1";
                string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"c.sub_div IN ({fsdn})" : "1=1";
                //string customerIdCondition = !string.IsNullOrEmpty(fcustomerId) ? $"i.old_customer_id IN ({fcustomerId})" : "1=1";
                string issueDescriptionCondition = !string.IsNullOrEmpty(fissueDescription) ? $"c.executed_description LIKE '%{fissueDescription}%'" : "1=1";
                
                string statusCondition = !string.IsNullOrEmpty(fstatus) ? $"c.status IN ({fstatus})" : "1=1";
                string isFaultyCondition = !string.IsNullOrEmpty(fisFaulty) ? $"c.is_faulty IN ({fisFaulty})" : "1=1";
                string createdDateCondition = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                string closeDateCondition = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                // Date range parsing
                string executedDateCondition = "1=1";
                if (!string.IsNullOrEmpty(fexecutedDateRange))
                {
                    var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                    if (dates.Count == 2)
                    {
                        var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
                        var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

                        executedDateCondition = $@"
                        (
                            (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
                            (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
                            (c.status = 3 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
                            (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
                            (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
                            (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
                            (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
                            (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
                            (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') OR
 (c.status = 11 AND c.under_warranty_date >= '{startDate}' AND c.under_warranty_date <= '{endDate}') 
                        )";
                    }
                }
                // Fetch data for dropdowns
                string ReferenceNoQuery = "SELECT DISTINCT ref_number AS value, ref_number AS text FROM meter_complaint";
                var ReferenceNoData = await db.QueryAsync<DTODropdown>(ReferenceNoQuery);
                ViewBag.SelectReferenceNo = new SelectList(ReferenceNoData, "Value", "Text");


                //string CustomerIdQuery = "SELECT DISTINCT i.old_customer_id AS Value, i.old_customer_id AS Text FROM installations i LEFT JOIN meter_complaint c  ON c.ref_number = i.old_ref_no LIMIT 50";
                //var CustomerIdData = await db.QueryAsync<DTODropdown>(CustomerIdQuery);
                //ViewBag.SelectCustomerIdData = new SelectList(CustomerIdData.Distinct(), "Value", "Text");

                string msnQuery = "SELECT DISTINCT meter_msn AS value, meter_msn AS text FROM meter_complaint";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");

                string assignedToQuery = "SELECT DISTINCT Id AS Value, username AS Text FROM user WHERE user_role = 14 AND Id NOT IN (413, 542)";
                var assignedToData = await db.QueryAsync<DTODropdown>(assignedToQuery);
                ViewBag.SelectAssignedTo = new SelectList(assignedToData.Distinct(), "Value", "Text");

                string subDivisionQuery = @"
    SELECT 
        sub_div AS value, 
        CONCAT(sub_div, '-', subdiv_name) AS text 
    FROM 
        meter_complaint 
    GROUP BY 
        sub_div, subdiv_name 
    ORDER BY 
        sub_div ASC";

                var subDivisionData = await db.QueryAsync<DTODropdown>(subDivisionQuery);
                ViewBag.SelectSubDivision = new SelectList(subDivisionData.Distinct(), "Value", "Text");

                string descriptionQuery = @"
                SELECT DISTINCT
                    CASE
                        WHEN d.description IS NOT NULL THEN d.id
                        ELSE c.executed_description
                    END AS Value,
                    CASE
                        WHEN d.description IS NOT NULL THEN d.description
                        ELSE c.executed_description
                    END AS Text
                FROM
                    meter_complaint c
                LEFT JOIN
                    meter_description d ON c.executed_description = d.id";

                var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
                ViewBag.SelectdescriptionData = new SelectList(descriptionData.Distinct(), "Value", "Text");

               
                string statusQuery = @"
                WITH StatusList AS (
                    SELECT 0 AS status, 'Open' AS Text
                    UNION ALL SELECT 1, 'Executed'
                    UNION ALL SELECT 2, 'Reinstalled/Replaced'
                    UNION ALL SELECT 3, 'Close'
                    UNION ALL SELECT 4, 'Mute'
                    UNION ALL SELECT 5, 'To be check'
                    UNION ALL SELECT 6, 'To be Removed'
                    UNION ALL SELECT 7, 'Removed from site'
                    UNION ALL SELECT 8, 'Returned to Subdivision'
                    UNION ALL SELECT 9, 'Not Under Warranty'
                    UNION ALL SELECT 10, 'With Sub-Division'
UNION ALL SELECT 11, 'Under Warranty'
                )
                SELECT 
                    sl.status AS Value,
                    sl.Text
                FROM 
                    StatusList sl
                LEFT JOIN 
                    (SELECT DISTINCT status FROM meter_complaint) mc
                ON 
                    sl.status = mc.status
                ORDER BY 
                    sl.status ASC";
                var statusData = await db.QueryAsync<DTODropdown>(statusQuery);
                ViewBag.SelectStatus = new SelectList(statusData.Distinct(), "Value", "Text");

                string isFaultyQuery = "SELECT DISTINCT is_faulty as Value, CASE WHEN is_faulty = 1 THEN 'Yes' ELSE 'No' END as Text FROM meter_complaint";
                //string isFaultyQuery = "SELECT DISTINCT is_faulty AS value, is_faulty AS text FROM meter_complaint ORDER BY is_faulty ASC";
                var isFaultyData = await db.QueryAsync<DTODropdown>(isFaultyQuery);
                ViewBag.SelectIsFaulty = new SelectList(isFaultyData.Distinct(), "Value", "Text");

                var frefnoA = string.IsNullOrEmpty(frefno) ? "1" : frefno;
                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                var openDescriptionA = string.IsNullOrEmpty(fopenDescription) ? "1" : fopenDescription;
                var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                //var customerIdA = string.IsNullOrEmpty(fcustomerId) ? "1" : fcustomerId;
                var statusA = string.IsNullOrEmpty(fstatus) ? "1" : fstatus;
                var isFaultyA = string.IsNullOrEmpty(fisFaulty) ? "1" : fisFaulty;
                var createdDateA = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var closeDateA = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var issueDescriptionA = string.IsNullOrEmpty(fissueDescription) ? "1" : fissueDescription;
                string executedDateA = "1"; // Default to "1" which is a no-op condition for SQL

                // Parse the executedDateRange if it's not null or empty
                if (!string.IsNullOrEmpty(fexecutedDateRange))
                {
                    var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                    if (dates.Count == 2)
                    {
                        // Format the dates and construct the SQL condition
                        executedDateA = $"c.execution_date >= '{dates[0]:yyyy-MM-dd} 00:00:00' AND c.execution_date <= '{dates[1]:yyyy-MM-dd} 23:59:59'";
                    }
                }
                // Query for complaints
                string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                FROM
                    meter_complaint c
                LEFT JOIN 
                    user u1 ON c.opened_by = u1.Id
                LEFT JOIN 
                    user u2 ON c.closed_by = u2.Id
                LEFT JOIN 
                    user u3 ON c.assigned_to = u3.Id
                LEFT JOIN
                meter_description d ON c.executed_description = d.id 
                WHERE
 {frefnoCondition} AND
                    {fmsnCondition} AND

                    {openDescriptionCondition} AND
                    {fatCondition} AND
{fsdnCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {createdDateCondition} AND
                    {executedDateCondition} AND
                    {isFaultyCondition} AND
                    {closeDateCondition}
                ORDER BY 
                   c.is_priority DESC,       
                    c.opened_at DESC";

                //c.is_priority ASC,c.status ASC, c.opened_at DESC";
                var countQueryFinal = string.Format($"select count(*) from ({GeneralQuery})a", CountQuery, frefnoA, fmsnA, fsdnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateA, closeDateA, openDescriptionA);
                var totalRecords = await db.ExecuteScalarAsync<int>(countQueryFinal, new { frefno, fmsn, fsdn, fat, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fissueDescription, start, fpageSize, fcloseDate, fopenDescription });
                var complaintQueryFinal = string.Format(GeneralQuery + LimitQuery, SelectQuery , frefnoA, fmsnA, fsdnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateA, closeDateA, openDescriptionA);
                var complaintData = await db.QueryAsync<ComplaintViewModel>(complaintQueryFinal, new { frefno, fmsn, fsdn, fat, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fissueDescription, start, fpageSize, fcloseDate, fopenDescription });



                //var complaintData = await db.QueryAsync<ComplaintViewModel>(complaintQueryFinal);
                var totalPages = (int)Math.Ceiling(totalRecords / (double)fpageSize);
                // Preserve filter values
                ViewData["FRefNo"] = frefno;
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                ViewData["FSDN"] = fsdn;
                ViewData["IssueDescription"] = fissueDescription;
                ViewData["openDescription"] = fopenDescription;
                ViewData["Status"] = fstatus;
                ViewData["IsFaulty"] = fisFaulty;
                //ViewData["CustomerId"] = fcustomerId;
                ViewData["page"] = fpage.Value;
                ViewData["pageSize"] = fpageSize;
                ViewData["totalRecords"] = totalRecords;
                ViewData["totalPages"] = totalPages;
                ViewBag.CreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
                ViewBag.CloseDate = fcloseDate?.ToString("yyyy-MM-dd");
                ViewBag.ExecutedDateRange = fexecutedDateRange;
                ViewBag.PageSize = fpageSize;
                var permissionsJson = HttpContext.Session.GetString("UserPermissions");
                if (!string.IsNullOrEmpty(permissionsJson))
                {
                    var permissions = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(permissionsJson);
                    ViewBag.UserPermissions = permissions;
                }
                return View(complaintData);
            }
        }

        //public async Task<IActionResult> ViewComplaint(string? fmsn, string? fat, string? fopenDescription, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize)
        //{
        //    var userId = HttpContext.Session.GetInt32("UserId");

        //    if (!userId.HasValue)
        //    {
        //        string unauthorizedMessage = "User is not authenticated. Redirecting to login page...";
        //        string loginUrl = Url.Action("LoginUser", "Account"); // Replace with your login URL

        //        // Return an HTML response that includes the unauthorized message and JavaScript redirection
        //        string htmlContent = $@"
        //    <html>
        //        <body>
        //            <h3>{unauthorizedMessage}</h3>
        //            <script>
        //                setTimeout(function() {{
        //                    window.location.href = '{loginUrl}';
        //                }}, 1000); // Redirects after 1 seconds
        //            </script>
        //        </body>
        //    </html>";

        //        return Content(htmlContent, "text/html");
        //    }
        //    ViewBag.SignedInUserId = userId.Value;
        //    //ViewBag.status=
        //    //using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    //{
        //    //    string sqlQuery = "SELECT id FROM user WHERE user_role = @UserRole";
        //    //    var parameters = new { UserRole = 1 };
        //    //    ViewBag.AllowedUserIds = db.Query<int>(sqlQuery, parameters).ToList();
        //    //}
        //    ViewBag.AllowedUserIds = new[] { "1" };
        //    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        //    {
        //        //page = page ?? 1;
        //        //var pageSize = 25;
        //        //var start = (page - 1) * pageSize;
        //        fpageSize = fpageSize ?? 50; // Default to 50 if not provided
        //        fpage = fpage ?? 1;
        //        var start = (fpage - 1) * fpageSize;
        //        // Removing leading commas from filter values
        //        //frefno = frefno?.TrimStart(',');
        //        //frefno = frefno?.Length > 0 ? string.Join(",", frefno.Split(',').Select(x => x.Substring(0, x.Length - 1))) : frefno;
        //        fmsn = fmsn?.TrimStart(',');
        //        //fcustomerId = fcustomerId?.TrimStart(',');
        //        fat = fat?.TrimStart(',');
        //        //fsdn = fsdn?.TrimStart(',');
        //        fissueDescription = fissueDescription?.Trim();
        //        fstatus = fstatus?.TrimStart(',');
        //        fisFaulty = fisFaulty?.TrimStart(',');
        //        fopenDescription = fopenDescription?.TrimStart(',');

        //        // Constructing SQL conditions
        //        //string frefnoCondition = !string.IsNullOrEmpty(frefno) ? $"c.ref_number IN ({frefno})" : "1=1";
        //        string openDescriptionCondition = !string.IsNullOrEmpty(fopenDescription)
        //        ? $"c.open_description IN ({string.Join(",", fopenDescription.Split(',').Select(x => $"'{x.Trim()}'"))})"
        //        : "1=1";
        //        string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"c.meter_msn IN ({fmsn})" : "1=1";
        //        string fatCondition = !string.IsNullOrEmpty(fat) ? $"c.assigned_to IN ({fat})" : "1=1";
        //        //string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"c.sub_div IN ({fsdn})" : "1=1";
        //        //string customerIdCondition = !string.IsNullOrEmpty(fcustomerId) ? $"i.old_customer_id IN ({fcustomerId})" : "1=1";
        //        string issueDescriptionCondition = !string.IsNullOrEmpty(fissueDescription) ? $"c.executed_description LIKE '%{fissueDescription}%'" : "1=1";
        //        //string issueDescriptionCondition;
        //        //if (!string.IsNullOrEmpty(issueDescription))
        //        //{
        //        //    // If issueDescription is not empty, apply filter on executed_description
        //        //    // Use SQL LIKE syntax directly for filtering
        //        //    issueDescriptionCondition =
        //        //        "(d.description IS NOT NULL AND d.description LIKE '%' + @issueDescription + '%') OR " +
        //        //        "(d.description IS NULL AND c.executed_description LIKE '%' + @issueDescription + '%')";
        //        //}
        //        //else
        //        //{
        //        //    // If issueDescription is empty, skip filtering
        //        //    issueDescriptionCondition = "1=1";
        //        //}
        //        string statusCondition = !string.IsNullOrEmpty(fstatus) ? $"c.status IN ({fstatus})" : "1=1";
        //        string isFaultyCondition = !string.IsNullOrEmpty(fisFaulty) ? $"c.is_faulty IN ({fisFaulty})" : "1=1";
        //        string createdDateCondition = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
        //        string closeDateCondition = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
        //        // Date range parsing
        //        string executedDateCondition = "1=1";
        //        if (!string.IsNullOrEmpty(fexecutedDateRange))
        //        {
        //            var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
        //            if (dates.Count == 2)
        //            {
        //                var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
        //                var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

        //                executedDateCondition = $@"
        //                (
        //                    (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
        //                    (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
        //                    (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
        //                    (c.status = 3 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
        //                    (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
        //                    (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
        //                    (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
        //                    (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
        //                    (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
        //                    (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
        //                    (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') 
        //                )";
        //            }
        //        }
        //        // Fetch data for dropdowns
        //        //string ReferenceNoQuery = "SELECT DISTINCT ref_number AS value, ref_number AS text FROM meter_complaint";
        //        //var ReferenceNoData = await db.QueryAsync<DTODropdown>(ReferenceNoQuery);
        //        //ViewBag.SelectReferenceNo = new SelectList(ReferenceNoData.Distinct(), "Value", "Text");

        //        //string CustomerIdQuery = "SELECT DISTINCT i.old_customer_id AS Value, i.old_customer_id AS Text FROM installations i LEFT JOIN meter_complaint c  ON c.ref_number = i.old_ref_no LIMIT 50";
        //        //var CustomerIdData = await db.QueryAsync<DTODropdown>(CustomerIdQuery);
        //        //ViewBag.SelectCustomerIdData = new SelectList(CustomerIdData.Distinct(), "Value", "Text");

        //        //string msnQuery = "SELECT DISTINCT meter_msn AS value, meter_msn AS text FROM meter_complaint";
        //        //var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
        //        //ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");

        //        //string assignedToQuery = "SELECT DISTINCT Id AS Value, username AS Text FROM user WHERE user_role = 14";
        //        //var assignedToData = await db.QueryAsync<DTODropdown>(assignedToQuery);
        //        //ViewBag.SelectAssignedTo = new SelectList(assignedToData.Distinct(), "Value", "Text");

        //        //string subDivisionQuery = "SELECT distinct sub_div_code as value,concat(sub_div_code,'-',name) as text FROM survey_hesco_subdivision group by name ORDER BY sub_div_code ASC";
        //        //var subDivisionData = await db.QueryAsync<DTODropdown>(subDivisionQuery);
        //        //ViewBag.SelectSubDivision = new SelectList(subDivisionData.Distinct(), "Value", "Text");

        //        //string descriptionQuery = @"
        //        //SELECT DISTINCT
        //        //    CASE
        //        //        WHEN d.description IS NOT NULL THEN d.id
        //        //        ELSE c.executed_description
        //        //    END AS Value,
        //        //    CASE
        //        //        WHEN d.description IS NOT NULL THEN d.description
        //        //        ELSE c.executed_description
        //        //    END AS Text
        //        //FROM
        //        //    meter_complaint c
        //        //LEFT JOIN
        //        //    meter_description d ON c.executed_description = d.id";

        //        //var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
        //        //ViewBag.SelectdescriptionData = new SelectList(descriptionData.Distinct(), "Value", "Text");

        //        /*string statusQuery = @"
        //        SELECT DISTINCT 
        //            status AS Value, 
        //            CASE 
        //                WHEN status = 0 THEN 'Open' 
        //                WHEN status = 1 THEN 'Executed' 
        //                WHEN status = 2 THEN 'Reinstalled/Replaced'
        //                WHEN status = 3 THEN 'Close'
        //                WHEN status = 4 THEN 'Mute'
        //                WHEN status = 5 THEN 'To be check'
        //                WHEN status = 6 THEN 'To be Removed'
        //                WHEN status = 7 THEN 'Removed from site'
        //                WHEN status = 8 THEN 'Returned to Subdivision'
        //                WHEN status = 9 THEN 'Not Under Warranty'
        //                ELSE 'With Sub-Division' 
        //            END AS Text 
        //        FROM 
        //            meter_complaint 
        //        ORDER BY 
        //            status ASC";*/
        //        //string statusQuery = @"
        //        //WITH StatusList AS (
        //        //    SELECT 0 AS status, 'Open' AS Text
        //        //    UNION ALL SELECT 1, 'Executed'
        //        //    UNION ALL SELECT 2, 'Reinstalled/Replaced'
        //        //    UNION ALL SELECT 3, 'Close'
        //        //    UNION ALL SELECT 4, 'Mute'
        //        //    UNION ALL SELECT 5, 'To be check'
        //        //    UNION ALL SELECT 6, 'To be Removed'
        //        //    UNION ALL SELECT 7, 'Removed from site'
        //        //    UNION ALL SELECT 8, 'Returned to Subdivision'
        //        //    UNION ALL SELECT 9, 'Not Under Warranty'
        //        //    UNION ALL SELECT 10, 'With Sub-Division'
        //        //)
        //        //SELECT 
        //        //    sl.status AS Value,
        //        //    sl.Text
        //        //FROM 
        //        //    StatusList sl
        //        //LEFT JOIN 
        //        //    (SELECT DISTINCT status FROM meter_complaint) mc
        //        //ON 
        //        //    sl.status = mc.status
        //        //ORDER BY 
        //        //    sl.status ASC";
        //        //var statusData = await db.QueryAsync<DTODropdown>(statusQuery);
        //        //ViewBag.SelectStatus = new SelectList(statusData.Distinct(), "Value", "Text");

        //        //string isFaultyQuery = "SELECT DISTINCT is_faulty as Value, CASE WHEN is_faulty = 1 THEN 'Yes' ELSE 'No' END as Text FROM meter_complaint";
        //        ////string isFaultyQuery = "SELECT DISTINCT is_faulty AS value, is_faulty AS text FROM meter_complaint ORDER BY is_faulty ASC";
        //        //var isFaultyData = await db.QueryAsync<DTODropdown>(isFaultyQuery);
        //        //ViewBag.SelectIsFaulty = new SelectList(isFaultyData.Distinct(), "Value", "Text");

        //        //var frefnoA = string.IsNullOrEmpty(frefno) ? "1" : frefno;
        //        var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
        //        var openDescriptionA = string.IsNullOrEmpty(fopenDescription) ? "1" : fopenDescription;
        //        //var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
        //        var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
        //        //var customerIdA = string.IsNullOrEmpty(fcustomerId) ? "1" : fcustomerId;
        //        var statusA = string.IsNullOrEmpty(fstatus) ? "1" : fstatus;
        //        var isFaultyA = string.IsNullOrEmpty(fisFaulty) ? "1" : fisFaulty;
        //        var createdDateA = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
        //        var closeDateA = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
        //        var issueDescriptionA = string.IsNullOrEmpty(fissueDescription) ? "1" : fissueDescription;
        //        string executedDateA = "1"; // Default to "1" which is a no-op condition for SQL

        //        // Parse the executedDateRange if it's not null or empty
        //        if (!string.IsNullOrEmpty(fexecutedDateRange))
        //        {
        //            var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
        //            if (dates.Count == 2)
        //            {
        //                // Format the dates and construct the SQL condition
        //                executedDateA = $"c.execution_date >= '{dates[0]:yyyy-MM-dd} 00:00:00' AND c.execution_date <= '{dates[1]:yyyy-MM-dd} 23:59:59'";
        //            }
        //        }
        //        // Query for complaints
        //        string GeneralQuery = $@"
        //        SELECT
        //            {SelectQuery}
        //        FROM
        //            meter_complaint c
        //        LEFT JOIN 
        //            user u1 ON c.opened_by = u1.Id
        //        LEFT JOIN 
        //            user u2 ON c.closed_by = u2.Id
        //        LEFT JOIN 
        //            user u3 ON c.assigned_to = u3.Id
        //        LEFT JOIN
        //        meter_description d ON c.executed_description = d.id 
        //        WHERE
        //            {fmsnCondition} AND
        //            {openDescriptionCondition} AND                    
        //            {fatCondition} AND
        //            {issueDescriptionCondition} AND
        //            {statusCondition} AND
        //            {createdDateCondition} AND
        //            {executedDateCondition} AND
        //            {isFaultyCondition} AND
        //            {closeDateCondition}
        //        ORDER BY 
        //           c.is_priority DESC,       
        //            c.opened_at DESC";

        //        //c.is_priority ASC,c.status ASC, c.opened_at DESC";
        //        var countQueryFinal = string.Format($"select count(*) from ({GeneralQuery})a", CountQuery, fmsnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateA, closeDateA,openDescriptionA);
        //        var totalRecords = await db.ExecuteScalarAsync<int>(countQueryFinal, new { fmsn, fat, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fissueDescription, start, fpageSize, fcloseDate, fopenDescription });
        //        var complaintQueryFinal = string.Format(GeneralQuery + LimitQuery, SelectQuery, fmsnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateA, closeDateA,openDescriptionA);
        //        var complaintData = await db.QueryAsync<ComplaintViewModel>(complaintQueryFinal, new { fmsn, fat, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fissueDescription, start, fpageSize, fcloseDate, fopenDescription });



        //        //var complaintData = await db.QueryAsync<ComplaintViewModel>(complaintQueryFinal);
        //        var totalPages = (int)Math.Ceiling(totalRecords / (double)fpageSize);
        //        // Preserve filter values
        //        //ViewData["FRefNo"] = frefno;
        //        ViewData["FMSN"] = fmsn;
        //        ViewData["FAT"] = fat;
        //        //ViewData["FSDN"] = fsdn;
        //        ViewData["IssueDescription"] = fissueDescription;
        //        ViewData["openDescription"] = fopenDescription;
        //        ViewData["Status"] = fstatus;
        //        ViewData["IsFaulty"] = fisFaulty;
        //        //ViewData["CustomerId"] = fcustomerId;
        //        ViewData["page"] = fpage.Value;
        //        ViewData["pageSize"] = fpageSize;
        //        ViewData["totalRecords"] = totalRecords;
        //        ViewData["totalPages"] = totalPages;
        //        ViewBag.CreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
        //        ViewBag.CloseDate = fcloseDate?.ToString("yyyy-MM-dd");
        //        ViewBag.ExecutedDateRange = fexecutedDateRange;
        //        ViewBag.PageSize = fpageSize;
        //        return View(complaintData);
        //    }
        //}


        public async Task<IActionResult> ExportToPdf(string? frefno, string? fmsn, string? fsdn, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize)
        {
            // Constructing SQL conditions
            string frefnoCondition = !string.IsNullOrEmpty(frefno) ? $"c.ref_number IN ({frefno})" : "1=1";
            string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"c.meter_msn IN ({fmsn})" : "1=1";
            string fatCondition = !string.IsNullOrEmpty(fat) ? $"c.assigned_to IN ({fat})" : "1=1";
            string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"c.sub_div IN ({fsdn})" : "1=1";
            //string customerIdCondition = !string.IsNullOrEmpty(fcustomerId) ? $"i.old_customer_id IN ({fcustomerId})" : "1=1";
            string issueDescriptionCondition = !string.IsNullOrEmpty(fissueDescription) ? $"c.executed_description LIKE '%{fissueDescription}%'" : "1=1";
            string statusCondition = !string.IsNullOrEmpty(fstatus) ? $"c.status IN ({fstatus})" : "1=1";
            string isFaultyCondition = !string.IsNullOrEmpty(fisFaulty) ? $"c.is_faulty IN ({fisFaulty})" : "1=1";
            string createdDateCondition = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            string closeDateCondition = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            // Date range parsing
            string executedDateCondition = "1=1";
            if (!string.IsNullOrEmpty(fexecutedDateRange))
            {
                var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                if (dates.Count == 2)
                {
                    var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
                    var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

                    executedDateCondition = $@"
                        (
                            (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
                            (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
                            (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
                            (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
                            (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
                            (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
                            (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
                            (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
                            (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') OR
(c.status = 11 AND c.under_warranty_date >= '{startDate}' AND c.under_warranty_date <= '{endDate}') 
                        )";
                }
            }
            string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                FROM
                    meter_complaint c
                LEFT JOIN 
                    user u1 ON c.opened_by = u1.Id
                LEFT JOIN 
                    user u2 ON c.closed_by = u2.Id
                LEFT JOIN 
                    user u3 ON c.assigned_to = u3.Id
                LEFT JOIN
                meter_description d ON c.executed_description = d.id
                WHERE
{frefnoCondition} AND
                    {fmsnCondition} AND
                    {fatCondition} AND
{fsdnCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {createdDateCondition} AND
                    {executedDateCondition} AND
                    {isFaultyCondition} AND
                    {closeDateCondition}
                ORDER BY 
                    c.status ASC, c.opened_at DESC";
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var frefnoA = string.IsNullOrEmpty(frefno) ? "1" : frefno;
                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                //var customerIdA = string.IsNullOrEmpty(fcustomerId) ? "1" : fcustomerId;
                var statusA = string.IsNullOrEmpty(fstatus) ? "1" : fstatus;
                var isFaultyA = string.IsNullOrEmpty(fisFaulty) ? "1" : fisFaulty;
                var createdDateA = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var closeDateA = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                // Default value for executedDateConditionA
                string executedDateConditionA = "1=1";
                if (!string.IsNullOrEmpty(fexecutedDateRange))
                {
                    var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                    if (dates.Count == 2)
                    {
                        var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
                        var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

                        executedDateConditionA = $@"
                        (
                            (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
                            (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
                            (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
                            (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
                            (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
                            (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
                            (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
                            (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
                            (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') OR
(c.status = 11 AND c.under_warranty_date >= '{startDate}' AND c.under_warranty_date <= '{endDate}') 
 
                        )";
                    }
                }
                //string executedDateConditionA = "1"; // Default to "1" which is a no-op condition for SQL

                //// Parse the executedDateRange if it's not null or empty
                //if (!string.IsNullOrEmpty(executedDateRange))
                //{
                //    var dates = executedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                //    if (dates.Count == 2)
                //    {
                //        // Format the dates and construct the SQL condition
                //        executedDateConditionA = $"c.execution_date >= '{dates[0]:yyyy-MM-dd} 00:00:00' AND c.execution_date <= '{dates[1]:yyyy-MM-dd} 23:59:59'";
                //    }
                //}
                //var executedDateA = executedDate.HasValue ? $"c.execution_date >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND c.execution_date <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var issueDescriptionA = string.IsNullOrEmpty(fissueDescription) ? "1" : fissueDescription;
                var ComplaintQueryFinal = string.Format(GeneralQuery, frefnoA, fmsnA, fsdnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateConditionA, closeDateA);
                var ComplaintData = await db.QueryAsync<ComplaintViewModel>(ComplaintQueryFinal, new { frefno,fmsn, fsdn, fat, fissueDescription, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fpageSize, fcloseDate });


                var htmlContent = await _pdfService.RenderViewAsStringAsync(this.ControllerContext, "ComplaintPDF", ComplaintData);
                var pdf = _pdfService.GeneratePdf(htmlContent);

                return File(pdf, "application/pdf", "ComplaintList.pdf");
            }
        }
        public async Task<IActionResult> ExportToExcel(string? frefno, string? fmsn, string? fsdn, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize)
        {
            // Constructing SQL conditions
            string frefnoCondition = !string.IsNullOrEmpty(frefno) ? $"c.ref_number IN ({frefno})" : "1=1";
            string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"c.meter_msn IN ({fmsn})" : "1=1";
            string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"c.sub_div IN ({fsdn})" : "1=1";
            string fatCondition = !string.IsNullOrEmpty(fat) ? $"c.assigned_to IN ({fat})" : "1=1";
           
            //string customerIdCondition = !string.IsNullOrEmpty(fcustomerId) ? $"i.old_customer_id IN ({fcustomerId})" : "1=1";
            string issueDescriptionCondition = !string.IsNullOrEmpty(fissueDescription) ? $"c.executed_description LIKE '%{fissueDescription}%'" : "1=1";
            string statusCondition = !string.IsNullOrEmpty(fstatus) ? $"c.status IN ({fstatus})" : "1=1";
            string isFaultyCondition = !string.IsNullOrEmpty(fisFaulty) ? $"c.is_faulty IN ({fisFaulty})" : "1=1";
            string createdDateCondition = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            string closeDateCondition = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            // Date range parsing
            string executedDateCondition = "1=1";
            if (!string.IsNullOrEmpty(fexecutedDateRange))
            {
                var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                if (dates.Count == 2)
                {
                    var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
                    var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

                    executedDateCondition = $@"
                        (
                            (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
                            (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
                            (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
                            (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
                            (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
                            (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
                            (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
                            (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
                            (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') OR
 (c.status = 11 AND c.under_warranty_date >= '{startDate}' AND c.under_warranty_date <= '{endDate}') 
                    )";
                }
            }
            string GeneralQuery = $@"
                SELECT
                    {SelectQuery}
                FROM
                    meter_complaint c               
               LEFT JOIN 
                    user u1 ON c.opened_by = u1.Id
                LEFT JOIN 
                    user u2 ON c.closed_by = u2.Id
                LEFT JOIN 
                    user u3 ON c.assigned_to = u3.Id
                LEFT JOIN
                meter_description d ON c.executed_description = d.id
                WHERE
{frefnoCondition} AND
                    {fmsnCondition} AND
                    {fatCondition} AND
{fsdnCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {createdDateCondition} AND
                    {executedDateCondition} AND
                    {isFaultyCondition} AND
                    {closeDateCondition}
                ORDER BY 
                    c.status ASC, c.opened_at DESC";
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var frefnoA = string.IsNullOrEmpty(frefno) ? "1" : frefno;
                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                var statusA = string.IsNullOrEmpty(fstatus) ? "1" : fstatus;
                //var customerIdA = string.IsNullOrEmpty(fcustomerId) ? "1" : fcustomerId;
                var isFaultyA = string.IsNullOrEmpty(fisFaulty) ? "1" : fisFaulty;
                var createdDateA = fcreatedDate.HasValue ? $"c.opened_at >= '{fcreatedDate.Value:yyyy-MM-dd} 00:00:00' AND c.opened_at <= '{fcreatedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var closeDateA = fcloseDate.HasValue ? $"c.closed_at >= '{fcloseDate.Value:yyyy-MM-dd} 00:00:00' AND c.closed_at <= '{fcloseDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                // Default value for executedDateConditionA
                string executedDateConditionA = "1=1";
                if (!string.IsNullOrEmpty(fexecutedDateRange))
                {
                    var dates = fexecutedDateRange.Split('-').Select(d => DateTime.Parse(d.Trim())).ToList();
                    if (dates.Count == 2)
                    {
                        var startDate = dates[0].ToString("yyyy-MM-dd 00:00:00");
                        var endDate = dates[1].ToString("yyyy-MM-dd 23:59:59");

                        executedDateConditionA = $@"
                        (
                            (c.status = 0 AND c.opened_at >= '{startDate}' AND c.opened_at <= '{endDate}') OR
                            (c.status = 1 AND c.execution_date >= '{startDate}' AND c.execution_date <= '{endDate}') OR
                            (c.status = 2 AND c.replaced_date >= '{startDate}' AND c.replaced_date <= '{endDate}') OR
                            (c.status = 4 AND c.mute_date >= '{startDate}' AND c.mute_date <= '{endDate}') OR
                            (c.status = 5 AND c.to_be_check_date >= '{startDate}' AND c.to_be_check_date <= '{endDate}') OR
                            (c.status = 6 AND c.toberemoved_date >= '{startDate}' AND c.toberemoved_date <= '{endDate}') OR
                            (c.status = 7 AND c.removed_date >= '{startDate}' AND c.removed_date <= '{endDate}') OR
                            (c.status = 8 AND c.returned_to_subdiv_date >= '{startDate}' AND c.returned_to_subdiv_date <= '{endDate}') OR
                            (c.status = 9 AND c.not_under_warranty_date >= '{startDate}' AND c.not_under_warranty_date <= '{endDate}') OR
                            (c.status = 10 AND c.with_subdiv_date >= '{startDate}' AND c.with_subdiv_date <= '{endDate}') OR
(c.status = 11 AND c.under_warranty_date >= '{startDate}' AND c.under_warranty_date <= '{endDate}') 
                        )";
                    }
                }
                var issueDescriptionA = string.IsNullOrEmpty(fissueDescription) ? "1" : fissueDescription;
                var ComplaintQueryFinal = string.Format(GeneralQuery, frefnoA, fmsnA, fsdnA, fatA, issueDescriptionA, statusA, isFaultyA, createdDateA, executedDateConditionA, closeDateA);
                //var ComplaintData = await db.QueryAsync<ComplaintViewModel>(ComplaintQueryFinal, new { frefno, fmsn, fat, fsdn, issueDescription, status, isFaulty, createdDate, executedDateRange, pageSize , closeDate });
                var ComplaintData = await db.QueryAsync<ComplaintViewModel>(ComplaintQueryFinal, new { frefno, fmsn, fsdn, fat, fissueDescription, fstatus, fisFaulty, fcreatedDate, fexecutedDateRange, fpageSize, fcloseDate });
                var fileDownloadName = "ComplaintList.xlsx";

                // Define a custom folder to save the file (you can choose a folder on the D drive or any location accessible to your application)
                var downloadFolderPath = Path.Combine("D:", "MyAppDownloads");  // Or specify your own folder path

                // Ensure the folder exists
                if (!Directory.Exists(downloadFolderPath))
                {
                    Directory.CreateDirectory(downloadFolderPath);
                }
                using (var package = CreateExcelPackage(ComplaintData))
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


        private ExcelPackage CreateExcelPackage(IEnumerable<ComplaintViewModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            var package = new ExcelPackage();
            package.Workbook.Properties.Title = "COMPLAINT LIST";
            package.Workbook.Properties.Author = "";
            package.Workbook.Properties.Subject = "COMPLAINT LIST";

            var worksheet = package.Workbook.Worksheets.Add("COMPLAINT LIST");

            int row = 1;
            worksheet.Cells[row, 1].Value = $"COMPLAINT LIST                                          DATE: {DateTime.Now:dd-MM-yyyy}";
            worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells[row, 1, row, 5].Merge = true;

            row++;

            // Header row
            worksheet.Cells[row, 1].Value = "Reference No";
            worksheet.Cells[row, 2].Value = "MSN";
            worksheet.Cells[row, 3].Value = "SubDiv"; ;
            worksheet.Cells[row, 4].Value = "ISSUE DESCRIPTION"; ;
            worksheet.Cells[row, 5].Value = "STATUS";
            worksheet.Cells[row, 1, row, 5].Style.Font.Bold = true;
            row++;



            foreach (var item in data)
            {
                worksheet.Cells[row, 1].Value = item.Reference_No;
                worksheet.Cells[row, 2].Value = item.MSN;
                worksheet.Cells[row, 3].Value = item.SubDiv;
                worksheet.Cells[row, 4].Value = item.ExecutedDescriptionText;
                worksheet.Cells[row, 5].Value = item.StatusDisplay;

                // Apply borders around each row
                var borderCells = worksheet.Cells[row, 1, row, 5];
                borderCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                row++;
            }

            // Apply borders around the entire table
            var fullTable = worksheet.Cells[1, 1, row - 1, 5];
            fullTable.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

            // AutoFitColumns
            worksheet.Cells[1, 1, row, 5].AutoFitColumns();

            return package;
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> EditComplaintDetails(int id, string? fopenDescription, string? frefno, string? fmsn, string? fsdn, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize, string? old_imsi, string? new_imsi, string? ftel_old, string? ftel_new)
        {
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
            ViewBag.SignedInUserId = userId.Value;
            List<int> fieldStaffUsers = new List<int>();

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 14";
                fieldStaffUsers = (await db.QueryAsync<int>(query)).ToList();
                ViewBag.FieldStaffList = fieldStaffUsers;
            }


            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    // Fetch complaint details
                    string query = @"
                    SELECT 
                        c.Id AS id, 
 c.ref_number AS Reference_No, 
                        c.meter_msn AS MSN, 
 c.sub_div AS SubDiv,
c.subdiv_name AS SubDivName,
c.old_imsi As Old_Imsi,
c.new_imsi As New_Imsi,
t1.name AS Telco_Old,
    t2.name AS Telco_New,
                        c.close_description AS Closed_Description,
                        c.executed_description AS Executed_Description,
                        c.open_description AS Open_Description,
                        c.status,
                        c.is_faulty AS IsFaulty,                        
                        c.is_priority AS Priority,
                        c.assigned_to AS AssignedTo,
c.remarks
                    FROM 
                        meter_complaint c
LEFT JOIN telco t1 ON c.telco_old = t1.id
LEFT JOIN telco t2 ON c.telco_new = t2.id
                    WHERE 
                        c.Id = @id";

                    var complaint = await db.QuerySingleOrDefaultAsync<ComplaintEditViewModel>(query, new { Id = id });

                    if (complaint == null)
                    {
                        return NotFound();
                    }

                    // Populate AssignedTo dropdown
                    string assignToQuery = "SELECT Id as value, username as text FROM user WHERE user_role = 14 AND Id != 413";
                    var assignToData = await db.QueryAsync<DTODropdown>(assignToQuery);
                    ViewBag.AssignToList = new SelectList(assignToData.Distinct(), "Value", "Text", complaint.AssignedTo);
                    // Populate Executed Description dropdown
                    string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
                    var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
                    ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");
                    // Preserve filter values
                    ViewData["FRefNo"] = frefno;
                    ViewData["FMSN"] = fmsn;
                    ViewData["FAT"] = fat;
                    ViewData["FSDN"] = fsdn;
                    ViewData["IssueDescription"] = fissueDescription;
                    ViewData["Status"] = fstatus;
                    ViewData["IsFaulty"] = fisFaulty;
                    ViewData["page"] = fpage.Value;
                    //ViewData["CustomerId"] = fcustomerId;
                    ViewData["pageSize"] = fpageSize;
                    ViewBag.CreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
                    ViewBag.CloseDate = fcloseDate?.ToString("yyyy-MM-dd");
                    ViewBag.ExecutedDateRange = fexecutedDateRange;
                    ViewData["openDescription"] = fopenDescription;
                    return View(complaint);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditComplaintDetails(ComplaintEditViewModel model, List<IFormFile> ComplaintImages, string? OtherExecutedDescription, string? fopenDescription, string? fmsn, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize, string? old_imsi, string? new_imsi)
        {
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

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT user_role FROM user WHERE id = @UserId";
                var userRole = await db.ExecuteScalarAsync<int>(query, new { UserId = userId.Value });

                if (userRole == 14)
                {
                    model.AssignedTo = userId.Value;
                }
            }
            // Initialize the base update query
            string updateQuery = @"
            UPDATE meter_complaint
            SET 
                status = @Status,
                is_faulty = @IsFaulty,
                open_description = @Open_Description,
                assigned_to = @AssignedTo,
                remarks = @Remarks,
                is_priority = @Priority";

            // Check the status and append necessary fields to the update query
            if (model.Status == 3) // Close status
            {
                updateQuery += @",
                close_description = @Closed_Description,
                closed_by = @ClosedBy,
                closed_at = @ClosedAt,
                is_priority =@Priority";

                model.ClosedAt = DateTime.Now;
                model.ClosedBy = userId.Value;
                model.Priority = 0;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Closed",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            // Check the status and append necessary fields to the update query
            if (model.Status == 5) // To be check status
            {
                updateQuery += @",
                to_be_check_date = @ToBeCheckDate,
                status =@Status";

                model.ToBeCheckDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "To be check",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 2)
            {
                updateQuery += @",
                replaced_date = @Replaced_Date,
                new_msn = @NewMSN,
                status = @Status";
                model.Replaced_Date = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Replaced/Reinstalled",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 1) // Executed status
            {
                // Combine multiple executed descriptions into a single string
                //model.Executed_Description = string.Join("; ", ExecutedDescriptions);
                if (OtherExecutedDescription != null)
                {
                    model.Executed_Description = OtherExecutedDescription;
                }
                model.Execution_Date = DateTime.Now;
                updateQuery += @",
                execution_date = @Execution_Date,
                executed_description = @Executed_Description,
            old_imsi = @Old_Imsi,
            new_imsi = @New_Imsi,
             telco_old = @Telco_Old,
             telco_new = @Telco_New";

                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Executed",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 6)
            {
                if (OtherExecutedDescription != null)
                {
                    model.Executed_Description = OtherExecutedDescription;
                }
                updateQuery += @",
                toberemoved_date = @TobeRemovedDate,
                executed_description = @Executed_Description,
                assigned_to = @AssignedTo,
                is_faulty=1,
                is_priority =1,
                status = @Status";
                model.TobeRemovedDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "To be Removed",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 7)
            {
                updateQuery += @",
                removed_date = @RemovedDate,
                assigned_to = @AssignedTo,
                is_faulty=1,
                is_priority =0,
                status = @Status";
                model.RemovedDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Removed from site",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 4)
            {
                updateQuery += @",
                mute_date = @MuteDate,                
                status = @Status";
                model.MuteDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Mute",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 8)
            {
                updateQuery += @",
                returned_to_subdiv_date = @ReturnedToSubdivDate, 
                status = @Status";
                model.ReturnedToSubdivDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Returned to Subdivision",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 9)
            {
                updateQuery += @",
                not_under_warranty_date = @NotUnderWarrantyDate, 
                status = @Status";
                model.NotUnderWarrantyDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Not Under Warranty",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 10)
            {
                updateQuery += @",
                with_subdiv_date = @WithSubdivDate,
                status = @Status";
                model.WithSubdivDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "With Sub-Division",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }
            else if (model.Status == 11)
            {
                updateQuery += @",
                under_warranty_date = @UnderWarrantyDate, 
                status = @Status";
                model.UnderWarrantyDate = DateTime.Now;
                string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
                logModel logModel = new()
                {
                    userId = userId.Value,
                    ActionDate = DateTime.Now,
                    Action = "Under Warranty",
                    complaintId = model.Id
                };
                if (ModelState.IsValid)
                {
                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                            {
                                command.Parameters.AddWithValue("@userId", logModel.userId);
                                command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                                command.Parameters.AddWithValue("@Action", logModel.Action);
                                command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                                connection.Open();
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return StatusCode(500, "Internal server error while processing complaint.");
                    }
                }
            }

            updateQuery += " WHERE id = @Id";

            // Validate uploaded files
            if (ComplaintImages != null && ComplaintImages.Count > 10)
            {
                ModelState.AddModelError("ComplaintImages", "You can only upload a maximum of 10 files.");
            }

            foreach (var file in ComplaintImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".png" && extension != ".jpeg" && extension != ".jpg" && extension != ".pdf" && extension != ".doc" && extension != ".docx" && extension != ".xls" && extension != ".xlsx" && extension != ".xml")
                {
                    ModelState.AddModelError("ComplaintImages", "Invalid file type. Only PNG, JPEG, JPG, PDF,EXCEL,WORD and XML are allowed.");
                }
            }

            // Ensure the model state is valid before proceeding
            if (ModelState.IsValid)
            {
                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(updateQuery, new
                        {
                            Status = model.Status,
                            IsFaulty = model.IsFaulty,
                            AssignedTo = model.AssignedTo,
                            Open_Description = model.Open_Description,
                            Closed_Description = model.Closed_Description,
                            Executed_Description = model.Executed_Description,
                            ClosedBy = model.ClosedBy,
                            Execution_Date = model.Execution_Date,
                            Replaced_Date = model.Replaced_Date,
                            NewMSN = model.NewMSN,
                            ClosedAt = model.ClosedAt,
                            Priority = model.Priority,
                            RemovedDate = model.RemovedDate,
                            ToBeCheckDate=model.ToBeCheckDate,
                            MuteDate = model.MuteDate,
                            TobeRemovedDate = model.TobeRemovedDate,
                            ReturnedToSubdivDate = model.ReturnedToSubdivDate,
                            NotUnderWarrantyDate = model.NotUnderWarrantyDate,
                            WithSubdivDate = model.WithSubdivDate,
                            Remarks = model.Remarks,
                            Id = model.Id,
                            Old_Imsi = model.Old_Imsi,
                            New_Imsi = model.New_Imsi,
                            Telco_Old = model.Telco_Old,
                           Telco_New = model.Telco_New,
                           UnderWarrantyDate = model.UnderWarrantyDate

                        });
                    }
                    if (!string.IsNullOrEmpty(model.URL))
                    {
                        string LocationQuery = @"
                     INSERT INTO meter_location ( location_url, location_at,location_by,complaint_id)
                     VALUES (@URL,@LocationAt,@LocationBy,@complaintId)";
                        LocationModel locationModel = new()
                        {
                            URL = model.URL,
                            LocationBy = userId.Value,
                            LocationAt = DateTime.Now,
                            complaintId = model.Id
                        };
                        if (ModelState.IsValid)
                        {
                            try
                            {
                                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                                {
                                    using (MySqlCommand command = new MySqlCommand(LocationQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@URL", model.URL);
                                        command.Parameters.AddWithValue("@complaintId", locationModel.complaintId);
                                        command.Parameters.AddWithValue("@LocationBy", locationModel.LocationBy);
                                        command.Parameters.AddWithValue("@LocationAt", locationModel.LocationAt);
                                        connection.Open();
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return StatusCode(500, "Internal server error while Adding Location.");
                            }
                        }
                        string logQuery = @"
                         INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                         VALUES (@userId,@Action,@ActionDate,@complaintId)";
                        logModel logModel1 = new()
                        {
                            userId = userId.Value,
                            ActionDate = DateTime.Now,
                            Action = "Location Added",
                            complaintId = model.Id
                        };
                        if (ModelState.IsValid)
                        {
                            try
                            {
                                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                                {
                                    using (MySqlCommand command = new MySqlCommand(logQuery, connection))
                                    {
                                        command.Parameters.AddWithValue("@userId", logModel1.userId);
                                        command.Parameters.AddWithValue("@complaintId", logModel1.complaintId);
                                        command.Parameters.AddWithValue("@Action", logModel1.Action);
                                        command.Parameters.AddWithValue("@ActionDate", logModel1.ActionDate);
                                        connection.Open();
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                return StatusCode(500, "Internal server error while Adding Location Logs.");
                            }
                        }
                    }
                        //// Save files to the server
                        //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/complaintImages");
                        ////var uploadsFolder = Path.Combine("D:\\xampp\\htdocs\\lesco\\backend\\web\\uploads\\meters_complaint");
                        //foreach (var file in ComplaintImages)
                        //{
                        //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        //    using (var fileStream = new FileStream(filePath, FileMode.Create))
                        //    {
                        //        await file.CopyToAsync(fileStream);
                        //    }
                        // Save files to the specified folder
                        var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\complaintImages";
                    // List of allowed extensions
                    var allowedExtensions = new List<string> { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xml" };

                    // Iterate through the uploaded files
                    foreach (var file in ComplaintImages)
                    {
                        // Get the file extension
                        var extension = Path.GetExtension(file.FileName).ToLower();

                        // Validate the file size and extension
                        if (file.Length > 0 && allowedExtensions.Contains(extension))
                        {
                            // Generate a unique file name with a timestamp
                            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: yyyyMMdd_HHmmssfff
                            var uniqueFileName = $"{timestamp}{extension}"; // e.g., 20250117_153045123.png

                            // Combine the directory path with the unique file name
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            // Save the file to the specified directory
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }
                            //// Ensure the directory exists
                            //if (!Directory.Exists(uploadsFolder))
                            //{
                            //    Directory.CreateDirectory(uploadsFolder);
                            //}

                            //// Iterate through the uploaded files
                            //foreach (var file in ComplaintImages)
                            //{
                            //    // Generate a unique file name to avoid collisions
                            //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);

                            //    // Combine the directory path with the unique file name
                            //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            //    // Save the file to the specified directory
                            //    using (var fileStream = new FileStream(filePath, FileMode.Create))
                            //    {
                            //        await file.CopyToAsync(fileStream);
                            //    }                    


                            // Save the file information in the complaint_images table
                            string insertImageQuery = @"
                            INSERT INTO complaint_images ( complaint_id, file_path)
                            VALUES ( @ComplaintId, @FileName)";

                            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await db.ExecuteAsync(insertImageQuery, new
                                {
                                    ComplaintId = model.Id,
                                    FileName = uniqueFileName
                                });
                            }
                        }
                    }
                    // Preserve filter values
           
                    ViewData["FMSN"] = fmsn;
                    ViewData["FAT"] = fat;
                    //ViewData["FSDN"] = fsdn;
                    ViewData["IssueDescription"] = fissueDescription;
                    ViewData["Status"] = fstatus;
                    ViewData["IsFaulty"] = fisFaulty;
                    ViewData["page"] = fpage.Value;
                    //ViewData["CustomerId"] = fcustomerId;
                    ViewData["openDescription"] = fopenDescription;
                    ViewData["pageSize"] = fpageSize;
                    ViewBag.fCreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
                    ViewBag.fCloseDate = fcloseDate?.ToString("yyyy-MM-dd");
                    ViewBag.fExecutedDateRange = fexecutedDateRange;

                    return RedirectToAction("ViewComplaint", new
                    {
                        fmsn,
                        fat,
                        fissueDescription,
                        fopenDescription,
                        fstatus,
                        fisFaulty,
                        fcreatedDate,
                        fcloseDate,
                        fexecutedDateRange,
                        fpage,
                        fpageSize
                    });
                    //return RedirectToAction("ViewComplaint");
                }
                catch (Exception ex)
                {

                }
            }

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Populate AssignedTo dropdown
                string assignToQuery = "SELECT Id as value, username as text FROM user WHERE user_role = 14";
                var assignToData = await db.QueryAsync<DTODropdown>(assignToQuery);
                ViewBag.AssignToList = new SelectList(assignToData.Distinct(), "Value", "Text", model.AssignedTo);
                string descriptionQuery = "SELECT id as value, description as text FROM meter_description";
                var descriptionData = await db.QueryAsync<DTODropdown>(descriptionQuery);
                ViewBag.DescriptionList = new SelectList(descriptionData, "Value", "Text");
            }
            ViewBag.SignedInUserId = userId.Value;
            List<int> fieldStaffUsers = new List<int>();

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 14";
                fieldStaffUsers = (await db.QueryAsync<int>(query)).ToList();
                ViewBag.FieldStaffList = fieldStaffUsers;
            }
           
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ViewComplaintDetail(int id, string? frefno, string? fmsn, string? fsdn, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize, string? fnew, string? fold, string? fadd)
        {
            HttpContext.Session.SetString("LastAction", "ViewComplaintDetail");

            ViewBag.ComplaintId = id;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                db.Open();
                dbMaster.Open();
                string currentDbQuery = "SELECT DATABASE();";
                string masterDbName = await dbMaster.ExecuteScalarAsync<string>(currentDbQuery);
                // Fetch complaint details
                string complaintQuery = $@"
            SELECT 
                c.Id,
                c.meter_msn AS MSN,
 c.ref_number AS Reference_No,
 c.subdiv_name AS SubDiv,
 c.address AS Address,
 c.old_imsi AS Old_Imsi,
 c.new_imsi AS New_Imsi,
t1.name AS Telco_Old,
    t2.name AS Telco_New,
                c.remarks AS Remarks,
                c.open_description AS Open_Description,
                c.close_description AS Close_Description,
                CASE
                    WHEN d.description IS NOT NULL THEN d.description
                    ELSE c.executed_description
                 END AS ExecutedDescriptionText,              
                c.execution_date AS Execution_Date,
                c.replaced_date AS Replaced_Date,
                c.removed_date AS RemovedDate,
                c.mute_date AS MuteDate,
                c.opened_at AS OpenedAt,
                c.closed_at AS ClosedAt,
                c.to_be_check_date AS ToBeCheckDate,
                c.returned_to_subdiv_date AS ReturnedToSubdivDate,
                c.not_under_warranty_date AS NotUnderWarrantyDate,
  c.under_warranty_date AS UnderWarrantyDate,
                c.with_subdiv_date AS WithSubdivDate,
                c.opened_by AS OpenedBy,
                c.new_msn AS NewMSN,
                c.last_comm_time AS LastCommunicationTime,
                u1.username AS OpenedByUsername,
                c.closed_by AS ClosedBy,
                u2.username AS ClosedByUsername,
                c.assigned_to AS AssignedTo,
                u3.username AS AssignedToUsername,
                c.status,             
               c.reported_by AS ComplaintReportedBy,
                CASE
                    WHEN c.reported_by IN (1, 2, 3, 4, 5) THEN NULL
                    ELSE u4.username
                END AS ReportedByUserName,
                c.is_faulty AS Is_Faulty,
                p.name AS ProjectName
            FROM 
                meter_complaint c
LEFT JOIN telco t1 ON c.telco_old = t1.id
LEFT JOIN telco t2 ON c.telco_new = t2.id
            LEFT JOIN 
                user u1 ON c.opened_by = u1.Id
            LEFT JOIN 
                user u2 ON c.closed_by = u2.Id
            LEFT JOIN 
                user u3 ON c.assigned_to = u3.Id
            LEFT JOIN
                meter_description d ON c.executed_description = d.id 
            LEFT JOIN
                user u4 ON c.reported_by = u4.Id    
           LEFT JOIN `{masterDbName}`.`projects` p ON c.project_id = p.Id
            WHERE 
                c.Id = @Id";
                string logQuery = @"
            SELECT 
               l.action AS Action,
               l.action_datetime AS ActionDate,
               u5.username AS ActionUser
            From
                 complaint_logs l
            LEFT JOIN
                 user u5 ON l.user_id = u5.Id
            WHERE 
                l.complaint_id = @Id";

                var complaint = await db.QuerySingleOrDefaultAsync<ComplaintViewModel>(complaintQuery, new { Id = id });
                var logs = await db.QueryAsync<LogViewModel>(logQuery, new { Id = id });
                if (complaint == null)
                {
                    return NotFound();
                }
                // Fetch associated images
                string imagesQuery = @"
                SELECT file_path
                FROM complaint_images
                WHERE complaint_id = @Id";

                complaint.Images = (await db.QueryAsync<string>(imagesQuery, new { Id = id })).ToList();
                string LocationQuery = @"
                SELECT location_url AS URL, location_at AS LocationAt
                FROM meter_location
                WHERE complaint_id = @Id";

                complaint.Locations = (await db.QueryAsync<LocationModel>(LocationQuery, new { Id = id })).ToList();
                ViewData["FRefNo"] = frefno;
                // Preserve filter values
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                ViewData["FSDN"] = fsdn;
                ViewData["FADD"] = fadd;
                ViewData["FNEW"] = fnew;
                ViewData["FOLD"] = fold;
                ViewData["IssueDescription"] = fissueDescription;
                ViewData["Status"] = fstatus;
                ViewData["page"] = fpage.Value;
                ViewData["pageSize"] = fpageSize;
                ViewData["IsFaulty"] = fisFaulty;
                ViewBag.CreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
                ViewBag.CloseDate = fcloseDate?.ToString("yyyy-MM-dd");
                ViewBag.ExecutedDateRange = fexecutedDateRange;
                var complaintLogModel = new ComplaintLogModel
                {
                    Complaint = complaint,
                    Logs = logs.ToList()
                };

                return View(complaintLogModel);
                //return View(complaint);
            }
        }

       
        [HttpPost]
        public async Task<IActionResult> SaveFaultyMeterDetails(FaultyMeterViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                string loginUrl = Url.Action("LoginUser", "Account");
                return Json(new { success = false, message = "User not authenticated", redirectUrl = loginUrl });
            }

            string checkQuery = @"
            SELECT COUNT(*) 
            FROM meters_faulty 
            WHERE msn = @MSN AND status IN (0,1,2)";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MSN", model.MSN);
                        connection.Open();
                        var existingFaultyCount = Convert.ToInt32(await command.ExecuteScalarAsync());

                        if (existingFaultyCount > 0)
                        {
                            return Json(new { success = false, message = "Faulty Meter List already contains this MSN." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error while checking existing Faulty Meters.");
            }

            int assignedId = 0;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 9";
                assignedId = await db.QuerySingleOrDefaultAsync<int>(query);
            }
            int projectId = 0; // Default value

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string getProjectQuery = "SELECT project_id FROM meter_complaint WHERE meter_msn = @MSN LIMIT 1";
                projectId = await db.QuerySingleOrDefaultAsync<int>(getProjectQuery, new { model.MSN });

                if (projectId == 0) // Ensure a valid project_id is retrieved
                {
                    return Json(new { success = false, message = "Project ID not found for the selected MSN." });
                }
            }
            model.OpenedAt = DateTime.Now;
            model.OpenedBy = userId.Value;
            model.AssignedTo = assignedId;
            model.Project = projectId.ToString(); // ✅ Convert int to string
           
            if (ModelState.IsValid)
            {
                string insertQuery = @"
                INSERT INTO meters_faulty (msn, opened_at, opened_by, open_description, assigned_to, data_reset,project_id)
                VALUES (@MSN, @OpenedAt, @OpenedBy, @OpenDescription, @AssignedTo, @DataReset,@Project)";
                string updateComplaintQuery = @"
                UPDATE meter_complaint
                SET is_faulty = 1
                WHERE meter_msn = @MSN";

                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(insertQuery, new
                        {
                            model.MSN,
                            model.OpenedAt,
                            model.OpenedBy,
                            model.OpenDescription,
                            model.AssignedTo,
                            model.DataReset,
                            model.Project
                        });

                        await db.ExecuteAsync(updateComplaintQuery, new { model.MSN });
                    }

                    return Json(new { success = true, redirectUrl = Url.Action("ViewComplaint", "Complaint") });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Error while saving faulty meter details.");
                }
            }

            return BadRequest(ModelState);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubDivCode(string msn)
        {
            if (string.IsNullOrEmpty(msn))
            {
                return BadRequest("MSN is required.");
            }

            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string query = "SELECT m_sub_div FROM installations WHERE meter_msn = @msn";
                    var subDivCode = await db.QuerySingleOrDefaultAsync<string>(query, new { msn });

                    if (subDivCode == null)
                    {
                        return NotFound();
                    }

                    return Json(new { subDivCode });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DeleteComplaint(int id)
        {
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
            if (id == 0)
            {
                return NotFound();
            }
            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string deleteQuery = "DELETE FROM meter_complaint WHERE id = @id";

                    using (MySqlCommand command = new MySqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.Add("@id", MySqlDbType.Int32).Value = id;

                        connection.Open();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                TempData["ErrorMessage"] = "An error occurred while trying to delete the complaint.";
                return StatusCode(500, "Internal server error.");
            }
            string insertQuery = @"
                INSERT INTO complaint_logs (user_id, action, action_datetime,complaint_id)
                VALUES (@userId,@Action,@ActionDate,@complaintId)";
            logModel logModel = new()
            {
                userId = userId.Value,
                ActionDate = DateTime.Now,
                Action = "Complaint Deleted",
                complaintId = id
            };
            if (ModelState.IsValid)
            {
                try
                {
                    using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        using (MySqlCommand command = new MySqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@userId", logModel.userId);
                            command.Parameters.AddWithValue("@complaintId", logModel.complaintId);
                            command.Parameters.AddWithValue("@Action", logModel.Action);
                            command.Parameters.AddWithValue("@ActionDate", logModel.ActionDate);
                            connection.Open();
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Internal server error while processing complaint.");
                }
            }
            return RedirectToAction("ViewComplaint");
        }

        [AuthorizeUserEx]
        public async Task<IActionResult> ViewFaultyMeters(string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? status, DateTime? executedDate, string? fdataReset, string? freceived, int? page, int? pageSize)
        {
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
            //int SignedInUserId = userId.Value;
            //List<FaultyMeterModel> faultyMeters;

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                pageSize = pageSize ?? 50; // Default to 50 if not provided
                page = page ?? 1;
                var start = (page - 1) * pageSize;
                // Removing leading commas from filter values
                fmsn = fmsn?.TrimStart(',');
                fat = fat?.TrimStart(',');
                fReportedBy = fReportedBy?.TrimStart(',');
                fdataReset = fdataReset?.TrimStart(',');
                freceived = freceived?.TrimStart(',');
                //fsdn = fsdn?.TrimStart(',');
                issueDescription = issueDescription?.Trim();
                status = status?.TrimStart(',');

                // Constructing SQL conditions
                string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"mf.msn IN ({fmsn})" : "1=1";
                string fatCondition = !string.IsNullOrEmpty(fat) ? $"mf.assigned_to IN ({fat})" : "1=1";
                string fReportedBycondition = !string.IsNullOrEmpty(fReportedBy) ? $"mf.opened_by IN ({fReportedBy})" : "1=1";
                //string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"mf.sub_div IN ({fsdn})" : "1=1";
                string issueDescriptionCondition = !string.IsNullOrEmpty(issueDescription) ? $"mf.open_description LIKE '%{issueDescription}%'" : "1=1";
                string statusCondition = !string.IsNullOrEmpty(status) ? $"mf.status IN ({status})" : "1=1";
                string dataResetCondition = !string.IsNullOrEmpty(fdataReset) ? $"mf.data_reset IN ({fdataReset})" : "1=1";
                string ReceivedCondition = !string.IsNullOrEmpty(freceived) ? $"mf.received IN ({freceived})" : "1=1";
                string executedDateCondition = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                // Fetch data for dropdowns

                string msnQuery = "SELECT DISTINCT msn AS value, msn AS text FROM meters_faulty";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");

                string assignedToQuery = "SELECT DISTINCT Id AS VALUE, username AS TEXT FROM USER WHERE user_role = 9 OR user_role = 15 OR user_role=1 OR user_role=5";
                var assignedToData = await db.QueryAsync<DTODropdown>(assignedToQuery);
                ViewBag.SelectAssignedTo = new SelectList(assignedToData.Distinct(), "Value", "Text");

                string ReportedByQuery = @" SELECT DISTINCT
                    u1.Id AS Value,  u1.username AS Text
                FROM meters_faulty mf
                    LEFT JOIN user u1 ON mf.opened_by = u1.Id";
                var ReportedByData = await db.QueryAsync<DTODropdown>(ReportedByQuery);
                ViewBag.SelectReportedBy = new SelectList(ReportedByData.Distinct(), "Value", "Text");

                //string subDivisionQuery = "SELECT distinct sub_div_code as value,concat(sub_div_code,'-',name) as text FROM survey_hesco_subdivision group by name ORDER BY sub_div_code ASC";
                //var subDivisionData = await db.QueryAsync<DTODropdown>(subDivisionQuery);
                //ViewBag.SelectSubDivision = new SelectList(subDivisionData.Distinct(), "Value", "Text");
                int SignedInUserId = userId.Value;
                //int adminUser = 0;

                // Retrieve the admin user ID
                //string query = "SELECT id FROM user WHERE user_role = 1";
                //var adminUsers = await db.QueryAsync<int>(query);
                //if (adminUsers.Any())
                //{
                //    adminUser = adminUsers.First(); 
                //}
                string statusQuery = @"
                SELECT DISTINCT 
                    status AS Value, 
                    CASE 
                        WHEN status = 0 THEN 'Open' 
                        WHEN status = 1 THEN 'Executed' 
                        WHEN status = 2 THEN 'Verified'
                        ELSE 'Dispatched' 
                    END AS Text 
                FROM 
                    meters_faulty 
                ORDER BY 
                    status ASC";
                var statusData = await db.QueryAsync<DTODropdown>(statusQuery);
                ViewBag.SelectStatus = new SelectList(statusData.Distinct(), "Value", "Text");

                string dataResetQuery = @"
                SELECT DISTINCT 
                    data_reset as Value, CASE WHEN data_reset = 1 THEN 'Yes' ELSE 'No' END as Text
                FROM 
                    meters_faulty";
                var dataResetData = await db.QueryAsync<DTODropdown>(dataResetQuery);
                ViewBag.SelectDataReset = new SelectList(dataResetData.Distinct(), "Value", "Text");
                string ReceivedQuery = @"
                SELECT DISTINCT  
                    received as Value, CASE WHEN received = 1 THEN 'Yes' ELSE 'No' END as Text
                FROM 
                    meters_faulty";
                var ReceivedData = await db.QueryAsync<DTODropdown>(ReceivedQuery);
                ViewBag.SelectReceived = new SelectList(ReceivedData.Distinct(), "Value", "Text");

                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                //var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fReportedByA = string.IsNullOrEmpty(fReportedBy) ? "1" : fReportedBy;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                var statusA = string.IsNullOrEmpty(status) ? "1" : status;
                var fdataResetA = string.IsNullOrEmpty(fdataReset) ? "1" : fdataReset;
                var freceivedA = string.IsNullOrEmpty(freceived) ? "1" : freceived;
                var executedDateA = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var issueDescriptionA = string.IsNullOrEmpty(issueDescription) ? "1" : issueDescription;

                // Query for faultyMeters
                string FaultyMetersGeneralQuery = $@"
                SELECT
                    {FaultyMetersSelectQuery}
                FROM meters_faulty mf
                    LEFT JOIN USER u1 ON mf.opened_by = u1.Id
                    LEFT JOIN USER u2 ON mf.assigned_to = u2.Id
                    LEFT JOIN (
                        SELECT meter_msn, MAX(removed_date) AS removed_date 
                        FROM meter_complaint 
                        GROUP BY meter_msn
                    ) c ON mf.msn = c.meter_msn
                WHERE
                      (mf.assigned_to = @SignedInUserId OR @SignedInUserId IN (SELECT id FROM user WHERE user_role = 1 OR user_role=17  OR user_role=5)) AND
                    mf.received =1 AND
                    {fmsnCondition} AND
                    {fatCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {executedDateCondition} AND
                    {fReportedBycondition} AND 
                    {dataResetCondition} AND
                    {ReceivedCondition}
                ORDER BY 
                    mf.data_reset ASC,mf.opened_at DESC";

                var countQueryFinal = string.Format($"select count(*) from ({FaultyMetersGeneralQuery})a", FaultyMetersCountQuery, fmsnA, fatA, issueDescriptionA, statusA, fReportedByA, executedDateA, fdataResetA, freceivedA);
                var totalRecords = await db.ExecuteScalarAsync<int>(countQueryFinal, new { SignedInUserId, fmsn, fat, status, fReportedBy, executedDate, issueDescription, start, pageSize, fdataReset, freceived });
                var FaultyMeterQueryFinal = string.Format(FaultyMetersGeneralQuery + FaultyLimitQuery, FaultyMetersSelectQuery, fmsnA, fatA, issueDescriptionA, fReportedByA, statusA, executedDateA, fdataResetA, freceivedA);
                var faultyMeters = await db.QueryAsync<FaultyMeterModel>(FaultyMeterQueryFinal, new { SignedInUserId, fmsn, fat, status, fReportedBy, executedDate, issueDescription, start, pageSize, fdataReset, freceived });

                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                // Preserve filter values
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                //ViewData["FSDN"] = fsdn;
                ViewData["IssueDescription"] = issueDescription;
                ViewData["Status"] = status;
                ViewData["DR"] = fdataReset;
                ViewData["page"] = page.Value;
                ViewData["pageSize"] = pageSize;
                ViewData["totalRecords"] = totalRecords;
                ViewData["totalPages"] = totalPages;
                ViewData["FRB"] = fReportedBy;
                ViewData["Received"] = freceived;
                ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                ViewBag.PageSize = pageSize;
                //string query = @"
                //SELECT 
                //    mf.id AS Id,
                //    mf.msn AS MSN,
                //    s.name AS SubDiv,
                //    mf.opened_at AS OpenedAt,
                //    u1.username AS OpenedBy, 
                //    u2.name AS AssignedTo, 
                //    mf.open_description AS OpenDescription
                //FROM meters_faulty mf
                //LEFT JOIN user u1 ON mf.opened_by = u1.Id
                //LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                //LEFT JOIN 
                //survey_hesco_subdivision s ON mf.sub_div = s.sub_div_code
                //WHERE mf.assigned_to = @SignedInUserId";

                //faultyMeters = (await db.QueryAsync<FaultyMeterModel>(query, new { SignedInUserId = SignedInUserId })).ToList();


                return View(faultyMeters);
            }
        }
        [AuthorizeUserEx]
        public async Task<IActionResult> ReceivePendingList(string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? status, DateTime? executedDate, string? fdataReset, string? freceived, int? page, int? pageSize)
        {
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
            int SignedInUserId = userId.Value;
            //List<FaultyMeterModel> faultyMeters;

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                pageSize = pageSize ?? 50; // Default to 50 if not provided
                page = page ?? 1;
                var start = (page - 1) * pageSize;
                // Removing leading commas from filter values
                fmsn = fmsn?.TrimStart(',');
                fat = fat?.TrimStart(',');
                fReportedBy = fReportedBy?.TrimStart(',');
                fdataReset = fdataReset?.TrimStart(',');
                freceived = freceived?.TrimStart(',');
                //fsdn = fsdn?.TrimStart(',');
                issueDescription = issueDescription?.Trim();
                status = status?.TrimStart(',');

                // Constructing SQL conditions
                string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"mf.msn IN ({fmsn})" : "1=1";
                string fatCondition = !string.IsNullOrEmpty(fat) ? $"mf.assigned_to IN ({fat})" : "1=1";
                string fReportedBycondition = !string.IsNullOrEmpty(fReportedBy) ? $"mf.opened_by IN ({fReportedBy})" : "1=1";
                //string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"mf.sub_div IN ({fsdn})" : "1=1";
                string issueDescriptionCondition = !string.IsNullOrEmpty(issueDescription) ? $"mf.open_description LIKE '%{issueDescription}%'" : "1=1";
                string statusCondition = !string.IsNullOrEmpty(status) ? $"mf.status IN ({status})" : "1=1";
                string dataResetCondition = !string.IsNullOrEmpty(fdataReset) ? $"mf.data_reset IN ({fdataReset})" : "1=1";
                string ReceivedCondition = !string.IsNullOrEmpty(freceived) ? $"mf.received IN ({freceived})" : "1=1";
                string executedDateCondition = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                // Fetch data for dropdowns

                string msnQuery = "SELECT DISTINCT msn AS value, msn AS text FROM meters_faulty WHERE received=0";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");

                string assignedToQuery = "SELECT DISTINCT Id AS VALUE, username AS TEXT FROM USER WHERE user_role = 9 OR user_role = 15 OR user_role=1 OR user_role=5";
                var assignedToData = await db.QueryAsync<DTODropdown>(assignedToQuery);
                ViewBag.SelectAssignedTo = new SelectList(assignedToData.Distinct(), "Value", "Text");

                string ReportedByQuery = @" SELECT DISTINCT
                    u1.Id AS Value,  u1.username AS Text
                FROM meters_faulty mf
                    LEFT JOIN user u1 ON mf.opened_by = u1.Id";
                var ReportedByData = await db.QueryAsync<DTODropdown>(ReportedByQuery);
                ViewBag.SelectReportedBy = new SelectList(ReportedByData.Distinct(), "Value", "Text");

                //string subDivisionQuery = "SELECT distinct sub_div_code as value,concat(sub_div_code,'-',name) as text FROM survey_hesco_subdivision group by name ORDER BY sub_div_code ASC";
                //var subDivisionData = await db.QueryAsync<DTODropdown>(subDivisionQuery);
                //ViewBag.SelectSubDivision = new SelectList(subDivisionData.Distinct(), "Value", "Text");


                string statusQuery = @"
                SELECT DISTINCT 
                    status AS Value, 
                    CASE 
                        WHEN status = 0 THEN 'Open' 
                        WHEN status = 1 THEN 'Executed' 
                        WHEN status = 2 THEN 'Verified'
                        WHEN status = 3 THEN 'Dispatched'
                        ELSE 'Returned to Subdivision' 
                    END AS Text 
                FROM 
                    meters_faulty 
                ORDER BY 
                    status ASC";
                var statusData = await db.QueryAsync<DTODropdown>(statusQuery);
                ViewBag.SelectStatus = new SelectList(statusData.Distinct(), "Value", "Text");

                string dataResetQuery = @"
                SELECT DISTINCT 
                    data_reset as Value, CASE WHEN data_reset = 1 THEN 'Yes' ELSE 'No' END as Text
                FROM 
                    meters_faulty";
                var dataResetData = await db.QueryAsync<DTODropdown>(dataResetQuery);
                ViewBag.SelectDataReset = new SelectList(dataResetData.Distinct(), "Value", "Text");
                string ReceivedQuery = @"
                SELECT DISTINCT  
                    received as Value, CASE WHEN received = 1 THEN 'Yes' ELSE 'No' END as Text
                FROM 
                    meters_faulty";
                var ReceivedData = await db.QueryAsync<DTODropdown>(ReceivedQuery);
                ViewBag.SelectReceived = new SelectList(ReceivedData.Distinct(), "Value", "Text");

                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                //var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fReportedByA = string.IsNullOrEmpty(fReportedBy) ? "1" : fReportedBy;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                var statusA = string.IsNullOrEmpty(status) ? "1" : status;
                var fdataResetA = string.IsNullOrEmpty(fdataReset) ? "1" : fdataReset;
                var freceivedA = string.IsNullOrEmpty(freceived) ? "1" : freceived;
                var executedDateA = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var issueDescriptionA = string.IsNullOrEmpty(issueDescription) ? "1" : issueDescription;

                // Query for faultyMeters
                //string FaultyMetersGeneralQuery = $@"
                //SELECT
                //    {FaultyMetersSelectQuery}
                //FROM meters_faulty mf
                //    LEFT JOIN user u1 ON mf.opened_by = u1.Id
                //    LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                //    LEFT JOIN 
                //    survey_hesco_subdivision s ON mf.sub_div = s.sub_div_code
                //    LEFT JOIN meter_complaint c ON mf.msn = c.meter_msn
                //WHERE
                //      mf.received = 0 AND
                //    {fmsnCondition} AND
                //    {fatCondition} AND
                //    {fsdnCondition} AND
                //    {issueDescriptionCondition} AND
                //    {statusCondition} AND
                //    {executedDateCondition} AND
                //    {fReportedBycondition} AND 
                //    {dataResetCondition} AND
                //    {ReceivedCondition}
                //ORDER BY 
                //    mf.data_reset ASC,mf.opened_at DESC";
                string FaultyMetersGeneralQuery = $@"
                SELECT
                    {FaultyMetersSelectQuery}
               FROM meters_faulty mf
                    LEFT JOIN USER u1 ON mf.opened_by = u1.Id
                    LEFT JOIN USER u2 ON mf.assigned_to = u2.Id
                    LEFT JOIN survey_hesco_subdivision s ON mf.sub_div = s.sub_div_code
                    LEFT JOIN (
                        SELECT meter_msn, MAX(removed_date) AS removed_date 
                        FROM meter_complaint 
                        GROUP BY meter_msn
                    ) c ON mf.msn = c.meter_msn
                    WHERE mf.received = 0 AND
                    {fmsnCondition} AND
                    {fatCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {executedDateCondition} AND
                    {fReportedBycondition} AND 
                    {dataResetCondition} AND
                    {ReceivedCondition}
                ORDER BY 
                    mf.data_reset ASC,mf.opened_at DESC";

                var countQueryFinal = string.Format($"select count(*) from ({FaultyMetersGeneralQuery})a", FaultyMetersCountQuery, fmsnA, fatA, issueDescriptionA, statusA, fReportedByA, executedDateA, fdataResetA, freceivedA);
                var totalRecords = await db.ExecuteScalarAsync<int>(countQueryFinal, new { fmsn, fat, status, fReportedBy, executedDate, issueDescription, start, pageSize, fdataReset, freceived });
                var FaultyMeterQueryFinal = string.Format(FaultyMetersGeneralQuery + FaultyLimitQuery, FaultyMetersSelectQuery, fmsnA, fatA, issueDescriptionA, fReportedByA, statusA, executedDateA, fdataResetA, freceivedA);
                var faultyMeters = await db.QueryAsync<FaultyMeterModel>(FaultyMeterQueryFinal, new { fmsn, fat, status, fReportedBy, executedDate, issueDescription, start, pageSize, fdataReset, freceived });

                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                // Preserve filter values
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                //ViewData["FSDN"] = fsdn;
                ViewData["IssueDescription"] = issueDescription;
                ViewData["Status"] = status;
                ViewData["DR"] = fdataReset;
                ViewData["page"] = page.Value;
                ViewData["pageSize"] = pageSize;
                ViewData["totalRecords"] = totalRecords;
                ViewData["totalPages"] = totalPages;
                ViewData["FRB"] = fReportedBy;
                ViewData["Received"] = freceived;
                ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                ViewBag.PageSize = pageSize;
                //string query = @"
                //SELECT 
                //    mf.id AS Id,
                //    mf.msn AS MSN,
                //    s.name AS SubDiv,
                //    mf.opened_at AS OpenedAt,
                //    u1.username AS OpenedBy, 
                //    u2.name AS AssignedTo, 
                //    mf.open_description AS OpenDescription
                //FROM meters_faulty mf
                //LEFT JOIN user u1 ON mf.opened_by = u1.Id
                //LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                //LEFT JOIN 
                //survey_hesco_subdivision s ON mf.sub_div = s.sub_div_code
                //WHERE mf.assigned_to = @SignedInUserId";

                //faultyMeters = (await db.QueryAsync<FaultyMeterModel>(query, new { SignedInUserId = SignedInUserId })).ToList();


                return View(faultyMeters);
            }
        }

        
        public async Task<IActionResult> ViewLocation(string msn)
        {
            if (string.IsNullOrEmpty(msn))
            {
                return BadRequest("Meter MSN is required.");
            }

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {

                string query = "SELECT latitude, longitude FROM installations WHERE meter_msn = @MSN";
                var location = await db.QueryFirstOrDefaultAsync(query, new { MSN = msn });

                if (location == null)
                {
                    return NotFound("Location data not found for the provided Meter MSN.");
                }
                string mapUrl = $"https://www.google.com/maps?q={location.latitude},{location.longitude}";
                return Redirect(mapUrl);
            }
        }
        public async Task<IActionResult> ViewSurveyLocation(string refNo)
        {
            if (string.IsNullOrEmpty(refNo))
            {
                return BadRequest("Meter Reference No. is required.");
            }

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {

                string query = "SELECT latitude, longitude FROM survey WHERE ref_no = @Reference_No";
                var location = await db.QueryFirstOrDefaultAsync(query, new { Reference_No = refNo });

                if (location == null)
                {
                    return NotFound("Location data not found for the provided Reference_No.");
                }
                string mapUrl = $"https://www.google.com/maps?q={location.latitude},{location.longitude}";
                return Redirect(mapUrl);
            }
        }
        public async Task<IActionResult> ViewUpdatedInstallationLocation(string refNo)
        {
            if (string.IsNullOrEmpty(refNo))
            {
                return BadRequest("Meter Reference No. is required.");
            }

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {

                string query = "SELECT latitude, longitude FROM meter_complaint WHERE ref_number = @Reference_No";
                var location = await db.QueryFirstOrDefaultAsync(query, new { Reference_No = refNo });

                if (location == null)
                {
                    return NotFound("Location data not found for the provided Reference_No.");
                }
                string mapUrl = $"https://www.google.com/maps?q={location.latitude},{location.longitude}";
                return Redirect(mapUrl);
            }
        }
        [HttpGet]
        public async Task<IActionResult> CloseBulkComplaint()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> CloseBulkComplaint(IFormFile file)
        {
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
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please upload a valid file.");
                return View();
            }

            var closedDescriptions = new List<(string msn, string closedDescription)>();
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");
            // Read the Excel file
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0]; // Assuming data is in the first worksheet
                    var rowCount = worksheet.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++) // Assuming first row is header
                    {
                        var msn = worksheet.Cells[row, 1].Text; // Adjust column index as necessary
                        var closedDescription = worksheet.Cells[row, 2].Text; // Adjust column index as necessary
                        closedDescriptions.Add((msn, closedDescription));
                    }
                }
            }

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                foreach (var (msn, closedDescription) in closedDescriptions)
                {
                    // Check if MSN exists with status 0
                    var existingMsn = await db.QueryFirstOrDefaultAsync<ComplaintViewModel>(
                        "SELECT * FROM meter_complaint WHERE meter_msn = @Msn AND status = 0", new { Msn = msn });

                    if (existingMsn != null)
                    {
                        // Update status to 3 and set close_description
                        var updateQuery = @"
                        UPDATE meter_complaint 
                        SET status = 3, 
                            close_description = @ClosedDescription,
                            closed_by = @ClosedBy,
                            closed_at = @ClosedAt 
                            WHERE meter_msn = @Msn";

                        var parameters = new
                        {
                            Msn = msn,
                            ClosedDescription = closedDescription,
                            ClosedAt = DateTime.Now,
                            ClosedBy = userId.Value
                        };

                        await db.ExecuteAsync(updateQuery, parameters);
                    }
                }
            }

            return RedirectToAction("ViewComplaint");
        }


        [HttpGet]
        public async Task<IActionResult> AssignBulkComplaints()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string subDivisionQuery = "SELECT distinct sub_div_code as value,concat(sub_div_code,'-',name) as text FROM survey_hesco_subdivision group by name ORDER BY sub_div_code ASC";
                var subDivisionData = await db.QueryAsync<DTODropdown>(subDivisionQuery);
                ViewBag.SelectSubDivision = new SelectList(subDivisionData.Distinct(), "Value", "Text");

                string assignedToQuery = "SELECT DISTINCT Id AS Value, username AS Text FROM user WHERE user_role = 14 AND Id != 413";
                var assignedToData = await db.QueryAsync<DTODropdown>(assignedToQuery);
                ViewBag.AssignToList = new SelectList(assignedToData.Distinct(), "Value", "Text");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AssignBulkComplaints(BulkAssignmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    foreach (var subdivision in model.SubDivisions)
                    {
                        // Fetch complaints with status 0 and assigned_to is null
                        string complaintQuery = @"
                        SELECT COUNT(*) 
                        FROM meter_complaint 
                        WHERE sub_div = @SubDivCode AND status=0";

                        int complaintCount = await db.ExecuteScalarAsync<int>(complaintQuery, new { SubDivCode = subdivision });

                        if (complaintCount > 0)
                        {
                            // Update only those complaints that match the criteria
                            string assignQuery = @"
                        UPDATE meter_complaint 
                        SET assigned_to = @AssignedTo 
                        WHERE sub_div = @SubDivCode AND status=0";

                            await db.ExecuteAsync(assignQuery, new { AssignedTo = model.AssignedTo, SubDivCode = subdivision });
                        }
                    }
                }
                return RedirectToAction("ViewComplaint");
            }

            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> EditFaultyMetersByQC(int id, string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? fdataReset, string? status, DateTime? executedDate, int? page, int? pageSize)
        {
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

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT user_role FROM user WHERE id = @UserId";
                var userRole = await db.ExecuteScalarAsync<int>(query, new { UserId = userId.Value });

                if (userRole == 9)
                {
                    return RedirectToAction("EditFaultyMeter", new
                    {
                        Id = id,
                        fmsn = fmsn,
                        fat = fat,
                        issueDescription = issueDescription,
                        status = status,
                        executedDate = executedDate,
                        fReportedBy = fReportedBy,
                        fdataReset = fdataReset,
                        pageSize = pageSize,
                        page = page
                    });
                }
                if (userRole == 1)
                {
                    return RedirectToAction("DispatchedFaultyMeter", new
                    {
                        Id = id,
                        fmsn = fmsn,
                        fat = fat,
                        issueDescription = issueDescription,
                        status = status,
                        executedDate = executedDate,
                        fReportedBy = fReportedBy,
                        fdataReset = fdataReset,
                        pageSize = pageSize,
                        page = page
                    });
                }
            }
            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string editquery = @"                 
                       SELECT 
                        mf.Id AS id,
                        mf.msn AS MSN,
                        mf.fault_description AS Fault_Description,
                        mf.remarks AS Remarks,
                        mf.is_verified AS Verified
                    FROM meters_faulty mf                 
                    WHERE 
                        mf.Id = @id";

                    var faultymeter = await db.QuerySingleOrDefaultAsync<FaultyMetersEditViewModel>(editquery, new { Id = id });

                    if (faultymeter == null)
                    {
                        return NotFound();
                    }
                    // Preserve filter values
                    ViewData["FMSN"] = fmsn;
                    ViewData["FAT"] = fat;
                    ViewData["FRB"] = fReportedBy;
                    //ViewData["FSDN"] = fsdn;
                    ViewData["IssueDescription"] = issueDescription;
                    ViewData["Status"] = status;
                    ViewData["page"] = page.Value;
                    ViewData["DR"] = fdataReset;
                    ViewData["pageSize"] = pageSize;
                    ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                    return View(faultymeter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }
        [HttpPost]
        public async Task<IActionResult> EditFaultyMetersByQC(FaultyMetersEditViewModel model, List<IFormFile> FaultyImages, string[] remarks)
        {
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
            int assignedId = 0;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 9";
                assignedId = await db.QuerySingleOrDefaultAsync<int>(query);
            }
            model.AssignedTo = assignedId;
            model.Remarks = string.Join("; ", remarks);
            // Initialize the base update query
            string updateQuery = @"
            UPDATE meters_faulty
            SET 
                 msn = @MSN,
                 sub_div =@SubDiv,
                 remarks = @Remarks,
                 assigned_to= @AssignedTo,
                 is_verified=@Verified,
                 status=0";
            if (model.Verified == 1)
            {
                updateQuery += @",
                     assigned_to= @AssignedTo,
                     verified_at=@VerifiedAt,
                     verified_by=@VerifiedBy,
                     is_verified=@Verified,
                     status=2";
                model.AssignedTo = 416;
                model.VerifiedAt = DateTime.Now;
                model.VerifiedBy = userId.Value;
            }
            updateQuery += " WHERE id = @Id";
            // Validate uploaded files
            if (FaultyImages != null && FaultyImages.Count > 10)
            {
                ModelState.AddModelError("FaultyImages", "You can only upload a maximum of 10 files.");
            }

            foreach (var file in FaultyImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".png" && extension != ".jpeg" && extension != ".jpg" && extension != ".pdf" && extension != ".doc" && extension != ".docx" && extension != ".xls" && extension != ".xlsx" && extension != ".xml")
                {
                    ModelState.AddModelError("FaultyImages", "Invalid file type. Only PNG, JPEG, JPG, PDF,EXCEL,WORD and XML are allowed.");
                }
            }

            // Ensure the model state is valid before proceeding
            if (ModelState.IsValid)
            {
                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(updateQuery, new
                        {
                            MSN = model.MSN,
                            Remarks = model.Remarks,
                            AssignedTo = model.AssignedTo,
                            VerifiedAt = model.VerifiedAt,
                            VerifiedBy = model.VerifiedBy,
                            Verified = model.Verified,
                            Id = model.Id
                        });
                    }

                    //// Save files to the server
                    //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/FaultImages");
                    ////var uploadsFolder = Path.Combine("D:\\xampp\\htdocs\\lesco\\backend\\web\\uploads\\meters_complaint");
                    //foreach (var file in FaultyImages)
                    //{
                    //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    //    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    //    {
                    //        await file.CopyToAsync(fileStream);
                    //    }
                    // Set the target folder to the desired directory
                    var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\FaultImages";

                    // Ensure the directory exists
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Iterate through the uploaded files
                    foreach (var file in FaultyImages)
                    {
                        if (file.Length > 0) // Ensure the file is not empty
                        {
                            // Generate a unique file name to avoid collisions
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);

                            // Combine the folder path with the unique file name
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            // Save the file to the specified directory
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            // Save the file information in the faulty_images table
                            string insertImageQuery = @"
                            INSERT INTO faulty_images ( fault_id, file_path)
                            VALUES (@FaultId, @FileName)";

                            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await db.ExecuteAsync(insertImageQuery, new
                                {
                                    FaultId = model.Id,
                                    FileName = uniqueFileName
                                });
                            }
                        }
                    }
                    return RedirectToAction("ViewFaultyMeters");
                }
                catch (Exception ex)
                {

                }
            }
            return View(model);
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> EditFaultyMeter(int id, string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? fdataReset, string? status, DateTime? executedDate, string? freceived, int? page, int? pageSize)
        {
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
            ViewBag.SignedInUserId = userId.Value;
            List<int> siteOfficerUsers = new List<int>();

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 9";
                siteOfficerUsers = (await db.QueryAsync<int>(query)).ToList();
                ViewBag.SiteOfficerList = siteOfficerUsers;
            }
            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string editquery = @"                 
                       SELECT 
                        mf.Id AS id,
                        mf.msn AS MSN,
                        mf.open_description AS OpenDescription,
                        mf.fault_description AS Fault_Description,
                        mf.remarks AS Remarks,
                        mf.is_verified AS Verified,
                        mf.dispatched_remarks AS Dispatched_Remarks,
                        mf.status AS Status,
                        mf.data_reset AS DataReset
                        FROM meters_faulty mf                 
                        WHERE 
                            mf.Id = @id";

                    var faultymeter = await db.QuerySingleOrDefaultAsync<FaultyMetersEditViewModel>(editquery, new { Id = id });

                    if (faultymeter == null)
                    {
                        return NotFound();
                    }
                    // Preserve filter values
                    ViewData["FMSN"] = fmsn;
                    ViewData["FAT"] = fat;
                    ViewData["FRB"] = fReportedBy;
                    //ViewData["FSDN"] = fsdn;
                    ViewData["IssueDescription"] = issueDescription;
                    ViewData["Status"] = status;
                    ViewData["page"] = page.Value;
                    ViewData["DR"] = fdataReset;
                    ViewData["pageSize"] = pageSize;
                    ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                    ViewData["Received"] = freceived;
                    ViewBag.SignedInUserId = userId.Value;
                    return View(faultymeter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditFaultyMeter(FaultyMetersEditViewModel model, List<IFormFile> FaultyImages, string?[] FaultDescriptions, string?[] remarks)
        {
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
            // Initialize the base update query
            string updateQuery = @"
            UPDATE meters_faulty
            SET
                status=@Status,
                open_description = @OpenDescription,
                data_reset = @DataReset";


            // Check the status and append necessary fields to the update query
            if (model.Status == 1)
            {
                updateQuery += @",
            fault_description = @Fault_Description,
          assigned_to = @AssignedTo,
          executed_at = @ExecutedAt,
          executed_by = @ExecutedBy,                
          status = @Status";

                int assignedId = 0;

                // Fetch all IDs and pick the first one
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string query = "SELECT id FROM user WHERE user_role = 15 ORDER BY id ASC"; // ORDER BY ensures consistency
                    var assignedIds = (await db.QueryAsync<int>(query)).ToList();
                    if (assignedIds.Any())
                    {
                        assignedId = assignedIds.First(); // Pick the first ID
                    }
                }

                // Assign the first ID and set other model properties
                model.AssignedTo = assignedId;
                model.Fault_Description = string.Join("; ", FaultDescriptions);
                model.ExecutedAt = DateTime.Now;
                model.ExecutedBy = userId.Value;
            }
            else if (model.Status == 2 && model.Verified == 1)
            {
                updateQuery += @",
                     remarks = @Remarks,
                     assigned_to= @AssignedTo,
                     verified_at=@VerifiedAt,
                     verified_by=@VerifiedBy,
                     is_verified=@Verified,
                     status=@Status";
                model.AssignedTo = 12;
                model.VerifiedAt = DateTime.Now;
                model.VerifiedBy = userId.Value;
            }
            else if (model.Status == 2 && model.Verified == 0)
            {
                updateQuery += @",
                     remarks = @Remarks,
                     assigned_to= @AssignedTo,
                     is_verified=@Verified,
                     status=0";
                model.AssignedTo = 12;
            }
            else if (model.Status == 3) // Dispatch status
            {
                updateQuery += @",
                   dispatched_remarks = @Dispatched_Remarks,
                   closed_at= @DispatchedAt,
                   closed_by= @DispatchedBy,
                   status=@Status";
                model.DispatchedAt = DateTime.Now;
                model.DispatchedBy = userId.Value;
            }
            updateQuery += " WHERE id = @Id";
            // Validate uploaded files
            if (FaultyImages != null && FaultyImages.Count > 10)
            {
                ModelState.AddModelError("FaultyImages", "You can only upload a maximum of 10 files.");
            }

            foreach (var file in FaultyImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".png" && extension != ".jpeg" && extension != ".jpg" && extension != ".pdf" && extension != ".doc" && extension != ".docx" && extension != ".xls" && extension != ".xlsx" && extension != ".xml")
                {
                    ModelState.AddModelError("FaultyImages", "Invalid file type. Only PNG, JPEG, JPG, PDF,EXCEL,WORD and XML are allowed.");
                }
            }

            // Ensure the model state is valid before proceeding
            if (ModelState.IsValid)
            {
                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(updateQuery, new
                        {
                            OpenDescription = model.OpenDescription,
                            Fault_Description = model.Fault_Description,
                            Status = model.Status,
                            AssignedTo = model.AssignedTo,
                            ExecutedBy = model.ExecutedBy,
                            ExecutedAt = model.ExecutedAt,
                            Remarks = model.Remarks,
                            VerifiedAt = model.VerifiedAt,
                            VerifiedBy = model.VerifiedBy,
                            Verified = model.Verified,
                            Dispatched_Remarks = model.Dispatched_Remarks,
                            DispatchedBy = model.DispatchedBy,
                            DispatchedAt = model.DispatchedAt,
                            Id = model.Id,
                            DataReset = model.DataReset
                        });
                    }

                    //// Save files to the server
                    //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/FaultImages");
                    ////var uploadsFolder = Path.Combine("D:\\xampp\\htdocs\\lesco\\backend\\web\\uploads\\meters_complaint");
                    //foreach (var file in FaultyImages)
                    //{
                    //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    //    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    //    {
                    //        await file.CopyToAsync(fileStream);
                    //    }
                    // Set the target folder to the desired directory
                    //var uploadsFolder = @"D:\xampp\htdocs\ComplaintSystem\FaultImages";

                    //// Ensure the directory exists
                    //if (!Directory.Exists(uploadsFolder))
                    //{
                    //    Directory.CreateDirectory(uploadsFolder);
                    //}

                    //// Iterate through the uploaded files
                    //foreach (var file in FaultyImages)
                    //{
                    //    if (file.Length > 0) // Ensure the file is not empty
                    //    {
                    //        // Generate a unique file name to avoid collisions
                    //        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);

                    //        // Combine the folder path with the unique file name
                    //        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    //        // Save the file to the specified directory
                    //        using (var fileStream = new FileStream(filePath, FileMode.Create))
                    //        {
                    //            await file.CopyToAsync(fileStream);
                    //        }
                    var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\FaultImages";
                    // List of allowed extensions
                    var allowedExtensions = new List<string> { ".png", ".jpeg", ".jpg", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xml" };

                    // Iterate through the uploaded files
                    foreach (var file in FaultyImages)
                    {
                        // Get the file extension
                        var extension = Path.GetExtension(file.FileName).ToLower();

                        // Validate the file size and extension
                        if (file.Length > 0 && allowedExtensions.Contains(extension))
                        {
                            // Generate a unique file name with a timestamp
                            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff"); // Format: yyyyMMdd_HHmmssfff
                            var uniqueFileName = $"{timestamp}{extension}"; // e.g., 20250117_153045123.png

                            // Combine the directory path with the unique file name
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            // Save the file to the specified directory
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }



                            // Save the file information in the complaint_images table
                            string insertImageQuery = @"
                                INSERT INTO faulty_images (fault_id, file_path)
                                VALUES ( @FaultId, @FileName)";

                            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await db.ExecuteAsync(insertImageQuery, new
                                {
                                    FaultId = model.Id,
                                    FileName = uniqueFileName
                                });
                            }
                        }
                    }
                    return RedirectToAction("ViewFaultyMeters");
                }
                catch (Exception ex)
                {

                }
            }
            ViewBag.SignedInUserId = userId.Value;
            List<int> siteOfficerUsers = new List<int>();

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 9";
                siteOfficerUsers = (await db.QueryAsync<int>(query)).ToList();
                ViewBag.SiteOfficerList = siteOfficerUsers;

            }
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> DispatchedFaultyMeter(int id, string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? status, string? fdataReset, DateTime? executedDate, int? page, int? pageSize)
        {
            try
            {
                using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    string editquery = @"                 
                       SELECT 
                        mf.Id AS id,
                        mf.msn AS MSN,
                        mf.fault_description AS Fault_Description,
                        mf.remarks AS Remarks
                    FROM meters_faulty mf                 
                    WHERE 
                        mf.Id = @id";

                    var faultymeter = await db.QuerySingleOrDefaultAsync<FaultyMetersEditViewModel>(editquery, new { Id = id });

                    if (faultymeter == null)
                    {
                        return NotFound();
                    }
                    // Preserve filter values
                    ViewData["FMSN"] = fmsn;
                    ViewData["FAT"] = fat;
                    ViewData["FRB"] = fReportedBy;
                    //ViewData["FSDN"] = fsdn;
                    ViewData["IssueDescription"] = issueDescription;
                    ViewData["Status"] = status;
                    ViewData["page"] = page.Value;
                    ViewData["DR"] = fdataReset;
                    ViewData["pageSize"] = pageSize;
                    ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                    return View(faultymeter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DispatchedFaultyMeter(FaultyMetersEditViewModel model, List<IFormFile> FaultyImages)
        {
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

            model.DispatchedAt = DateTime.Now;
            model.DispatchedBy = userId.Value;

            // Initialize the base update query
            string updateQuery = @"
            UPDATE meters_faulty
            SET 
                 msn = @MSN,
                 dispatched_remarks = @Dispatched_Remarks,
                 closed_at= @DispatchedAt,
                 closed_by= @DispatchedBy,
                status=3
            WHERE id = @Id";

            // Validate uploaded files
            if (FaultyImages != null && FaultyImages.Count > 10)
            {
                ModelState.AddModelError("FaultyImages", "You can only upload a maximum of 10 files.");
            }

            foreach (var file in FaultyImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".png" && extension != ".jpeg" && extension != ".jpg" && extension != ".pdf" && extension != ".doc" && extension != ".docx" && extension != ".xls" && extension != ".xlsx" && extension != ".xml")
                {
                    ModelState.AddModelError("FaultyImages", "Invalid file type. Only PNG, JPEG, JPG, PDF,EXCEL,WORD and XML are allowed.");
                }
            }

            // Ensure the model state is valid before proceeding
            if (ModelState.IsValid)
            {
                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(updateQuery, new
                        {
                            MSN = model.MSN,
                            Dispatched_Remarks = model.Dispatched_Remarks,
                            DispatchedBy = model.DispatchedBy,
                            DispatchedAt = model.DispatchedAt,
                            Id = model.Id
                        });
                    }

                    //// Save files to the server
                    //var uploadsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot/FaultImages");
                    ////var uploadsFolder = Path.Combine("D:\\xampp\\htdocs\\lesco\\backend\\web\\uploads\\meters_complaint");
                    //foreach (var file in FaultyImages)
                    //{
                    //    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    //    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    //    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    //    {
                    //        await file.CopyToAsync(fileStream);
                    //    }
                    // Set the target folder to the desired directory
                    var uploadsFolder = @"D:\xampp\htdocs\HESCOPhotos\FaultImages";

                    // Ensure the directory exists
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Iterate through the uploaded files
                    foreach (var file in FaultyImages)
                    {
                        if (file.Length > 0) // Ensure the file is not empty
                        {
                            // Generate a unique file name to avoid collisions
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);

                            // Combine the folder path with the unique file name
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            // Save the file to the specified directory
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }




                            // Save the file information in the complaint_images table
                            string insertImageQuery = @"
                            INSERT INTO faulty_images (fault_id, file_path)
                            VALUES ( @FaultId, @FileName)";

                            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await db.ExecuteAsync(insertImageQuery, new
                                {                                
                                    FaultId = model.Id,
                                    FileName = uniqueFileName
                                });
                            }
                        }
                    }

                    return RedirectToAction("ViewFaultyMeters");
                }
                catch (Exception ex)
                {

                }
            }
            return View(model);
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> ViewFaultyMeterDetail(int id, string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? fdataReset, string? status, DateTime? executedDate, int? page, int? pageSize)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            {
                db.Open();
                dbMaster.Open();
                string currentDbQuery = "SELECT DATABASE();";
                string masterDbName = await dbMaster.ExecuteScalarAsync<string>(currentDbQuery);
                // Fetch complaint details
                string meterFaultyQuery = $@"
             SELECT 
                        mf.id AS Id,
                        mf.msn AS MSN,
                        mf.opened_at AS OpenedAt,                       
                        mf.open_description AS OpenDescription,
                        mf.status AS Status,
                        mf.data_reset AS DataReset,
                        mf.received AS Received,
                        mf.received_date AS ReceivedDate,
                        mf.fault_description AS Fault_Description,
                       u3.username AS ExecutedBy,
                        mf.executed_at AS ExecutedAt,                       
                        mf.remarks AS Remarks,
                        mf.dispatched_remarks AS Dispatched_Remarks,
                        mf.verified_at AS VerifiedAt,
                        u4.username AS VerifiedBy,
                        u5.username AS DispatchedBy,
                        mf.closed_at AS DispatchedAt,
                        u1.username AS OpenedBy, 
                        u2.name AS AssignedTo ,
                        p.name AS ProjectName
                    FROM meters_faulty mf
                    LEFT JOIN user u1 ON mf.opened_by = u1.Id
                    LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                     LEFT JOIN user u3 ON mf.executed_by = u3.Id
                     LEFT JOIN user u4 ON mf.verified_by = u4.Id
                     LEFT JOIN user u5 ON mf.closed_by = u5.Id
                    LEFT JOIN `{masterDbName}`.`projects` p ON mf.project_id = p.Id
            WHERE 
                mf.id = @Id";

                var meterFaulty = await db.QuerySingleOrDefaultAsync<FaultyMeterModel>(meterFaultyQuery, new { Id = id });

                if (meterFaulty == null)
                {
                    return NotFound();
                }

                // Fetch associated images
                string imagesQuery = @"
                SELECT file_path
                FROM faulty_images
                WHERE fault_id = @Id";

                meterFaulty.Images = (await db.QueryAsync<string>(imagesQuery, new { Id = id })).ToList();
                // Preserve filter values
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                ViewData["FRB"] = fReportedBy;
                //ViewData["FSDN"] = fsdn;
                ViewData["IssueDescription"] = issueDescription;
                ViewData["Status"] = status;
                ViewData["page"] = page.Value;
                ViewData["DR"] = fdataReset;
                ViewData["pageSize"] = pageSize;
                ViewBag.ExecutedDate = executedDate?.ToString("yyyy-MM-dd");
                return View(meterFaulty);
            }
        }
        [HttpGet]
        public async Task<IActionResult> ViewFaultReportDetail(string msn, int id, string? fmsn, string? fcustomerId, string? fat, string? fissueDescription, string? fstatus, string? fisFaulty, DateTime? fcreatedDate, DateTime? fcloseDate, string? fexecutedDateRange, int? fpage, int? fpageSize)
        {
            ViewBag.Id = id;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                // Check if MSN exists in meters_faulty table
                string checkMsnQuery = @"
                SELECT COUNT(*) 
                FROM meters_faulty mf
                WHERE mf.msn = @MSN";

                var msnExists = await db.ExecuteScalarAsync<int>(checkMsnQuery, new { MSN = msn });

                // Preserve filter values
                //ViewData["FRefNo"] = frefno;
                ViewData["FMSN"] = fmsn;
                ViewData["FAT"] = fat;
                //ViewData["FSDN"] = fsdn;
                ViewData["IssueDescription"] = fissueDescription;
                ViewData["Status"] = fstatus;
                ViewData["page"] = fpage.Value;
                ViewData["pageSize"] = fpageSize;
                ViewData["CustomerId"] = fcustomerId;
                ViewData["IsFaulty"] = fisFaulty;
                ViewBag.CreatedDate = fcreatedDate?.ToString("yyyy-MM-dd");
                ViewBag.CloseDate = fcloseDate?.ToString("yyyy-MM-dd");
                ViewBag.ExecutedDateRange = fexecutedDateRange;

                if (msnExists == 0)
                {
                    // If the MSN doesn't exist, return a message that will trigger a popup
                    //return Json(new { success = false, message = "This MSN is not in faulty meter list." });
                    return View();
                }

                // Proceed with the existing query to fetch the details if MSN exists
                string meterFaultyQuery = @"
                SELECT 
                        mf.id AS Id,
                        mf.msn AS MSN,
                        mf.opened_at AS OpenedAt,                       
                        mf.open_description AS OpenDescription,
                        mf.status AS Status,
                        mf.data_reset AS DataReset,
                        mf.received AS Received,
                        mf.received_date AS ReceivedDate,
                        mf.fault_description AS Fault_Description,
                        u3.username AS ExecutedBy,
                        mf.executed_at AS ExecutedAt,                       
                        mf.remarks AS Remarks,
                        mf.dispatched_remarks AS Dispatched_Remarks,
                        mf.verified_at AS VerifiedAt,
                        u4.username AS VerifiedBy,
                        u5.username AS DispatchedBy,
                        mf.closed_at AS DispatchedAt,
                        u1.username AS OpenedBy, 
                        u2.name AS AssignedTo 
                    FROM meters_faulty mf
                    LEFT JOIN user u1 ON mf.opened_by = u1.Id
                    LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                    LEFT JOIN user u3 ON mf.executed_by = u3.Id
                    LEFT JOIN user u4 ON mf.verified_by = u4.Id
                    LEFT JOIN user u5 ON mf.closed_by = u5.Id
                WHERE 
                  mf.msn = @MSN
                    ORDER BY opened_at DESC
                LIMIT 1";

                var meterFaulty = await db.QuerySingleOrDefaultAsync<FaultyMeterModel>(meterFaultyQuery, new { MSN = msn });

                if (meterFaulty == null)
                {
                    return NotFound();
                }
                // Fetch the fault_id for the current MSN
                int faultId = meterFaulty.Id;

                // Fetch associated images using the fault_id
                string imagesQuery = @"
                SELECT file_path
                FROM faulty_images
                WHERE fault_id = @FaultId";

                meterFaulty.Images = (await db.QueryAsync<string>(imagesQuery, new { FaultId = faultId })).ToList();

                return View(meterFaulty);
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> CreateFaultyMetersComplaint()
        {
            //using (IDbConnection dbDefault = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            //using (IDbConnection dbMaster = new MySqlConnection(_configuration.GetConnectionString("MasterDBConnection")))
            //{
            //    dbDefault.Open();
            //    dbMaster.Open();
            //    // Get the currently connected database name
            //    string currentDbQuery = "SELECT DATABASE();";
            //    string currentDbName = await dbDefault.ExecuteScalarAsync<string>(currentDbQuery);

            //    if (string.IsNullOrEmpty(currentDbName))
            //    {
            //        return BadRequest("Unable to retrieve the database name.");
            //    }

            //    // Get db_id from project_databases based on the current database name
            //    string dbIdQuery = "SELECT id FROM project_databases WHERE db_name = @DbName";
            //    int? dbId = await dbMaster.ExecuteScalarAsync<int?>(dbIdQuery, new { DbName = currentDbName });

            //    if (!dbId.HasValue)
            //    {
            //        return BadRequest("Database ID not found in project_databases table.");
            //    }

            //    // Get projects based on the retrieved db_id
            //    string projectQuery = "SELECT DISTINCT id AS Value, name AS Text FROM projects WHERE db_id = @DbId";
            //    var projectData = (await dbMaster.QueryAsync<DTODropdown>(projectQuery, new { DbId = dbId })).Distinct().ToList();

            //    ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
            //}
            var model = new FaultyMeterViewModel();
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> CreateFaultyMetersComplaint(FaultyMeterViewModel model)
        {
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
            string checkQuery = @"
            SELECT COUNT(*) 
            FROM meters_faulty 
            WHERE msn = @MSN AND status IN (0,1,2)";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    using (MySqlCommand command = new MySqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MSN", model.MSN);
                        connection.Open();
                        var existingFaultyCount = Convert.ToInt32(await command.ExecuteScalarAsync());

                        if (existingFaultyCount > 0)
                        {
                            ModelState.AddModelError(string.Empty, "Faulty Meter List have already this MSN.");
                            return View(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error while checking existing Faulty Meters.");
            }
            int assignedId = 0;
            string project;
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string query = "SELECT id FROM user WHERE user_role = 9";
                assignedId = await db.QuerySingleOrDefaultAsync<int>(query);
                project = await db.QueryFirstOrDefaultAsync<string>(
                  "SELECT project_id FROM meters WHERE meter_msn = @MSN",
                  new { MSN = model.MSN });
            }
            model.Project = project;

            model.OpenedAt = DateTime.Now;
            model.OpenedBy = userId.Value;
            model.AssignedTo = assignedId;

            if (ModelState.IsValid)
            {
                string insertQuery = @"
                INSERT INTO meters_faulty (msn, opened_at, opened_by, open_description,assigned_to,data_reset,project_id)
                VALUES (@MSN, @OpenedAt, @OpenedBy, @OpenDescription, @AssignedTo,@DataReset,@Project)";

                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await db.ExecuteAsync(insertQuery, new
                        {
                            model.MSN,
                            model.OpenedAt,
                            model.OpenedBy,
                            model.OpenDescription,
                            model.AssignedTo,
                            model.Project,
                            model.DataReset
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return StatusCode(500, "Internal server error.");
                }
                return RedirectToAction("ViewFaultyMeters");
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetMSNs(string query)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string msnQuery = "SELECT Id as Value, meter_msn as Text FROM meters WHERE meter_msn LIKE @SearchTerm LIMIT 10";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery, new { SearchTerm = "%" + query + "%" });
                return Json(msnData);
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetSubDivByMSN(int msnId)
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                //string query = @"
                //SELECT i.m_sub_div, s.name AS subDivName, i.m_sub_div AS subDivCode
                //FROM installations i
                //LEFT JOIN survey_hesco_subdivision s ON i.m_sub_div = s.sub_div_code
                //WHERE i.Id = @msnId";
                string query = @"
                SELECT sub_div AS subDivCode
                FROM meters
                WHERE id = @msnId";

                var result = await db.QueryFirstOrDefaultAsync(query, new { msnId = msnId });

                if (result != null)
                {
                    return Json(new { subDivCode = result.subDivCode });
                }
                else
                {
                    return Json(new { subDivCode = "" });
                }
            }
        }
        [AuthorizeUserEx]
        [HttpGet]
        public async Task<IActionResult> ReceivedMetersReceipt()
        {
            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                string msnQuery = "SELECT DISTINCT msn AS value, msn AS text FROM meters_faulty WHERE received=0";
                var msnData = await db.QueryAsync<DTODropdown>(msnQuery);
                ViewBag.SelectMSN = new SelectList(msnData.Distinct(), "Value", "Text");
            }
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ReceivedMetersReceipt(MetersReceiptModel model, string[] MSNs)
        {
            if (MSNs != null && MSNs.Length > 0)
            {
                model.ReceivedDate = DateTime.Now;

                try
                {
                    using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        string updateQuery = @"
                        UPDATE meters_faulty
                        SET 
                            received = 1,
                            received_date = @ReceivedDate
                        WHERE
                            msn = @MSN";

                        foreach (var msn in MSNs)
                        {
                            await db.ExecuteAsync(updateQuery, new
                            {
                                ReceivedDate = model.ReceivedDate,
                                MSN = msn
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return StatusCode(500, "Internal server error.");
                }

                return RedirectToAction("ViewFaultyMeters");
            }

            return View(model);
        }


        public async Task<IActionResult> ExportFaultyMeterToExcel(string? fmsn, string? fat, string? issueDescription, string? fReportedBy, string? status, DateTime? executedDate, string? fdataReset, string? freceived, int? page, int? pageSize)
        {
            string fmsnCondition = !string.IsNullOrEmpty(fmsn) ? $"mf.msn IN ({fmsn})" : "1=1";
            string fatCondition = !string.IsNullOrEmpty(fat) ? $"mf.assigned_to IN ({fat})" : "1=1";
            string fReportedBycondition = !string.IsNullOrEmpty(fReportedBy) ? $"mf.opened_by IN ({fReportedBy})" : "1=1";
            //string fsdnCondition = !string.IsNullOrEmpty(fsdn) ? $"mf.sub_div IN ({fsdn})" : "1=1";
            string issueDescriptionCondition = !string.IsNullOrEmpty(issueDescription) ? $"mf.open_description LIKE '%{issueDescription}%'" : "1=1";
            string statusCondition = !string.IsNullOrEmpty(status) ? $"mf.status IN ({status})" : "1=1";
            string dataResetCondition = !string.IsNullOrEmpty(fdataReset) ? $"mf.data_reset IN ({fdataReset})" : "1=1";
            string executedDateCondition = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
            string ReceivedCondition = !string.IsNullOrEmpty(freceived) ? $"mf.received IN ({freceived})" : "1=1";

            string FaultyMetersGeneralQuery = $@"
                SELECT
                    {FaultyMetersSelectQuery}
                FROM meters_faulty mf
                    LEFT JOIN user u1 ON mf.opened_by = u1.Id
                    LEFT JOIN user u2 ON mf.assigned_to = u2.Id
                    LEFT JOIN 
                    survey_hesco_subdivision s ON mf.sub_div = s.sub_div_code
                    LEFT JOIN meter_complaint c ON mf.msn = c.meter_msn
                WHERE
                    {fmsnCondition} AND
                    {fatCondition} AND
                    {issueDescriptionCondition} AND
                    {statusCondition} AND
                    {executedDateCondition} AND
                    {fReportedBycondition} AND 
                    {dataResetCondition} AND
                    {ReceivedCondition}
                ORDER BY 
                    mf.data_reset ASC";

            using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var fmsnA = string.IsNullOrEmpty(fmsn) ? "1" : fmsn;
                //var fsdnA = string.IsNullOrEmpty(fsdn) ? "1" : fsdn;
                var fReportedByA = string.IsNullOrEmpty(fReportedBy) ? "1" : fReportedBy;
                var fatA = string.IsNullOrEmpty(fat) ? "1" : fat;
                var statusA = string.IsNullOrEmpty(status) ? "1" : status;
                var fdataResetA = string.IsNullOrEmpty(fdataReset) ? "1" : fdataReset;
                var executedDateA = executedDate.HasValue ? $"mf.opened_at >= '{executedDate.Value:yyyy-MM-dd} 00:00:00' AND mf.opened_at <= '{executedDate.Value:yyyy-MM-dd} 23:59:59'" : "1=1";
                var issueDescriptionA = string.IsNullOrEmpty(issueDescription) ? "1" : issueDescription;
                var freceivedA = string.IsNullOrEmpty(freceived) ? "1" : freceived;

                var FaultyMeterQueryFinal = string.Format(FaultyMetersGeneralQuery, fmsnA, fatA, issueDescriptionA, fReportedByA, statusA, executedDateA, fdataResetA, freceivedA);
                var faultyMeters = await db.QueryAsync<FaultyMeterModel>(FaultyMeterQueryFinal, new { fmsn, fat, status, fReportedBy, executedDate, issueDescription, pageSize, fdataReset, freceived });
                var fileDownloadName = "FaultyMeterList.xlsx";

                // Define a custom folder to save the file (you can choose a folder on the D drive or any location accessible to your application)
                var downloadFolderPath = Path.Combine("D:", "MyAppDownloads");  // Or specify your own folder path

                // Ensure the folder exists
                if (!Directory.Exists(downloadFolderPath))
                {
                    Directory.CreateDirectory(downloadFolderPath);
                }

                using (var package = CreateFaultyMeterExcelPackage(faultyMeters))
                {
                    var filePath = Path.Combine(downloadFolderPath, fileDownloadName);
                    package.SaveAs(new FileInfo(filePath)); // Save file to the custom location
                                                            // Now return the file to the browser for inline display
                    var fileBytes = System.IO.File.ReadAllBytes(filePath);
                    return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileDownloadName);
                }
                //var fileDownloadName = "FaultyMeterList.xlsx";

                //using (var package = CreateFaultyMeterExcelPackage(faultyMeters))
                //{
                //    package.SaveAs(new FileInfo(Path.Combine(_webHostEnvironment.WebRootPath, fileDownloadName)));
                //}
                //return File($"~/{fileDownloadName}", XlsxContentType, fileDownloadName);
            }
        }
        private ExcelPackage CreateFaultyMeterExcelPackage(IEnumerable<FaultyMeterModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            var package = new ExcelPackage();
            package.Workbook.Properties.Title = "FAULTY_METER LIST";
            package.Workbook.Properties.Author = "";
            package.Workbook.Properties.Subject = "FAULTY_METER LIST";

            var worksheet = package.Workbook.Worksheets.Add("FAULTY_METER LIST");

            int row = 1;
            worksheet.Cells[row, 1].Value = $"FAULTY_METER LIST                                           DATE: {DateTime.Now:dd-MM-yyyy}";
            worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells[row, 1, row, 5].Merge = true;

            row++;

            // Header row

            worksheet.Cells[row, 1].Value = "MSN";
            worksheet.Cells[row, 2].Value = "Reported By";
            worksheet.Cells[row, 3].Value = "DESCRIPTION"; ;
            worksheet.Cells[row, 4].Value = "STATUS";
            worksheet.Cells[row, 5].Value = "Assigned To";
            worksheet.Cells[row, 1, row, 5].Style.Font.Bold = true;
            row++;



            foreach (var item in data)
            {
                worksheet.Cells[row, 1].Value = item.MSN;
                worksheet.Cells[row, 2].Value = item.OpenedBy;
                worksheet.Cells[row, 3].Value = item.Remarks;
                worksheet.Cells[row, 4].Value = item.StatusDisplay;
                worksheet.Cells[row, 5].Value = item.AssignedTo;

                // Apply borders around each row
                var borderCells = worksheet.Cells[row, 1, row, 5];
                borderCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                borderCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                row++;
            }

            // Apply borders around the entire table
            var fullTable = worksheet.Cells[1, 1, row - 1, 5];
            fullTable.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            fullTable.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

            // AutoFitColumns
            worksheet.Cells[1, 1, row, 5].AutoFitColumns();

            return package;
        }

    }
}