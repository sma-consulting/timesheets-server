using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Anyone signed in may read customers - the time log and project pages need the
	// names. Only admins may change them, same as projects.
	internal class CustomerFunctions : RequestHandler
	{
		private readonly ICustomerRepo _customerRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly ISecurityService _securityService;

		public CustomerFunctions(
			ICustomerRepo customerRepo,
			IProjectRepo projectRepo,
			ISecurityService securityService)
		{
			_customerRepo = customerRepo;
			_projectRepo = projectRepo;
			_securityService = securityService;
		}

		[FunctionName("CreateCustomer")]
		public async Task<IActionResult> RunCreateCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/")] HttpRequest req)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			Customer customer = JsonConvert.DeserializeObject<Customer>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = Validate(customer);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			return Ok(
				() => _customerRepo.Create(customer),
				(c) => new
				{
					customer = c
				});
		}


		[FunctionName("GetCustomer")]
		public async Task<IActionResult> RunGetCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customer/{id}/")] HttpRequest req, string id)
		{
			return Ok(
				() => _customerRepo.Get(id),
				(c) => new
				{
					customer = c
				});
		}


		[FunctionName("GetAllCustomer")]
		public async Task<IActionResult> RunGetAllCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customer/")] HttpRequest req)
		{
			return Ok(
				() => _customerRepo.GetAll(),
				(c) => new
				{
					customerList = c
				});
		}


		[FunctionName("UpdateCustomer")]
		public async Task<IActionResult> RunUpdateCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/{id}/update/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			Customer customer = JsonConvert.DeserializeObject<Customer>(
				await new StreamReader(req.Body).ReadToEndAsync());
			customer.Id = id;

			string invalid = Validate(customer);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			return Ok(
				() => _customerRepo.Update(customer).Item2,
				(c) => new
				{
					customer = c
				});
		}


		// Refused while any project still points at the customer - deleting it would
		// leave those projects, and every time entry on them, billed to nobody.
		[FunctionName("DeleteCustomer")]
		public async Task<IActionResult> RunDeleteCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customer/{id}/delete/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			int inUse = _projectRepo.GetAll().Count(p => p.CustomerId == id);
			if (inUse > 0)
			{
				return Invalid(string.Format(
					"This customer still has {0} project(s). Move or delete them first.", inUse));
			}

			return Ok(
				() => _customerRepo.Delete(id),
				(c) => new
				{
					deletedCustomer = c
				});
		}


		private static string Validate(Customer customer)
		{
			if (customer == null || string.IsNullOrWhiteSpace(customer.Name))
			{
				return "A customer name is required.";
			}

			customer.Name = customer.Name.Trim();
			customer.Code = customer.Code?.Trim();
			customer.Currency = string.IsNullOrWhiteSpace(customer.Currency)
				? "CAD"
				: customer.Currency.Trim().ToUpperInvariant();

			if (customer.Currency.Length != 3 || !customer.Currency.All(char.IsLetter))
			{
				return "Currency must be a 3-letter code such as CAD or USD.";
			}

			return null;
		}
	}
}
