using DinkToPdf;
using DinkToPdf.Contracts;
using HESCO;
using HESCO.Controllers;
using HESCO.Models.Complaint;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
public class PdfService
{
    private readonly IConverter _converter;
    private readonly ViewRenderService _viewRenderService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public PdfService(IConverter converter, ViewRenderService viewRenderService, EmailService emailService, IConfiguration configuration)
    {
        _converter = converter;
        _viewRenderService = viewRenderService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public byte[] GeneratePdf(string htmlContent)
    {
        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Landscape,
                PaperSize = PaperKind.A4,
            },
            Objects = {
                new ObjectSettings() {
                    PagesCount = true,
                    HtmlContent = htmlContent,
                    WebSettings = { DefaultEncoding = "utf-8" },
                    HeaderSettings = { FontName = "Arial", FontSize = 9},
                    FooterSettings = { FontName = "Arial", FontSize = 9, Right = "Page [page] of [toPage]"},
                }
            }

        };
        return _converter.Convert(doc);
    }
    public async Task<string> RenderViewAsStringAsync<TModel>(ControllerContext context, string viewName, TModel model)
    {
        return await _viewRenderService.RenderViewToStringAsync(context, viewName, model);
    }
    public async Task GeneratePdfAndSendEmail(ControllerContext context)
    {
        using (IDbConnection db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        {
            string startDateQuery = "SELECT MIN(created_date) FROM installations";
            var startDate = await db.ExecuteScalarAsync<DateTime>(startDateQuery);

            var reportQueryFinal = @"
            SELECT
               shs.circle_name,
            shs.division_name,
            shs.sub_div_code,   
            shs.name AS sub_division_name,
            COALESCE(ma.allocated, 0) AS Allocated,
            COALESCE(mr.received_qty, 0) AS Received,
            COALESCE(ti.today_installed, 0) AS Today_Installed,
            COALESCE(ti.total_installed, 0) AS Total_Installed,
            COALESCE(mt.return_qty, 0) AS Returned,
            CASE 
                WHEN ti.total_installed = 0 THEN COALESCE(ma.allocated, 0)
                ELSE COALESCE(ma.allocated, 0) - COALESCE(ti.total_installed, 0)
            END AS Balance,           
            mr.remarks 
             FROM
            survey_hesco_subdivision shs
        LEFT JOIN (
            SELECT
                sub_div,
                SUM(allocated) AS allocated           
            FROM
                meter_allocation       
            
            GROUP BY
                sub_div
        ) ma ON shs.sub_div_code = ma.sub_div
         LEFT JOIN (
            SELECT
                sub_div,
                  SUM(received_qty) AS received_qty,                            
                 GROUP_CONCAT(remarks SEPARATOR '; ') AS remarks
            FROM
               meter_received      
            
            GROUP BY
                sub_div
        ) mr ON shs.sub_div_code = mr.sub_div
        LEFT JOIN (
            SELECT
                sub_div,
                  SUM(return_qty) AS return_qty            
                
            FROM
               meter_return
            
            GROUP BY
                sub_div
        ) mt ON shs.sub_div_code = mt.sub_div
            LEFT JOIN(
                SELECT
                    m_sub_div,
                    SUM(CASE WHEN DATE(created_date) = CURDATE() THEN 1 ELSE 0 END) AS today_installed,
                    COUNT(*) AS total_installed
                FROM
                    installations
                GROUP BY
                    m_sub_div
            ) ti ON shs.sub_div_code = ti.m_sub_div
            WHERE       
                sub_div_code != 8574 AND
                allocated != 0
            GROUP BY
                shs.circle_name, shs.division_name, shs.sub_div_code, shs.name
            ORDER BY
                shs.sub_div_code";

            var reportData = await db.QueryAsync<ReportData>(reportQueryFinal, new { startDate });

            var groupedData = reportData
                .GroupBy(r => r.circle_name)
                .Select(g => new GroupedReportData
                {
                    CircleName = g.Key,
                    Items = g.ToList(),
                    TotalAllocated = g.Sum(item => item.allocated),
                    TotalReceived = g.Sum(item => item.received),
                    TotalTodayInstalled = g.Sum(item => item.today_installed),
                    TotalTotalInstalled = g.Sum(item => item.total_installed),
                    TotalBalance = g.Sum(item => item.allocated) - g.Sum(item => item.total_installed)
                })
                .ToList();

            var grandTotal = new
            {
                TotalAllocated = groupedData.Sum(g => g.TotalAllocated),
                TotalReceived = groupedData.Sum(g => g.TotalReceived),
                TotalTodayInstalled = groupedData.Sum(g => g.TotalTodayInstalled),
                TotalTotalInstalled = groupedData.Sum(g => g.TotalTotalInstalled),
                TotalBalance = groupedData.Sum(g => g.TotalAllocated) - groupedData.Sum(g => g.TotalTotalInstalled)
            };

            var viewModel = new PdfViewModel
            {
                GroupedData = groupedData,
                GrandTotal = grandTotal,
                Logo1Base64 = ConvertImageToBase64("wwwroot/assets/img/icons/Lesco.png"),
                Logo2Base64 = ConvertImageToBase64("wwwroot/assets/img/icons/logo.png")
            };

            var htmlContent = await _viewRenderService.RenderViewToStringAsync(context, "ReportPDF", viewModel);
            var pdf = GeneratePdf(htmlContent);
            var emailBody = @"
                <p>Dear Sir,</p>
                <p>Please find attached the Daily Installation Report-LESCO Project.</p>                
                <p>Regards,<br/>
                Accurate Pvt Ltd.<br/></p>
                <p style='margin-top: 20px; color: #666; text-align: center;'>
                    <span style='background: linear-gradient(to right, rgba(255,255,255,0) 0%, rgba(255,255,255,0.5) 50%, rgba(255,255,255,0) 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent;'>This report is system generated</span>
                </p>
            ";
            //var ccEmails = new List<string> { "faisal.qayyum@accurate.com.pk", "shamim.developer@accurate.com.pk" };
            var ccEmails = new List<string>
            {
                "shakeel@accurate.com.pk",
                "mueen@accurate.com.pk",
                "faisal.qayyum@accurate.com.pk",
                "engineer.habib@accurate.com.pk",
                "faizan.lesco@accurate.com.pk"
            };

            await _emailService.SendEmailWithAttachmentAsync(
                "Daily Installation Report-LESCO Project",
                emailBody,
                "hamza.javaid@accurate.com.pk",
                "project.reports@accurate.com.pk",
                ccEmails,
                pdf,
                "DailyInstallationReport.pdf"
            );
        }
    }
    private string ConvertImageToBase64(string imagePath)
    {
        byte[] imageArray = System.IO.File.ReadAllBytes(imagePath);
        return Convert.ToBase64String(imageArray);
    }
}