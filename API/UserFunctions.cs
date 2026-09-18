using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    internal class LoginRequest
    {
        public string Email { get; set; }
    }

    internal class UserFunctions : RequestHandler
    {

        private ISecurityService _securityService;
        private IUserService _userService;
        private ILogger<UserFunctions> _logger;


		public UserFunctions(ISecurityService securityService, IUserService userService, ILogger<UserFunctions> logger) 
        { 
            _securityService = securityService;
            _userService = userService;
            _logger = logger;
        }

		private static IActionResult NotSignedIn()
		{
			return new OkObjectResult(new
			{
				status = new { code = 401, error = "Not signed in." },
				result = new { },
			});
		}

		[FunctionName("WhoAmI")]
		public async Task<IActionResult> RunWhoAmI(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "whoami")] HttpRequest req)
		{
			var email = _securityService.GetCurrentEmail();

			if (string.IsNullOrWhiteSpace(email))
			{
				return NotSignedIn();
			}

			return Ok(
				() => _userService.ResolveOrCreate(email),
				(t) => new
				{
					teamMember = t
				});
		}


		[FunctionName("Login")]
		public async Task<IActionResult> RunLogin(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequest req)
		{
			LoginRequest login = JsonConvert.DeserializeObject<LoginRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());

			if (string.IsNullOrWhiteSpace(login?.Email))
			{
				return NotSignedIn();
			}

			return Ok(
				() => _userService.ResolveOrCreate(login.Email),
				(t) => new
				{
					teamMember = t
				});
		}

		[FunctionName("CreateUser")]
        public async Task<IActionResult> RunCreateUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/create/")] HttpRequest req)
        {
            User user = JsonConvert.DeserializeObject<User>(
                await new StreamReader(req.Body).ReadToEndAsync());

            return Ok(
                () => (new DatabaseRepo<User>()).Create(user),
                (p) => new
                {
                    user = p
                });
        }


        [FunctionName("DeleteUser")]
        public async Task<IActionResult> RunDeleteUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user/{id}/delete/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<User>()).Delete(id),
                (p) => new
                {
                    deletedUser = p
                });
        }


        [FunctionName("GetUser")]
        public async Task<IActionResult> RunGetUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<User>()).Get(id),
                (p) => new
                {
                    user = p
                });
        }


        [FunctionName("UpdateUser")]
        public async Task<IActionResult> RunUpdateUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/{id}/update/")] HttpRequest req, string id)
        {
            User user = JsonConvert.DeserializeObject<User>(
                await new StreamReader(req.Body).ReadToEndAsync());
            user.Id = id;

            return Ok(
                () => (new DatabaseRepo<User>()).Update(user),
                (res) => new
                {
                    oldUser = ((Tuple<User, User>)res).Item1,
                    newUser = ((Tuple<User, User>)res).Item2,
                });
        }
    }
}
