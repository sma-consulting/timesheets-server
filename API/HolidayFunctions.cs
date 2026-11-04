using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// SMA's holidays, for the Staff hours page. Anyone signed in may read them;
	// only admins may add or remove them. There's no screen for that yet: admins
	// add each new year's from the payroll calendar through this API.
	internal class HolidayFunctions : RequestHandler
	{
		private readonly IHolidayRepo _holidayRepo;

		public HolidayFunctions(IHolidayRepo holidayRepo)
		{
			_holidayRepo = holidayRepo;
		}

		[Allow(Role.SignedIn)]
		[Function("GetHolidays")]
		public async Task<IActionResult> RunGetHolidays(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "holiday/")] HttpRequest req)
		{
			return Ok(
				() => _holidayRepo.GetAll().OrderBy(h => h.Date).ToList(),
				(h) => new
				{
					holidayList = h
				});
		}


		// Body: { "date": "2027-03-29", "name": "easterMonday" }.
		[Allow(Role.Admin)]
		[Function("CreateHoliday")]
		public async Task<IActionResult> RunCreateHoliday(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "holiday/")] HttpRequest req)
		{
			Holiday holiday = JsonConvert.DeserializeObject<Holiday>(
				await new StreamReader(req.Body).ReadToEndAsync());

			ApiError invalid = Validate(holiday);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			return Ok(
				() => _holidayRepo.Create(holiday),
				(h) => new
				{
					holiday = h
				});
		}


		[Allow(Role.Admin)]
		[Function("DeleteHoliday")]
		public async Task<IActionResult> RunDeleteHoliday(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "holiday/{id}/")] HttpRequest req, string id)
		{
			if (!_holidayRepo.Exists(id))
			{
				return Invalid(Errors.Holidays.NotFound);
			}

			return Ok(
				() => _holidayRepo.Delete(id),
				(h) => new
				{
					deletedHoliday = h
				});
		}


		// Null when the holiday can be saved, otherwise the reason it can't. One
		// holiday a day: the Staff hours page keys them by date.
		private ApiError Validate(Holiday holiday)
		{
			if (holiday == null || holiday.Date == default)
			{
				return Errors.Holidays.DateRequired;
			}

			holiday.Name = holiday.Name?.Trim();
			if (string.IsNullOrEmpty(holiday.Name))
			{
				return Errors.Holidays.NameRequired;
			}

			// Compared here rather than in a query: there are only a few dozen, and
			// dates are stored as {Year, Month, Day} documents.
			if (_holidayRepo.GetAll().Any(h => h.Date == holiday.Date))
			{
				return Errors.Holidays.DateTaken(holiday.Date);
			}

			// Always a new holiday, whatever id was sent.
			holiday.Id = null;
			return null;
		}
	}
}
