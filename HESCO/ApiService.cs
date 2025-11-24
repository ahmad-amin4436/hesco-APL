using Newtonsoft.Json;
using System.Text;

namespace HESCO
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;

        public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        public class AuthenticationResult
        {
            public bool IsAuthenticated { get; set; }
            public string SecretKey { get; set; }
            public string ErrorMessage { get; set; }
        }
        public async Task<AuthenticationResult> AuthenticateUser(string username, string password)
        {
            var url = "http://3.6.96.107:8320/hesco-udil/api/web/v1/auth/authenticate2";

            try
            {
                // Validate the inputs
                if (string.IsNullOrWhiteSpace(password))
                {
                    return new AuthenticationResult { IsAuthenticated = false, ErrorMessage = "Password cannot be empty." };
                }

                // Create the payload
                var payload = new
                {
                    username = username,
                    password = password
                };

                // Serialize the payload to JSON
                var jsonPayload = JsonConvert.SerializeObject(payload);

                // Create the HTTP content
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send the POST request
                var response = await _httpClient.PostAsync(url, content);

                // Read the response content as a string
                var responseContent = await response.Content.ReadAsStringAsync();

                // Check if the response status is 500 (Internal Server Error)
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    return new AuthenticationResult { IsAuthenticated = false, ErrorMessage = "Internal server error occurred." };
                }

                // Ensure the response is successful
                response.EnsureSuccessStatusCode();

                // Deserialize the response
                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);

                // Check if "secret_key" is present in the response
                if (responseObject.ContainsKey("secret_key"))
                {
                    return new AuthenticationResult { IsAuthenticated = true, SecretKey = responseObject["secret_key"] };
                }
                else
                {
                    return new AuthenticationResult { IsAuthenticated = false, ErrorMessage = "Username or Password is incorrect." };
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError($"HTTP request error: {httpEx.Message}");
                return new AuthenticationResult { IsAuthenticated = false, ErrorMessage = "Unable to reach the API. Please try again later." };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
                return new AuthenticationResult { IsAuthenticated = false, ErrorMessage = "An unexpected error occurred." };
            }
        }
    }
}