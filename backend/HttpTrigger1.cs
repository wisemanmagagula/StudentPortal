using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backend.Function
{
    public class HttpTrigger1
    {
        private readonly ILogger<HttpTrigger1> _logger;

        public HttpTrigger1(ILogger<HttpTrigger1> logger)
        {
            _logger = logger;
        }

        [Function("HttpExample")]
        public static MultiResponse Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("HttpExample");
            logger.LogInformation("C# HTTP trigger function processed a request.");

            var message = "Welcome to Azure Functions!";

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteStringAsync(message);

            // Return a response to both HTTP trigger and Azure Cosmos DB output binding.
            return new MultiResponse()
            {
                GetUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "1234",
                    Password = "somepassword",
                    Name = "Wiseman",
                    Surname = "sfsgfxh",
                },
                HttpResponse = response
            };
        }
    }

    public class MultiResponse
    {
        [CosmosDBOutput("wmcosmosdb", "wmcosmosdb", Connection = "CosmosDbConnectionSetting", CreateIfNotExists = true)]
        public User GetUser { get; set; }
        public HttpResponseData HttpResponse { get; set; }
    }

    public class MyDocument
    {
        public string id { get; set; }
        public string message { get; set; }
    }

    public class AuthenticationFunction
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthenticationFunction> _logger;

        public AuthenticationFunction(IConfiguration configuration, ILogger<AuthenticationFunction> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [Function("Authenticate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("AuthenticationFunction");
            logger.LogInformation("Authentication function triggered.");

            // Parse the request body to get the username and password
            string requestBody;
            using (var reader = new StreamReader(req.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            LoginRequest user;
            try
            {
                user = JsonConvert.DeserializeObject<LoginRequest>(requestBody);
                if (user == null)
                {
                    throw new JsonException("Deserialized user is null");
                }
            }
            catch (JsonException ex)
            {
                logger.LogError($"Error deserializing request body: {ex.Message}");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request format");
                return badRequestResponse;
            }

            logger.LogInformation($"Received authentication request for username: {user.Username}");

            // Authenticate user against Cosmos DB
            bool isAuthenticated = await AuthenticateUser(user, logger);

            var response = req.CreateResponse();
            if (isAuthenticated)
            {
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync("Authentication successful");
                logger.LogInformation("Authentication successful.");
            }
            else
            {
                response.StatusCode = HttpStatusCode.Unauthorized;
                await response.WriteStringAsync("Authentication failed");
                logger.LogInformation("Authentication failed.");
            }

            return response;
        }

        private async Task<bool> AuthenticateUser(LoginRequest activeUser, ILogger logger)
        {
            try
            {
                string cosmosDbConnectionString = _configuration["CosmosDbConnectionSetting"];
                if (string.IsNullOrEmpty(cosmosDbConnectionString))
                {
                    logger.LogError("CosmosDbConnectionSetting is null or empty.");
                    return false;
                }

                logger.LogInformation("Connecting to Cosmos DB...");
                var cosmosClient = new CosmosClient(cosmosDbConnectionString);
                string databaseId = "wmcosmosdb";
                string containerId = "Users";

                var container = cosmosClient.GetContainer(databaseId, containerId);

                var sqlQuery = "SELECT * FROM c WHERE c.username = @username";
                var queryDefinition = new QueryDefinition(sqlQuery).WithParameter("@username", activeUser.Username);
                var queryResultSetIterator = container.GetItemQueryIterator<User>(queryDefinition);

                while (queryResultSetIterator.HasMoreResults)
                {
                    var response = await queryResultSetIterator.ReadNextAsync();
                    var user = response.FirstOrDefault();

                    if (user != null && user.Password == activeUser.Password)
                    {
                        return true; // Authentication successful
                    }
                }

                return false; // User not found or password incorrect
            }
            catch (CosmosException ex)
            {
                logger.LogError($"Cosmos DB Exception: {ex}");
                return false; // Authentication failed
            }
            catch (Exception ex)
            {
                logger.LogError($"General Exception: {ex}");
                return false; // Authentication failed
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class CreateUserFunction
    {
        private readonly ILogger<CreateUserFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public CreateUserFunction(ILogger<CreateUserFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _container = _cosmosClient.GetContainer("wmcosmosdb", "Users");
        }

        [Function("CreateUser")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("Processing CreateUser request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            User user = JsonConvert.DeserializeObject<User>(requestBody);

            if (user == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid user data.");
                return badRequestResponse;
            }

            user.Id = Guid.NewGuid().ToString();

            await _container.CreateItemAsync(user, new PartitionKey(user.Id));

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(user);
            return response;
        }
    }

    public class RegisterUserModulesFunction
    {
        private readonly ILogger<RegisterUserModulesFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _userContainer;
        private readonly Container _userModuleContainer;

        public RegisterUserModulesFunction(
            ILogger<RegisterUserModulesFunction> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _userContainer = _cosmosClient.GetContainer("wmcosmosdb", "Users");
            _userModuleContainer = _cosmosClient.GetContainer("wmcosmosdb", "UserModules");
        }

        [Function("RegisterUserModules")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/{username}/enrol-modules")] HttpRequestData req,
            string username)
        {
            _logger.LogInformation($"Processing RegisterUserModules request for username: {username}");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var registerRequest = JsonConvert.DeserializeObject<RegisterUserModulesRequest>(requestBody);

                if (registerRequest == null || registerRequest.moduleIds == null || !registerRequest.moduleIds.Any())
                {
                    _logger.LogError(requestBody);
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid input");
                    return badRequestResponse;
                }

                if (username == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await notFoundResponse.WriteStringAsync("Missing Username.");
                    return notFoundResponse;
                }

                // Create UserModules for each module code
                var userModules = registerRequest.moduleIds.Select(moduleCode => new UserModule
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentId = username,
                    ModuleId = moduleCode.Code,
                    SemesterMark = 0,
                    ExamMark = 0,
                    FinalMark = 0,
                    IsRegistered = true,
                }).ToList();

                foreach (var userModule in userModules)
                {
                    await _userModuleContainer.CreateItemAsync(userModule, new PartitionKey(userModule.Id));
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new { message = "Modules enrolled successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the request.");
                return errorResponse;
            }
        }
    }

    public class RegisterUserModulesRequest
    {
        public List<Module> moduleIds { get; set; }
    }

    public class UserDetails
    {
        public User User { get; set; }
        public List<UserModule> EnrolledModules { get; set; }
    }

    public class UpdatePasswordFunction
    {
        private readonly ILogger<UpdatePasswordFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public UpdatePasswordFunction(ILogger<UpdatePasswordFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _container = _cosmosClient.GetContainer("wmcosmosdb", "Users");
        }

        [Function("UpdatePassword")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "users/{id}/update-password")] HttpRequestData req, string id)
        {
            _logger.LogInformation("Processing UpdatePassword request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<UpdatePasswordRequest>(requestBody);

            if (data == null || string.IsNullOrWhiteSpace(data.NewPassword))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid password update data.");
                return badRequestResponse;
            }

            try
            {
                ItemResponse<User> userResponse = await _container.ReadItemAsync<User>(id, new PartitionKey(id));
                User user = userResponse.Resource;

                user.Password = data.NewPassword;

                await _container.ReplaceItemAsync(user, user.Id, new PartitionKey(user.Id));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Password updated successfully.");
                return response;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("User not found.");
                return notFoundResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating password: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Internal server error.");
                return errorResponse;
            }
        }
    }

    public class GetUserDetailsFunction
    {
        private readonly ILogger<GetUserDetailsFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _userContainer;
        private readonly Container _userModuleContainer;
        private readonly Container _moduleContainer;

        public GetUserDetailsFunction(
            ILogger<GetUserDetailsFunction> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _userContainer = _cosmosClient.GetContainer("wmcosmosdb", "Users");
            _userModuleContainer = _cosmosClient.GetContainer("wmcosmosdb", "UserModules");
            _moduleContainer = _cosmosClient.GetContainer("wmcosmosdb", "Modules");
        }

        [Function("GetUserDetails")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{username}/enrolment")] HttpRequestData req,
            string username)
        {
            _logger.LogInformation($"Processing GetUserDetails request for username: {username}");

            try
            {
                // Query the Users container to find the user by username
                var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.username = @username")
                    .WithParameter("@username", username);

                FeedIterator<User> queryResultSetIterator = _userContainer.GetItemQueryIterator<User>(queryDefinition);

                User user = null;
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<User> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (User u in currentResultSet)
                    {
                        user = u;
                        break;
                    }
                }

                if (user == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("No Student Found for that Username.");
                    return notFoundResponse;
                }

                
                var userModuleQuery = new QueryDefinition("SELECT * FROM c WHERE c.studentId = @studentId")
                    .WithParameter("@studentId", user.Username);

                FeedIterator<UserModule> userModuleIterator = _userModuleContainer.GetItemQueryIterator<UserModule>(
                    userModuleQuery);

                List<UserModule> userModules = new List<UserModule>();
                while (userModuleIterator.HasMoreResults)
                {
                    FeedResponse<UserModule> response = await userModuleIterator.ReadNextAsync();
                    userModules.AddRange(response.Resource);
                }

                // Retrieve details for each enrolled module using the ModuleCode
                List<Module> modules = new List<Module>();
                foreach (UserModule userModule in userModules)
                {
                    var moduleQueryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.code = @moduleCode")
                        .WithParameter("@moduleCode", userModule.ModuleId);

                    FeedIterator<Module> moduleQueryResultSetIterator = _moduleContainer.GetItemQueryIterator<Module>(
                        moduleQueryDefinition);

                    while (moduleQueryResultSetIterator.HasMoreResults)
                    {
                        FeedResponse<Module> moduleResponse = await moduleQueryResultSetIterator.ReadNextAsync();
                        foreach (Module module in moduleResponse)
                        {
                            modules.Add(module);
                            break;
                        }
                    }
                }

                // Construct the response object
                UserDetails userDetails = new UserDetails
                {
                    User = user,
                    EnrolledModules = userModules.Where(e => e.IsRegistered).ToList(),
                };

                // Return the response
                var responseR = req.CreateResponse(HttpStatusCode.OK);
                await responseR.WriteAsJsonAsync(userDetails);
                return responseR;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("No Student Found for that Username.");
                return notFoundResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the request.");
                return errorResponse;
            }
        }
    }

    public class AddModuleFunction
    {
        private readonly ILogger<AddModuleFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _moduleContainer;

        public AddModuleFunction(ILogger<AddModuleFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _moduleContainer = _cosmosClient.GetContainer("wmcosmosdb", "Modules");
        }

        [Function("AddModule")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("Processing AddModule request.");

            try
            {
                // Read the request body to get module data
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                Module module = JsonConvert.DeserializeObject<Module>(requestBody);

                // Generate a new GUID for the module ID
                module.Id = Guid.NewGuid().ToString();

                // Create the module item in Cosmos DB
                await _moduleContainer.CreateItemAsync(module, new PartitionKey(module.Id));

                // Prepare the response
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(module);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the request.");
                return errorResponse;
            }
        }
    }
    public class GetAllModulesFunction
    {
        private readonly ILogger<GetAllModulesFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _moduleContainer;

        public GetAllModulesFunction(ILogger<GetAllModulesFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _cosmosClient = new CosmosClient(configuration["CosmosDbConnectionSetting"]);
            _moduleContainer = _cosmosClient.GetContainer("wmcosmosdb", "Modules");
        }

        [Function("GetAllModules")]
        public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "modules")] HttpRequestData req)
        {
            _logger.LogInformation("GetAllModules function triggered.");

            try
            {
                var query = new QueryDefinition("SELECT * FROM c");
                var modules = new List<Module>();

                var iterator = _moduleContainer.GetItemQueryIterator<Module>(query);

                while (iterator.HasMoreResults)
                {
                    var responseR = await iterator.ReadNextAsync();
                    modules.AddRange(responseR.Resource);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(modules);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the request.");
                return errorResponse;
            }
        }

    }

    public class Module
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("code")]
        public required string Code { get; set; }

        [JsonProperty("semester")]
        public int Semester { get; set; }
        [JsonProperty("lecturer")]
        public required string Lecturer { get; set; }
    }


    public class UpdatePasswordRequest
    {
        [JsonProperty("newPassword")]
        public required string NewPassword { get; set; }
    }

    public class User
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("username")]
        public required string Username { get; set; }

        [JsonProperty("password")]
        public required string Password { get; set; }

        [JsonProperty("name")]
        public required string Name { get; set; }
        [JsonProperty("surname")]
        public required string Surname { get; set; }

        [JsonProperty("role")]
        public UserRole Role { get; set; }
    }

    public enum UserRole
    {
        Student,
        Lecturer,
        Admin
    }

    public class UserModule
    {
        [JsonProperty("id")]
        public required string Id { get; set; }

        [JsonProperty("studentId")]
        public required string StudentId { get; set; }

        [JsonProperty("moduleId")]
        public required string ModuleId { get; set; }

        [JsonProperty("semesterMark")]
        public double SemesterMark { get; set; }

        [JsonProperty("examMark")]
        public double ExamMark { get; set; }

        [JsonProperty("finalMark")]
        public double FinalMark { get; set; }

        [JsonProperty("isRegistered")]
        public bool IsRegistered { get; set; }
    }
}