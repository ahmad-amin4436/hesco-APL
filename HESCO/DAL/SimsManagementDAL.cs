using Dapper;
using HESCO.Models;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Numerics;

namespace HESCO.DAL
{
    public class SimsManagementDAL
    {
        private readonly IConfiguration _configuration;
        public SimsManagementDAL(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region General Methods
        public async Task<(bool success, object data, string error)> GetMeterModel(int projectId)
        {
            try
            {
                using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var parameters = new { p_project_id = projectId };

                var result = await db.QueryAsync<(bool success, int value, string text)>(
                    "GetMeterModelData",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                var resultList = result.ToList();

                if (resultList.Any() && resultList.First().success)
                {
                    var meterModels = resultList.Select(r => new
                    {
                        value = r.value,
                        text = r.text
                    }).ToList();

                    return (true, meterModels, null);
                }

                return (false, null, "No meter models found");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<(IEnumerable<DTODropdown> projectData, string error)> GetProjectsForCurrentDB()
        {
            try
            {
                using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var projectData = await db.QueryAsync<DTODropdown>(
                    "GetProjectsForCurrentDB",
                    commandType: CommandType.StoredProcedure
                );

                return (projectData, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
        #endregion

        #region IMSI
        public async Task<IEnumerable<dynamic>> GetIMSISuggestionsAsync(string suggestionType, string searchTerm)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            var parameters = new
            {
                p_suggestion_type = suggestionType,
                p_search_term = searchTerm
            };

            return await connection.QueryAsync<dynamic>(
                "GetIMSISuggestions_CrossDB",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }
        public async Task<(int recordsTotal, IEnumerable<IMSIDataViewModel> data)> GetIMSIDataCrossDB(
        string fimsi, string fsimNumber, string foperator, string fproject, string fchangeProject, string fstatus,
        string fcreatedBy, string fupdatedBy, string fcreatedAt, string fupdatedAt,
        string fmapDateTime, string fchangeProjectDate, string fissuedTo, string fsimStatus,
        int fpage, int fpageSize)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            var parameters = new
            {
                p_fimsi = fimsi,
                p_fsimNumber = fsimNumber,
                p_foperator = foperator,
                p_fproject = fproject,
                p_fchangeProject = fchangeProject,
                p_fstatus = fstatus,
                p_fcreatedBy = fcreatedBy,
                p_fupdatedBy = fupdatedBy,
                p_fcreatedAt = fcreatedAt,
                p_fupdatedAt = fupdatedAt,
                p_fmapDateTime = fmapDateTime,
                p_fchangeProjectDate = fchangeProjectDate,
                p_fissuedTo = fissuedTo,
                p_fsimStatus = fsimStatus,
                p_fpage = fpage,
                p_fpageSize = fpageSize
            };

            using var multi = await connection.QueryMultipleAsync(
                "GetIMSIDataCrossDB_Simple",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            var recordsTotal = await multi.ReadSingleAsync<int>();
            var data = await multi.ReadAsync<IMSIDataViewModel>();

            return (recordsTotal, data);
        }
        public async Task<(bool success, string message)> DeleteIMSI(int id)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            var parameters = new { p_id = id };

            var result = await connection.QueryFirstOrDefaultAsync<(bool success, string message)>(
                "DeleteIMSIById",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return result;
        }

        public async Task<IMSIDataViewModel> GetIMSIById(int id)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            try
            {
                var parameters = new { p_id = id };

                var imsi = await connection.QueryFirstOrDefaultAsync<IMSIDataViewModel>(
                    "GetIMSIById",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return imsi;
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error for not found
            {
                return null; // Handle not found in DAL
            }
        }
        public async Task<IEnumerable<dynamic>> LoadIMSISearchDDL(string suggestionType)
        {
            using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var parameters = new { p_suggestion_type = suggestionType };
            return await db.QueryAsync<dynamic>(
                "LoadIMSISearchDDL",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }
        public async Task ImportIMSIData(string imsi, string simNumber, string operatorName, string monthlyBill,
                                string dataDetails, int? UserId, string Project, string MeterType)
        {
            using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            await db.ExecuteAsync(
                 "ImportIMSIData",
                 new
                 {
                     p_imsi = imsi,
                     p_sim_number = simNumber,
                     p_operator_name = operatorName,
                     p_monthly_bill = monthlyBill,
                     p_data_details = dataDetails,
                     p_user_id = UserId,
                     p_project_id = Project,
                     p_meter_type = MeterType
                 },
            commandType: CommandType.StoredProcedure
            );
        }
        public async Task<(IEnumerable<DTODropdown> projectData, string error)> GetImportIMSIData()
        {
            try
            {
                using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var projectData = await db.QueryAsync<DTODropdown>(
                    "GetImportIMSIData",
                    commandType: CommandType.StoredProcedure
                );

                return (projectData, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
        #endregion

        #region IMEI
        public async Task<IMEIDataViewModel> GetIMEIById(int id)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            try
            {
                var parameters = new { p_id = id };

                var imei = await connection.QueryFirstOrDefaultAsync<IMEIDataViewModel>(
                    "GetIMEIById",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return imei;
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error for not found
            {
                return null; // Or throw a custom exception
            }
        }
        public async Task<(bool success, string message)> DeleteIMEI(int id)
        {
            try
            {
                using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                var parameters = new { p_id = id };

                var result = await connection.QueryFirstOrDefaultAsync<(bool success, string message)>(
                    "DeleteIMEIById",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return (result.success, result.message);
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting IMEI: {ex.Message}");
            }
        }
        public async Task<object> GetIMEISuggestions(string suggestionType, string searchTerm)
        {
            // Determine which connection to use based on suggestion type


            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("p_suggestion_type", suggestionType);
                parameters.Add("p_search_term", searchTerm);

                var suggestions = await connection.QueryAsync<dynamic>(
                    "GetIMEISuggestions",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return suggestions.ToList();
            }
            catch (MySqlException ex) when (ex.Number == 1644) // Custom error code for invalid type
            {
                throw new ArgumentException("Invalid suggestion type");
            }
        }

        public async Task<object> GetIMEIDataFromDatabaseCrossDB(
    string fimei, string fproject, string fchangeProject, string fstatus,
    string fuploadedBy, string fupdatedBy, string fuploadedAt,
    string fupdatedAt, string fmapDateTime, string fchangeProjectDate,
    int fpage, int fpageSize, string draw)
        {
            using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            var parameters = new DynamicParameters();
            parameters.Add("p_fimei", fimei);
            parameters.Add("p_fproject", fproject);
            parameters.Add("p_fchangeProject", fchangeProject);
            parameters.Add("p_fstatus", fstatus);
            parameters.Add("p_fuploadedBy", fuploadedBy);
            parameters.Add("p_fupdatedBy", fupdatedBy);
            parameters.Add("p_fuploadedAt", fuploadedAt);
            parameters.Add("p_fupdatedAt", fupdatedAt);
            parameters.Add("p_fmapDateTime", fmapDateTime);
            parameters.Add("p_fchangeProjectDate", fchangeProjectDate);
            parameters.Add("p_fpage", fpage);
            parameters.Add("p_fpageSize", fpageSize);

            using var multi = await connection.QueryMultipleAsync(
                "GetIMEIDataCrossDB",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            var recordsTotal = await multi.ReadSingleAsync<int>();
            var data = await multi.ReadAsync<IMEIDataViewModel>();

            return new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsTotal,
                data = data
            };
        }

        public async Task ImportIMEIData(string imei, int? userId, string projectId)
        {
            using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            await db.ExecuteAsync(
                "ImportIMEIData",
                new
                {
                    p_imei = imei,
                    p_user_id = userId,
                    p_project_id = projectId
                },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<dynamic>> LoadIMEISearchDDL(string suggestionType)
        {
            using var db = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var parameters = new { p_suggestion_type = suggestionType };
            return await db.QueryAsync<dynamic>(
                "LoadIMEISearchDDL",
                parameters,
                commandType: CommandType.StoredProcedure
            );
        }
        #endregion

    }
}
