using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace sma.plan
{
	// Rate lists (RateList): who costs what, shared by the projects on a
	// contract. Admins only, like the rates themselves. One person's rate is
	// added or corrected through ProjectBillingRateFunctions; here are the lists,
	// changing several people's rates at once - the yearly increase - and which
	// list a project uses.
	internal class RateListFunctions : RequestHandler
	{
		private readonly IProjectBillingRateRepo _rateRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly DatabaseRepo<RateList> _listRepo = new DatabaseRepo<RateList>();
		private readonly DatabaseRepo<TeamMember> _memberRepo = new DatabaseRepo<TeamMember>();

		public RateListFunctions(IProjectBillingRateRepo rateRepo, IProjectRepo projectRepo)
		{
			_rateRepo = rateRepo;
			_projectRepo = projectRepo;
		}

		// Every list, with the projects using it.
		[Allow(Role.Admin)]
		[Function("GetRateLists")]
		public async Task<IActionResult> RunGetRateLists(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rateList/")] HttpRequest req)
		{
			return Ok(
				() =>
				{
					List<Project> use = _projectRepo.GetRateListUse();
					return _listRepo.GetAll()
						.OrderBy(l => l.Name)
						.Select(l => new
						{
							l.Id,
							l.Name,
							Projects = use
								.Where(p => p.RateListId == l.Id)
								.OrderBy(p => p.FundingCode)
								.Select(p => new { p.Id, p.Name, p.FundingCode })
								.ToList(),
						})
						.ToList();
				},
				(lists) => new
				{
					rateListList = lists
				});
		}

		[Allow(Role.Admin)]
		[Function("CreateRateList")]
		public async Task<IActionResult> RunCreateRateList(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rateList/")] HttpRequest req)
		{
			RateList list = JsonConvert.DeserializeObject<RateList>(await new StreamReader(req.Body).ReadToEndAsync());
			string invalid = CheckName(list?.Name);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			list.Id = null;
			list.Name = list.Name.Trim();
			return Ok(
				() => _listRepo.Create(list),
				(l) => new
				{
					rateList = l
				});
		}

		[Allow(Role.Admin)]
		[Function("RenameRateList")]
		public async Task<IActionResult> RunRenameRateList(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rateList/{id}/update/")] HttpRequest req, string id)
		{
			RateList sent = JsonConvert.DeserializeObject<RateList>(await new StreamReader(req.Body).ReadToEndAsync());
			string invalid = CheckName(sent?.Name);
			if (invalid != null)
			{
				return Invalid(invalid);
			}
			RateList list = _listRepo.Find(id);
			if (list == null)
			{
				return Invalid("That rate list no longer exists.");
			}

			list.Name = sent.Name.Trim();
			return Ok(
				() => _listRepo.Update(list).Item2,
				(l) => new
				{
					rateList = l
				});
		}

		// Only a list no project uses, with its rates.
		[Allow(Role.Admin)]
		[Function("DeleteRateList")]
		public async Task<IActionResult> RunDeleteRateList(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "rateList/{id}/")] HttpRequest req, string id)
		{
			if (!_listRepo.Exists(id))
			{
				return Invalid("That rate list no longer exists.");
			}
			long inUse = _projectRepo.CountByRateList(id);
			if (inUse > 0)
			{
				return Invalid(string.Format("{0} project(s) still use this list. Move them to another list first.", inUse));
			}

			return Ok(
				() =>
				{
					_rateRepo.DeleteByList(id);
					return _listRepo.Delete(id);
				},
				(l) => new
				{
					deletedRateList = l
				});
		}

		// A list's rates, every person's whole history.
		[Allow(Role.Admin)]
		[Function("GetRateListRates")]
		public async Task<IActionResult> RunGetRateListRates(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rateList/{id}/rates/")] HttpRequest req, string id)
		{
			if (!_listRepo.Exists(id))
			{
				return Invalid("That rate list no longer exists.");
			}
			return Ok(
				() => _rateRepo.GetByList(id),
				(r) => new
				{
					projectBillingRateList = r
				});
		}

		private class RateChange
		{
			public List<string> TeamMemberIds { get; set; }
			// Raise each by this much (3 for 3%) ...
			public decimal? Percent { get; set; }
			// ... or set each to this.
			public decimal? HourlyRate { get; set; }
			public DateOnly StartDate { get; set; }
		}

		// Several people's new rates from one day - the yearly increase: each
		// raised by a percentage of their latest rate, or all set to one rate
		// (which can also add someone to the list). Each one's current rate ends
		// the day before; nothing is written unless every one of them fits.
		[Allow(Role.Admin)]
		[Function("ChangeRateListRates")]
		public async Task<IActionResult> RunChangeRateListRates(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rateList/{id}/change/")] HttpRequest req, string id)
		{
			RateChange change = JsonConvert.DeserializeObject<RateChange>(await new StreamReader(req.Body).ReadToEndAsync());
			List<string> people = (change?.TeamMemberIds ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
			if (people.Count == 0)
			{
				return Invalid("Choose whose rates change.");
			}
			if (change.Percent.HasValue == change.HourlyRate.HasValue)
			{
				return Invalid("Give either a percentage or a new rate.");
			}
			if (change.StartDate == default)
			{
				return Invalid("A start date is required.");
			}
			if (!_listRepo.Exists(id))
			{
				return Invalid("That rate list no longer exists.");
			}

			List<ProjectBillingRate> rows = _rateRepo.GetByList(id, people);
			var toClose = new List<string>();
			var added = new List<ProjectBillingRate>();
			foreach (string person in people)
			{
				ProjectBillingRate latest = rows.Where(r => r.TeamMemberId == person).OrderBy(r => r.StartDate).LastOrDefault();
				string who = _memberRepo.Find(person)?.Name;
				if (who == null)
				{
					return Invalid("One of those people no longer exists.");
				}
				if (latest == null && change.Percent.HasValue)
				{
					return Invalid(string.Format("{0} has no rate on this list to raise.", who));
				}
				if (latest != null)
				{
					if (change.StartDate <= latest.StartDate)
					{
						return Invalid(string.Format("{0}'s current rate starts {1:yyyy-MM-dd}; the new one has to start after that.", who, latest.StartDate));
					}
					if (latest.EndDate.HasValue && latest.EndDate.Value != change.StartDate.AddDays(-1))
					{
						return Invalid(string.Format("{0}'s latest rate ends {1:yyyy-MM-dd}; the new one has to start the day after.", who, latest.EndDate.Value));
					}
					if (latest.EndDate == null)
					{
						toClose.Add(latest.Id);
					}
				}

				decimal rate = change.Percent.HasValue
					? Math.Round(latest.HourlyRate * (1 + change.Percent.Value / 100), 2, MidpointRounding.AwayFromZero)
					: change.HourlyRate.Value;
				string invalid = ProjectBillingRateFunctions.CheckRate(rate, change.StartDate, null);
				if (invalid != null)
				{
					return Invalid(invalid);
				}
				added.Add(new ProjectBillingRate
				{
					RateListId = id,
					TeamMemberId = person,
					HourlyRate = rate,
					StartDate = change.StartDate,
				});
			}

			return Ok(
				() =>
				{
					_rateRepo.Close(toClose, change.StartDate.AddDays(-1));
					_rateRepo.CreateMany(added);
					return _rateRepo.GetByList(id);
				},
				(r) => new
				{
					projectBillingRateList = r
				});
		}

		private class RateListUse
		{
			public string ProjectId { get; set; }
			// The list to use; null for none ...
			public string RateListId { get; set; }
			// ... or a new list of its own with this name, starting as a copy
			// of the one it uses now (empty if none), so splitting a project
			// off a shared list keeps everyone's rates.
			public string NewListName { get; set; }
		}

		// Which list a project's hours are priced from.
		[Allow(Role.Admin)]
		[Function("UseRateList")]
		public async Task<IActionResult> RunUseRateList(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "rateList/use/")] HttpRequest req)
		{
			RateListUse use = JsonConvert.DeserializeObject<RateListUse>(await new StreamReader(req.Body).ReadToEndAsync());
			if (use == null || string.IsNullOrWhiteSpace(use.ProjectId) || !_projectRepo.Exists(use.ProjectId))
			{
				return Invalid("That project doesn't exist.");
			}
			bool newList = !string.IsNullOrWhiteSpace(use.NewListName);
			if (newList)
			{
				string invalid = CheckName(use.NewListName);
				if (invalid != null)
				{
					return Invalid(invalid);
				}
			}
			else if (!string.IsNullOrWhiteSpace(use.RateListId) && !_listRepo.Exists(use.RateListId))
			{
				return Invalid("That rate list no longer exists.");
			}

			return Ok(
				() =>
				{
					RateList created = null;
					if (newList)
					{
						created = _listRepo.Create(new RateList { Name = use.NewListName.Trim() });
						string current = _projectRepo.Get(use.ProjectId).RateListId;
						if (!string.IsNullOrEmpty(current))
						{
							_rateRepo.CreateMany(_rateRepo.GetByList(current)
								.Select(r => new ProjectBillingRate
								{
									RateListId = created.Id,
									TeamMemberId = r.TeamMemberId,
									HourlyRate = r.HourlyRate,
									StartDate = r.StartDate,
									EndDate = r.EndDate,
								})
								.ToList());
						}
					}
					string listId = created?.Id ?? (string.IsNullOrWhiteSpace(use.RateListId) ? null : use.RateListId);
					_projectRepo.SetRateList(use.ProjectId, listId);
					return new { ProjectId = use.ProjectId, RateListId = listId, RateList = created };
				},
				(r) => r);
		}

		private static string CheckName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "Give the list a name.";
			}
			return name.Trim().Length > 100 ? "Keep the name under 100 characters." : null;
		}
	}
}
