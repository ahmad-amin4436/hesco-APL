using Dapper;
using HESCO.DAL;
using HESCO.Models;
using HESCO.Models.Projects;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis;
using MySql.Data.MySqlClient;
using Mysqlx.Expr;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Data;
using System.Drawing;
using System.Linq;

namespace HESCO.Controllers
{
    [AuthorizeUserEx]
    public class SimsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly SimsManagementDAL _dal;
        public SimsController(IConfiguration configuration, SimsManagementDAL dal)
        {
            _configuration = configuration;
            _dal = dal;
        }
        #region General Methods
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMeterModel(int projectId)
        {
            var (success, models, error) = await _dal.GetMeterModel(projectId);

            if (success)
            {
                return Json(new
                {
                    success = true,
                    models = models
                });
            }

            return Json(new { success = false, error = error });
        }

        [HttpPost]
        private IActionResult RedirectToLogin()
        {
            string unauthorizedMessage = "User is not authenticated. Redirecting to login page...";
            string loginUrl = Url.Action("LoginUser", "Account");

            string htmlContent = $@"
        <html>
            <body>
                <h3>{unauthorizedMessage}</h3>
                <script>
                    setTimeout(function() {{
                        window.location.href = '{loginUrl}';
                    }}, 1000);
                </script>
            </body>
        </html>";

            return Content(htmlContent, "text/html");
        }

        #endregion

        #region ExportIMEI
        public async Task<IActionResult> ExportIMEIToExcel(string exportType,
       string fimei = null, string fproject = null, string fchangeProject = null,
            string fstatus = null, string fuploadedBy = null, string fupdatedBy = null,
            string fuploadedAt = null, string fupdatedAt = null, string fmapDateTime = null,
            string fchangeProjectDate = null, string fissuedTo = null, string fsimStatus = null, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                // For "all" pages, ignore pagination parameters and get all data
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize; // 0 means get all data
                dynamic result = await _dal.GetIMEIDataFromDatabaseCrossDB(
                                 fimei, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                                  fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, page, pageSize, Request.Query["draw"].FirstOrDefault() ?? "1"
                              );
                var fileName = exportType == "all" ? "IMEI_Data_All_Pages" : "IMEI_Data_Current_Page";

                byte[] fileBytes = GenerateIMEIExcelFile(((IEnumerable<IMEIDataViewModel>)result.data).ToList());
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        public byte[] GenerateIMEIExcelFile(List<IMEIDataViewModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("IMEI Data");

            // Header styling
            using (var headerRange = worksheet.Cells[1, 1, 1, 7])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                headerRange.Style.Font.Color.SetColor(Color.White);
                headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Headers
            worksheet.Cells[1, 1].Value = "IMEI";
            worksheet.Cells[1, 2].Value = "Project Name";
            worksheet.Cells[1, 3].Value = "Change Project Name";
            worksheet.Cells[1, 4].Value = "Created By";
            worksheet.Cells[1, 5].Value = "Created At";
            worksheet.Cells[1, 6].Value = "Map DateTime";
            worksheet.Cells[1, 7].Value = "Change Project Date";

            // Data
            for (int i = 0; i < data.Count; i++)
            {
                var row = data[i];
                var rowIndex = i + 2;

                worksheet.Cells[rowIndex, 1].Value = row.imei ?? "-";
                worksheet.Cells[rowIndex, 2].Value = row.project_name ?? "-";
                worksheet.Cells[rowIndex, 3].Value = row.change_project_name ?? "-";
                worksheet.Cells[rowIndex, 4].Value = row.uploaded_by_username ?? "-";
                worksheet.Cells[rowIndex, 5].Value = FormatDateForExcelNullable(row.uploaded_at);
                worksheet.Cells[rowIndex, 6].Value = FormatDateForExcelNullable(row.map_datetime);
                worksheet.Cells[rowIndex, 7].Value = FormatDateForExcelNullable(row.change_project_id_at);


                // Alternate row coloring
                if (i % 2 == 0)
                {
                    using var rowRange = worksheet.Cells[rowIndex, 1, rowIndex, 7];
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                }
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders to all cells
            using var dataRange = worksheet.Cells[1, 1, data.Count + 1, 7];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            return package.GetAsByteArray();
        }
        private string FormatDateForExcelNullable(DateTime? dt)
        {
            if (!dt.HasValue || dt.Value == DateTime.MinValue)
                return "-";

            return dt.Value.ToString("yyyy-MM-dd HH:mm");
        }

        public async Task<IActionResult> ExportIMEIToPDF(string exportType,
            string fimei = null, string fproject = null, string fchangeProject = null,
            string fstatus = null, string fuploadedBy = null, string fupdatedBy = null,
            string fuploadedAt = null, string fupdatedAt = null, string fmapDateTime = null,
            string fchangeProjectDate = null, string fissuedTo = null, string fsimStatus = null, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize; // 0 means get all data
                dynamic result = await _dal.GetIMEIDataFromDatabaseCrossDB(
                                 fimei, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                                  fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, page, pageSize, Request.Query["draw"].FirstOrDefault() ?? "1"
                              );
                var fileName = exportType == "all" ? "IMEI_Data_All_Pages" : "IMEI_Data_Current_Page";

                byte[] fileBytes = GenerateIMEIPdfFile(((IEnumerable<IMEIDataViewModel>)result.data).ToList());
                return File(fileBytes,
                    "application/pdf",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }
        public byte[] GenerateIMEIPdfFile(List<IMEIDataViewModel> data)
        {
            using var memoryStream = new MemoryStream();

            // PDF document
            iTextSharp.text.Document document = new iTextSharp.text.Document(PageSize.A4.Rotate(), 10f, 10f, 10f, 10f);
            PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // ======== Styles ========
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DARK_GRAY);
            var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

            BaseColor headerBackground = new BaseColor(70, 130, 180);
            BaseColor alternateRowBg = new BaseColor(240, 240, 240);
            BaseColor whiteBg = BaseColor.WHITE;

            // ======== Title ========
            Paragraph title = new Paragraph("IMEI Data Export", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };
            document.Add(title);

            // ======== Export Info ========
            Paragraph exportInfo = new Paragraph(
                $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm} | Total Records: {data.Count}",
                infoFont)
            {
                Alignment = Element.ALIGN_LEFT,
                SpacingAfter = 15f
            };
            document.Add(exportInfo);

            // ======== Table ========
            string[] headers =
            {
        "IMEI", "Project Name", "Change Project Name",
        "Created By", "Created At", "Map DateTime", "Change Project Date"
    };

            PdfPTable table = new PdfPTable(headers.Length)
            {
                WidthPercentage = 100,
                SpacingBefore = 10f,
                SpacingAfter = 10f
            };

            // Set equal column widths
            table.SetWidths(Enumerable.Repeat(2f, headers.Length).ToArray());

            // ======== Add Headers ========
            foreach (var header in headers)
            {
                table.AddCell(CreateCell(header, headerFont, headerBackground, Element.ALIGN_CENTER));
            }

            // ======== Add Rows ========
            for (int i = 0; i < data.Count; i++)
            {
                var r = data[i];
                var bg = (i % 2 == 1) ? alternateRowBg : whiteBg;

                table.AddCell(CreateCell(r.imei));
                table.AddCell(CreateCell(r.project_name));
                table.AddCell(CreateCell(r.change_project_name));
                table.AddCell(CreateCell(r.uploaded_by_username));
                table.AddCell(CreateCell(FormatDate(r.uploaded_at)));
                table.AddCell(CreateCell(FormatDate(r.map_datetime)));
                table.AddCell(CreateCell(FormatDate(r.change_project_id_at)));
            }

            document.Add(table);
            document.Close();

            return memoryStream.ToArray();


            // ======== Helper Methods ========

            PdfPCell CreateCell(string text, Font font = null, BaseColor bg = null, int align = Element.ALIGN_LEFT)
            {
                return new PdfPCell(new Phrase(text ?? "-", font ?? cellFont))
                {
                    BackgroundColor = bg ?? whiteBg,
                    HorizontalAlignment = align,
                    Padding = 5f
                };
            }

            string FormatDate(DateTime? date)
            {
                return date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm") : "-";
            }
        }

        private void AddTableCell(iTextSharp.text.pdf.PdfPTable table, string content, iTextSharp.text.Font font, iTextSharp.text.BaseColor backgroundColor)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(content, font))
            {
                BackgroundColor = backgroundColor,
                HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT,
                Padding = 4f,
                PaddingLeft = 6f
            };
            table.AddCell(cell);
        }

       
        #endregion

        #region ExportIMSI
        public async Task<IActionResult> ExportIMSIToExcel(string exportType,
       string fimsi = null, string fsimNumber = null, string foperator = null,
            string fproject = null, string fchangeProject = null,
            string fstatus = null, string fcreatedBy = null, string fupdatedBy = null,
            string fcreatedAt = null, string fupdatedAt = null, string fmapDateTime = null,
            string fchangeProjectDate = null, string fissuedTo = null, string fsimStatus = null, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                // For "all" pages, ignore pagination parameters and get all data
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize; // 0 means get all data

                var result = await _dal.GetIMSIDataCrossDB(
                   fimsi: fimsi,
                   fsimNumber: fsimNumber,
                   foperator: foperator,
                   fproject: fproject,
                   fchangeProject: fchangeProject,
                   fstatus: fstatus,
                   fcreatedBy: fcreatedBy,
                   fupdatedBy: fupdatedBy,
                   fcreatedAt: fcreatedAt,
                   fupdatedAt: fupdatedAt,
                   fmapDateTime: fmapDateTime,
                   fchangeProjectDate: fchangeProjectDate,
                   fissuedTo: fissuedTo,
                   fsimStatus: fsimStatus,
                   fpage: page,
                   fpageSize: pageSize
               );
                var fileName = exportType == "all" ? "IMSI_Data_All_Pages" : "IMSI_Data_Current_Page";

                byte[] fileBytes = GenerateIMSIExcelFile(result.data.ToList());
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        public byte[] GenerateIMSIExcelFile(List<IMSIDataViewModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("IMSI Data");

            // Header styling
            using (var headerRange = worksheet.Cells[1, 1, 1, 10])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                headerRange.Style.Font.Color.SetColor(Color.White);
                headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Headers
            worksheet.Cells[1, 1].Value = "IMSI";
            worksheet.Cells[1, 2].Value = "SIM Number";
            worksheet.Cells[1, 3].Value = "Operator";
            worksheet.Cells[1, 4].Value = "Project Name";
            worksheet.Cells[1, 5].Value = "Change Project Name";
            worksheet.Cells[1, 6].Value = "Issued To";
            worksheet.Cells[1, 7].Value = "Created By";
            worksheet.Cells[1, 8].Value = "Created At";
            worksheet.Cells[1, 9].Value = "Map DateTime";
            worksheet.Cells[1, 10].Value = "Change Project Date";

            // Data
            if (data != null && data.Count > 0)
            {
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var rowIndex = i + 2;

                    worksheet.Cells[rowIndex, 1].Value = row.imsi ?? "-";
                    worksheet.Cells[rowIndex, 2].Value = row.sim_number ?? "-";
                    worksheet.Cells[rowIndex, 3].Value = row.operator_name ?? "-";
                    worksheet.Cells[rowIndex, 4].Value = row.project_name ?? "-";
                    worksheet.Cells[rowIndex, 5].Value = row.change_project_name ?? "-";
                    worksheet.Cells[rowIndex, 6].Value = row.issued_to?.ToString() ?? "-";
                    worksheet.Cells[rowIndex, 7].Value = row.created_by_username?.ToString() ?? "-";
                    worksheet.Cells[rowIndex, 8].Value = FormatDateForExcel(row.created_at);
                    worksheet.Cells[rowIndex, 9].Value = FormatDateForExcel(row.map_datetime);
                    worksheet.Cells[rowIndex, 10].Value = FormatDateForExcel(row.change_project_id_at, true);

                    // Alternate row coloring
                    if (i % 2 == 0)
                    {
                        using var rowRange = worksheet.Cells[rowIndex, 1, rowIndex, 10];
                        rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        rowRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    }
                }

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Add borders to all cells
                using var dataRange = worksheet.Cells[1, 1, data.Count + 1, 10];
                dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }
            else
            {
                // Handle empty data gracefully
                worksheet.Cells[2, 1].Value = "No data available";
            }

            return package.GetAsByteArray();
        }

        private string FormatDateForExcel(DateTime? date, bool dateOnly = false)
        {
            if (!date.HasValue || date.Value.Year == 1)
                return "-";

            return dateOnly ? date.Value.ToString("yyyy-MM-dd") : date.Value.ToString("yyyy-MM-dd HH:mm");
        }

        private string FormatDateForExcel(string dateString, bool dateOnly = false)
        {
            if (string.IsNullOrEmpty(dateString) || dateString.StartsWith("0001-01-01"))
                return "-";

            if (DateTime.TryParse(dateString, out DateTime date))
            {
                return dateOnly ? date.ToString("yyyy-MM-dd") : date.ToString("yyyy-MM-dd HH:mm");
            }

            return "-";
        }

        public async Task<IActionResult> ExportIMSIToPDF(string exportType,
            string fimsi = null,string fsimNumber = null,string foperator = null,
            string fproject = null, string fchangeProject = null,
            string fstatus = null, string fcreatedBy = null, string fupdatedBy = null,
            string fcreatedAt = null, string fupdatedAt = null, string fmapDateTime = null,
            string fchangeProjectDate = null, string fissuedTo = null, string fsimStatus = null, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                // For "all" pages, ignore pagination parameters and get all data
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize;

                var result = await _dal.GetIMSIDataCrossDB(
                    fimsi: fimsi,
                    fsimNumber: fsimNumber,
                    foperator: foperator,
                    fproject: fproject,
                    fchangeProject: fchangeProject,
                    fstatus: fstatus,
                    fcreatedBy: fcreatedBy,
                    fupdatedBy: fupdatedBy,
                    fcreatedAt: fcreatedAt,
                    fupdatedAt: fupdatedAt,
                    fmapDateTime: fmapDateTime,
                    fchangeProjectDate: fchangeProjectDate,
                    fissuedTo: fissuedTo,
                    fsimStatus: fsimStatus,
                    fpage: page,
                    fpageSize: pageSize
                );

                var fileName = exportType == "all" ? "IMSI_Data_All_Pages" : "IMSI_Data_Current_Page";

                byte[] fileBytes = GenerateIMSIPdfFile(result.data.ToList());
                return File(fileBytes,
                    "application/pdf",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }
        public byte[] GenerateIMSIPdfFile(List<IMSIDataViewModel> data)
        {
            using var memoryStream = new MemoryStream();

            // PDF document
            iTextSharp.text.Document document = new iTextSharp.text.Document(PageSize.A4.Rotate(), 10f, 10f, 10f, 10f);
            PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // ======== Styles ========
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DARK_GRAY);
            var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

            BaseColor headerBackground = new BaseColor(70, 130, 180);
            BaseColor alternateRowBg = new BaseColor(240, 240, 240);
            BaseColor whiteBg = BaseColor.WHITE;

            // ======== Title ========
            Paragraph title = new Paragraph("IMSI Data Export", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };
            document.Add(title);

            // ======== Export Info ========
            Paragraph exportInfo = new Paragraph(
                $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm} | Total Records: {data.Count}",
                infoFont)
            {
                Alignment = Element.ALIGN_LEFT,
                SpacingAfter = 15f
            };
            document.Add(exportInfo);

            // ======== Table ========
            string[] headers =
            {
        "IMSI", "SIM Number", "Operator", "Project Name", "Change Project Name",
        "Issued To", "Created By", "Created At", "Map DateTime", "Change Project Date"
    };

            PdfPTable table = new PdfPTable(headers.Length)
            {
                WidthPercentage = 100,
                SpacingBefore = 10f,
                SpacingAfter = 10f
            };

            // Set equal column widths
            table.SetWidths(Enumerable.Repeat(2f, headers.Length).ToArray());

            // ======== Add Headers ========
            foreach (var header in headers)
            {
                table.AddCell(CreateCell(header, headerFont, headerBackground, Element.ALIGN_CENTER));
            }

            // ======== Add Rows ========
            for (int i = 0; i < data.Count; i++)
            {
                var r = data[i];
                var bg = (i % 2 == 1) ? alternateRowBg : whiteBg;

                table.AddCell(CreateCell(r.imsi));
                table.AddCell(CreateCell(r.sim_number));
                table.AddCell(CreateCell(r.operator_name));
                table.AddCell(CreateCell(r.project_name));
                table.AddCell(CreateCell(r.change_project_name));
                table.AddCell(CreateCell(r.issued_to?.ToString()));
                table.AddCell(CreateCell(r.created_by_username ?? r.created_by.ToString()));
                table.AddCell(CreateCell(FormatDate(r.created_at)));
                table.AddCell(CreateCell(FormatDate(r.map_datetime)));
                table.AddCell(CreateCell(FormatDate(r.change_project_id_at)));
            }

            document.Add(table);
            document.Close();

            return memoryStream.ToArray();


            // ======== Helper Methods ========

            PdfPCell CreateCell(string text, Font font = null, BaseColor bg = null, int align = Element.ALIGN_LEFT)
            {
                return new PdfPCell(new Phrase(text ?? "-", font ?? cellFont))
                {
                    BackgroundColor = bg ?? whiteBg,
                    HorizontalAlignment = align,
                    Padding = 5f
                };
            }

            string FormatDate(DateTime? date)
            {
                return date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm") : "-";
            }
        }

        private string FormatDateForPdf(DateTime? date, bool dateOnly = false)
        {
            if (!date.HasValue || date.Value.Year == 1)
                return "-";

            return dateOnly ? date.Value.ToString("yyyy-MM-dd") : date.Value.ToString("yyyy-MM-dd HH:mm");
        }

        private string FormatDateForPdf(string dateString, bool dateOnly = false)
        {
            if (string.IsNullOrEmpty(dateString) || dateString.StartsWith("0001-01-01"))
                return "-";

            if (DateTime.TryParse(dateString, out DateTime date))
            {
                return dateOnly ? date.ToString("yyyy-MM-dd") : date.ToString("yyyy-MM-dd HH:mm");
            }

            return "-";
        }

        #endregion

        #region ExportBarcode
        public async Task<IActionResult> ExportBarcodeToExcel(string exportType,
      string foptocomcode, string fproject, string fchangeProject, string fstatus,
  string fuploadedBy, string fupdatedBy, string fuploadedAt,
  string fupdatedAt, string fmapDateTime, string fchangeProjectDate, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                // For "all" pages, ignore pagination parameters and get all data
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize; // 0 means get all data
                dynamic result = await _dal.GetBarcodeData(
                                 foptocomcode, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                                  fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, page, pageSize, Request.Query["draw"].FirstOrDefault() ?? "1"
                              );
                var fileName = exportType == "all" ? "Barcode_Data_All_Pages" : "Barcode_Data_Current_Page";

                byte[] fileBytes = GenerateBarcodeExcelFile(((IEnumerable<BarcodeDataViewModel>)result.data).ToList());
                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        public byte[] GenerateBarcodeExcelFile(List<BarcodeDataViewModel> data)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Accurate");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Barcode Data");

            // Header styling
            using (var headerRange = worksheet.Cells[1, 1, 1, 7])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                headerRange.Style.Font.Color.SetColor(Color.White);
                headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Headers
            worksheet.Cells[1, 1].Value = "Optocom Code";
            worksheet.Cells[1, 2].Value = "Project Name";
            worksheet.Cells[1, 3].Value = "Change Project Name";
            worksheet.Cells[1, 4].Value = "Created By";
            worksheet.Cells[1, 5].Value = "Created At";
            worksheet.Cells[1, 6].Value = "Map DateTime";
            worksheet.Cells[1, 7].Value = "Change Project Date";

            // Data
            for (int i = 0; i < data.Count; i++)
            {
                var row = data[i];
                var rowIndex = i + 2;

                worksheet.Cells[rowIndex, 1].Value = row.serial_no ?? "-";
                worksheet.Cells[rowIndex, 2].Value = row.project_name ?? "-";
                worksheet.Cells[rowIndex, 3].Value = row.change_project_name ?? "-";
                worksheet.Cells[rowIndex, 4].Value = row.created_by_username ?? "-";
                worksheet.Cells[rowIndex, 5].Value = FormatDateForExcelNullable(row.created_at);
                worksheet.Cells[rowIndex, 6].Value = FormatDateForExcelNullable(row.map_datetime);
                worksheet.Cells[rowIndex, 7].Value = FormatDateForExcelNullable(row.change_project_id_at);


                // Alternate row coloring
                if (i % 2 == 0)
                {
                    using var rowRange = worksheet.Cells[rowIndex, 1, rowIndex, 7];
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                }
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders to all cells
            using var dataRange = worksheet.Cells[1, 1, data.Count + 1, 7];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            return package.GetAsByteArray();
        }
    
        public async Task<IActionResult> ExportBarcodeToPDF(string exportType,
      string foptocomcode, string fproject, string fchangeProject, string fstatus,
  string fuploadedBy, string fupdatedBy, string fuploadedAt,
  string fupdatedAt, string fmapDateTime, string fchangeProjectDate, int fpage = 1, int fpageSize = 50)
        {
            try
            {
                int page = exportType == "all" ? 1 : fpage;
                int pageSize = exportType == "all" ? int.MaxValue : fpageSize; // 0 means get all data
                dynamic result = await _dal.GetBarcodeData(
                                 foptocomcode, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                                  fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, page, pageSize, Request.Query["draw"].FirstOrDefault() ?? "1"
                              );
                var fileName = exportType == "all" ? "Barcod_Data_All_Pages" : "Barcod_Data_Current_Page";

                byte[] fileBytes = GenerateBarcodePdfFile(((IEnumerable<BarcodeDataViewModel>)result.data).ToList());
                return File(fileBytes,
                    "application/pdf",
                    $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }
        public byte[] GenerateBarcodePdfFile(List<BarcodeDataViewModel> data)
        {
            using var memoryStream = new MemoryStream();

            // PDF document
            iTextSharp.text.Document document = new iTextSharp.text.Document(PageSize.A4.Rotate(), 10f, 10f, 10f, 10f);
            PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // ======== Styles ========
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DARK_GRAY);
            var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

            BaseColor headerBackground = new BaseColor(70, 130, 180);
            BaseColor alternateRowBg = new BaseColor(240, 240, 240);
            BaseColor whiteBg = BaseColor.WHITE;

            // ======== Title ========
            Paragraph title = new Paragraph("Barcode Data Export", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20f
            };
            document.Add(title);

            // ======== Export Info ========
            Paragraph exportInfo = new Paragraph(
                $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm} | Total Records: {data.Count}",
                infoFont)
            {
                Alignment = Element.ALIGN_LEFT,
                SpacingAfter = 15f
            };
            document.Add(exportInfo);

            // ======== Table ========
            string[] headers =
            {
        "Optocom Code", "Project Name", "Change Project Name",
        "Created By", "Created At", "Map DateTime", "Change Project Date"
    };

            PdfPTable table = new PdfPTable(headers.Length)
            {
                WidthPercentage = 100,
                SpacingBefore = 10f,
                SpacingAfter = 10f
            };

            // Set equal column widths
            table.SetWidths(Enumerable.Repeat(2f, headers.Length).ToArray());

            // ======== Add Headers ========
            foreach (var header in headers)
            {
                table.AddCell(CreateCell(header, headerFont, headerBackground, Element.ALIGN_CENTER));
            }

            // ======== Add Rows ========
            for (int i = 0; i < data.Count; i++)
            {
                var r = data[i];
                var bg = (i % 2 == 1) ? alternateRowBg : whiteBg;

                table.AddCell(CreateCell(r.serial_no));
                table.AddCell(CreateCell(r.project_name));
                table.AddCell(CreateCell(r.change_project_name));
                table.AddCell(CreateCell(r.created_by_username));
                table.AddCell(CreateCell(FormatDate(r.created_at)));
                table.AddCell(CreateCell(FormatDate(r.map_datetime)));
                table.AddCell(CreateCell(FormatDate(r.change_project_id_at)));
            }

            document.Add(table);
            document.Close();

            return memoryStream.ToArray();


            // ======== Helper Methods ========

            PdfPCell CreateCell(string text, Font font = null, BaseColor bg = null, int align = Element.ALIGN_LEFT)
            {
                string safeText = string.IsNullOrWhiteSpace(text) ? "-" : text;

                return new PdfPCell(new Phrase(safeText, font ?? cellFont))
                {
                    BackgroundColor = bg ?? whiteBg,
                    HorizontalAlignment = align,
                    Padding = 5f
                };
            }


            string FormatDate(DateTime? date)
            {
                if (date == null || date.Value == DateTime.MinValue)
                    return "-";

                return date.Value.ToString("yyyy-MM-dd HH:mm");
            }


        }


        #endregion

        #region IMEI
        public async Task<IActionResult> ImportIMEI()
        {
            try
            {
                var (projectData, error) = await _dal.GetProjectsForCurrentDB();

                if (!string.IsNullOrEmpty(error))
                {
                    return BadRequest($"An error occurred: {error}");
                }

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
                return View();
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }
        [HttpPost]
        public async Task<IActionResult> ImportIMEI(IMEIData imeiData, IFormFile file)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return RedirectToAction("ImportIMEI", new { error = "No file selected" });
                }

                // Validate user authentication
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return RedirectToLogin();
                }

                // Validate project selection
                if (string.IsNullOrWhiteSpace(imeiData.Project))
                {
                    return RedirectToAction("ImportIMEI", new { error = "Please select a project" });
                }

                var UserId = userId.Value;
                var processedIMEIs = new HashSet<string>();

                ExcelPackage.License.SetNonCommercialOrganization("Accurate");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.First();
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++) // Skip header row
                {
                    var imei = worksheet.Cells[row, 1].Text?.Trim();

                    if (!string.IsNullOrWhiteSpace(imei) && !processedIMEIs.Contains(imei))
                    {
                        processedIMEIs.Add(imei);
                        await _dal.ImportIMEIData(imei, userId, imeiData.Project);
                    }
                }

                // Bulk import using DAL

                return RedirectToAction("ViewIMEIList");
            }
            catch (Exception ex)
            {
                return RedirectToAction("ImportIMEI", new { error = "An error occurred during import" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetIMEIDetails(int id)
        {
            try
            {
                var imei = await _dal.GetIMEIById(id);

                if (imei == null)
                {
                    return Json(new { success = false, message = "IMEI not found" });
                }

                return Json(new { success = true, data = imei });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> DeleteIMEI(int id)
        {
            try
            {
                var result = await _dal.DeleteIMEI(id);

                return Json(new { success = result.success, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        public async Task<JsonResult> LoadIMEISearchDDL(string suggestionType)
        {
            if (string.IsNullOrWhiteSpace(suggestionType))
                return Json(new { success = false, message = "Suggestion type is required." });
            try
            {
                var suggestions = await _dal.LoadIMEISearchDDL(suggestionType);

                return Json(new { success = true, data = suggestions.ToList() });
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error for invalid type
            {
                return Json(new { success = false, message = "Invalid suggestion type." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult ViewIMEIList()
        {
            return View();
        }
        public IActionResult DownloadTemplateIMEI()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "IMEITemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "IMEITemplate.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpGet]
        public async Task<JsonResult> GetIMEIData(
      string fimei, string fproject, string fchangeProject, string fstatus,
      string fuploadedBy, string fupdatedBy, string fuploadedAt,
      string fupdatedAt, string fmapDateTime, string fchangeProjectDate,
      int fpage = 1, int fpageSize = 50, string suggestionType = null, string searchTerm = null)
        {
            try
            {
                // Handle select2 suggestions for dropdown filters
                if (!string.IsNullOrEmpty(suggestionType) && !string.IsNullOrEmpty(searchTerm))
                {
                    var suggestions = await _dal.GetIMEISuggestions(suggestionType, searchTerm);
                    return Json(suggestions);
                }

                // Handle datatable server-side processing
                var result = await _dal.GetIMEIDataFromDatabaseCrossDB(
                    fimei, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                    fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, fpage, fpageSize, Request.Query["draw"].FirstOrDefault() ?? "1");

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Convert.ToInt32(Request.Query["draw"].FirstOrDefault() ?? "1"),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        #endregion

        #region IMSI

        public async Task<IActionResult> ImportIMSI()
        {
            try
            {
                var (projectData, error) = await _dal.GetImportIMSIData();

                if (!string.IsNullOrEmpty(error))
                {
                    return BadRequest($"An error occurred: {error}");
                }

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
                return View();
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }
        [HttpPost]
        public async Task<IActionResult> ImportIMSI(IMSIData imsiData, IFormFile file)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return RedirectToAction("ImportIMSI", new { error = "No file selected" });
                }

                // Validate user authentication
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return RedirectToLogin();
                }

                // Validate model and required fields
                if (string.IsNullOrWhiteSpace(imsiData.Project))
                {
                    return RedirectToAction("ImportIMSI", new { error = "Please select project and meter type" });
                }

                var UserId = userId.Value;
                var processedIMSIs = new HashSet<string>();

                ExcelPackage.License.SetNonCommercialOrganization("Accurate");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.First();
                var rowCount = worksheet.Dimension.Rows;

                using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                for (int row = 2; row <= rowCount; row++) // Skip header row
                {
                    var imsi = worksheet.Cells[row, 1].Text?.Trim();
                    var simNumber = worksheet.Cells[row, 2].Text?.Trim();
                    var operatorName = worksheet.Cells[row, 3].Text?.Trim();
                    var monthlyBill = worksheet.Cells[row, 4].Text?.Trim();
                    var dataDetails = worksheet.Cells[row, 5].Text?.Trim();

                    // Check if all required fields are present and not duplicate
                    if (!string.IsNullOrWhiteSpace(imsi) &&
                        !string.IsNullOrWhiteSpace(simNumber) &&
                        !string.IsNullOrWhiteSpace(operatorName) &&
                        !string.IsNullOrWhiteSpace(monthlyBill) &&
                        !string.IsNullOrWhiteSpace(dataDetails) &&
                        !processedIMSIs.Contains(imsi))
                    {
                        processedIMSIs.Add(imsi);
                        if (!string.IsNullOrWhiteSpace(imsiData.MeterType))
                        {
                            await _dal.ImportIMSIData(imsi, simNumber, operatorName, monthlyBill,
                                dataDetails, UserId, imsiData.Project, imsiData.MeterType);
                        }
                        else
                        {
                            await _dal.ImportIMSIData(imsi, simNumber, operatorName, monthlyBill,
                              dataDetails, UserId, imsiData.Project,"");
                        }
                    }
                }

                return RedirectToAction("ViewIMSIList");
            }
            catch (Exception ex)
            {
                return RedirectToAction("ImportIMSI", new { error = "An error occurred during import" });
            }
        }

        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "IMSITemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "ImportIMSI.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
       

        public IActionResult ViewIMSIList()
        {
            return View();
        }
        [HttpGet]
        public async Task<JsonResult> GetIMSIData(
    string fimsi, string fsimNumber, string foperator, string fproject, string fchangeProject, string fstatus,
    string fcreatedBy, string fupdatedBy, string fcreatedAt, string fupdatedAt,
    string fmapDateTime, string fchangeProjectDate, string fissuedTo, string fsimStatus,
    int fpage = 1, int fpageSize = 50, string suggestionType = null, string searchTerm = null)
        {
            try
            {
                // Handle select2 suggestions for dropdown filters
                if (!string.IsNullOrEmpty(suggestionType) && !string.IsNullOrEmpty(searchTerm))
                {
                    var suggestions = await GetIMSISuggestions(suggestionType, searchTerm);
                    return Json(suggestions);
                }

                // Handle datatable server-side processing
                var result = await GetIMSIDataFromDatabaseCrossDB(
                    fimsi, fsimNumber, foperator, fproject, fchangeProject, fstatus, fcreatedBy, fupdatedBy,
                    fcreatedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, fissuedTo, fsimStatus, fpage, fpageSize);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Convert.ToInt32(Request.Query["draw"].FirstOrDefault() ?? "1"),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }
        public async Task<JsonResult> LoadIMSISearchDDL(string suggestionType)
        {
            if (string.IsNullOrWhiteSpace(suggestionType))
                return Json(new { success = false, message = "Suggestion type is required." });

            try
            {
                var suggestions = await _dal.LoadIMSISearchDDL(suggestionType);

                return Json(new { success = true, data = suggestions.ToList() });
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error for invalid type
            {
                return Json(new { success = false, message = "Invalid suggestion type." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<object> GetIMSISuggestions(string suggestionType, string searchTerm)
        {
            try
            {
                var suggestions = await _dal.GetIMSISuggestionsAsync(suggestionType, searchTerm);
                return suggestions.ToList();
            }
            catch (MySqlException ex) when (ex.Number == 1644)
            {
                throw new ArgumentException("Invalid suggestion type");
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while fetching suggestions: {ex.Message}");
            }
        }
        // Cross-database approach for IMSI
        private async Task<object> GetIMSIDataFromDatabaseCrossDB(
     string fimsi, string fsimNumber, string foperator, string fproject, string fchangeProject, string fstatus,
     string fcreatedBy, string fupdatedBy, string fcreatedAt, string fupdatedAt,
     string fmapDateTime, string fchangeProjectDate, string fissuedTo, string fsimStatus,
     int fpage, int fpageSize)
        {
            try
            {
                var (recordsTotal, data) = await _dal.GetIMSIDataCrossDB(
                    fimsi, fsimNumber, foperator, fproject, fchangeProject, fstatus,
                    fcreatedBy, fupdatedBy, fcreatedAt, fupdatedAt,
                    fmapDateTime, fchangeProjectDate, fissuedTo, fsimStatus,
                    fpage, fpageSize
                );

                return new
                {
                    draw = Request.Query["draw"].FirstOrDefault() ?? "1",
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsTotal,
                    data = data
                };
            }
            catch (Exception ex)
            {
                // Handle exception
                throw;
            }
        }
        [HttpGet]
        public async Task<JsonResult> GetIMSIDetails(int id)
        {
            try
            {
                var imsi = await _dal.GetIMSIById(id);

                if (imsi == null)
                {
                    return Json(new { success = false, message = "IMSI not found" });
                }

                return Json(new { success = true, data = imsi });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> DeleteIMSI(int id)
        {
            try
            {
                var result = await _dal.DeleteIMSI(id);

                return Json(new { success = result.success, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        #endregion

        #region Barcode

        public async Task<IActionResult> ImportBarcodeData()
        {
            try
            {
                var (projectData, error) = await _dal.GetProjectsForCurrentDB();

                if (!string.IsNullOrEmpty(error))
                {
                    return BadRequest($"An error occurred: {error}");
                }

                ViewBag.SelectProject = new SelectList(projectData, "Value", "Text");
                return View();
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }
        public IActionResult DownloadTemplateBarcode()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "BarcodeTemplate.xlsx");
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = "BarcodeTemplate.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        [HttpPost]
        public async Task<IActionResult> ImportBarcodeData(BarcodeData barcodeData, IFormFile file)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return RedirectToAction("ImportBarcodeData", new { error = "No file selected" });
                }

                // Validate user authentication
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return RedirectToLogin();
                }

                // Validate project selection
                if (string.IsNullOrWhiteSpace(barcodeData.Project))
                {
                    return RedirectToAction("ImportBarcodeData", new { error = "Please select a project" });
                }

                var UserId = userId.Value;
                var processedOptoComCode = new HashSet<string>();

                ExcelPackage.License.SetNonCommercialOrganization("Accurate");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.First();
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++) // Skip header row
                {
                    var OptoComCode = worksheet.Cells[row, 1].Text?.Trim();

                    if (!string.IsNullOrWhiteSpace(OptoComCode) && !processedOptoComCode.Contains(OptoComCode))
                    {
                        processedOptoComCode.Add(OptoComCode);
                        await _dal.ImportBarcodeData(OptoComCode, userId, barcodeData.Project);
                    }
                }

                // Bulk import using DAL

                return RedirectToAction("ViewBarcodeList");
            }
            catch (Exception ex)
            {
                return RedirectToAction("ImportIMEI", new { error = "An error occurred during import" });
            }
        }

        public IActionResult ViewBarcodeList()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetBarcodeData(
   string foptocomcode, string fproject, string fchangeProject, string fstatus,
   string fuploadedBy, string fupdatedBy, string fuploadedAt,
   string fupdatedAt, string fmapDateTime, string fchangeProjectDate,
   int fpage = 1, int fpageSize = 50, string suggestionType = null, string searchTerm = null)
        {
            try
            {
                // Handle select2 suggestions for dropdown filters
                if (!string.IsNullOrEmpty(suggestionType) && !string.IsNullOrEmpty(searchTerm))
                {
                    var suggestions = await _dal.GetBarcodeSuggestions(suggestionType, searchTerm);
                    return Json(suggestions);
                }

                // Handle datatable server-side processing
                var result = await _dal.GetBarcodeData(
                    foptocomcode, fproject, fchangeProject, fstatus, fuploadedBy, fupdatedBy,
                    fuploadedAt, fupdatedAt, fmapDateTime, fchangeProjectDate, fpage, fpageSize, Request.Query["draw"].FirstOrDefault() ?? "1");

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Convert.ToInt32(Request.Query["draw"].FirstOrDefault() ?? "1"),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }
        public async Task<JsonResult> LoadBarcodeSearchDDL(string suggestionType)
        {
            if (string.IsNullOrWhiteSpace(suggestionType))
                return Json(new { success = false, message = "Suggestion type is required." });
            try
            {
                var suggestions = await _dal.LoadBarcodeSearchDDL(suggestionType);

                return Json(new { success = true, data = suggestions.ToList() });
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error for invalid type
            {
                return Json(new { success = false, message = "Invalid suggestion type." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetBarcodeDetails(int id)
        {
            try
            {
                var imei = await _dal.GetBarcodeDetails(id);

                if (imei == null)
                {
                    return Json(new { success = false, message = "IMEI not found" });
                }

                return Json(new { success = true, data = imei });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> DeleteBarcode(int id)
        {
            try
            {
                var result = await _dal.DeleteBarcode(id);

                return Json(new { success = result.success, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        #endregion
    }
}
