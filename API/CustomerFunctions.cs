using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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

		public CustomerFunctions(
			ICustomerRepo customerRepo,
			IProjectRepo projectRepo)
		{
			_customerRepo = customerRepo;
			_projectRepo = projectRepo;
		}

		[Allow(Role.Admin)]
		[Function("CreateCustomer")]
		public async Task<IActionResult> RunCreateCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/")] HttpRequest req)
		{
			Customer customer = JsonConvert.DeserializeObject<Customer>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = Customer.Validate(customer);
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


		[Allow(Role.SignedIn)]
		[Function("GetCustomer")]
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


		[Allow(Role.SignedIn)]
		[Function("GetAllCustomer")]
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


		[Allow(Role.Admin)]
		[Function("UpdateCustomer")]
		public async Task<IActionResult> RunUpdateCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/{id}/update/")] HttpRequest req, string id)
		{
			Customer customer = JsonConvert.DeserializeObject<Customer>(
				await new StreamReader(req.Body).ReadToEndAsync());
			customer.Id = id;

			string invalid = Customer.Validate(customer);
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
		[Allow(Role.Admin)]
		[Function("DeleteCustomer")]
		public async Task<IActionResult> RunDeleteCustomer(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customer/{id}/")] HttpRequest req, string id)
		{
			long inUse = _projectRepo.CountByCustomer(id);
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
	}
}
