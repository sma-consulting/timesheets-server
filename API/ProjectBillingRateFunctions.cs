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
	// Billing rates are commercially sensitive: only admins set them, and only
	// admins read them - except a project's manager, who may read their own
	// projects' rates. They see them on those projects' invoices anyway, and
	// need them for the project's time not yet invoiced.
	//
	// A rate is a person's on a rate list (RateList), which one or more projects
	// price their hours from. For each list + person the rates form one unbroken
	// timeline - no overlaps, no gaps, only the latest open-ended - so every day
	// from the first rate on has exactly one price. Each write is checked against
	// the whole timeline as it would look afterwards, and nothing is written if
	// that check fails. Changing several people's rates at once is
	// RateListFunctions' Raise.
	internal class ProjectBillingRateFunctions : RequestHandler
	{
		private readonly IProjectBillingRateRepo _rateRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly IProjectService _projectService;
		private readonly ISecurityService _securityService;
		private readonly DatabaseRepo<RateList> _listRepo = new DatabaseRepo<RateList>();
		private readonly DatabaseRepo<TeamMember> _memberRepo = new DatabaseRepo<TeamMember>();

		public ProjectBillingRateFunctions(
			IProjectBillingRateRepo rateRepo,
			IProjectRepo projectRepo,
			IProjectService projectService,
			ISecurityService securityService)
		{
			_rateRepo = rateRepo;
			_projectRepo = projectRepo;
			_projectService = projectService;
			_securityService = securityService;
		}

		// What a project's hours are priced by: its rate list's rows, tagged with
		// the project. None while it has no list.
		[Allow(Role.SignedIn)]
		[Function("GetProjectBillingRates")]
		public async Task<IActionResult> RunGetProjectBillingRates(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectBillingRate/")] HttpRequest req)
		{
			string projectId = req.Query["projectId"];
			if (string.IsNullOrWhiteSpace(projectId))
			{
				return Invalid("A projectId is required.");
			}

			if (!_securityService.IsCurrentUserAdmin() && !_projectService.ManagedProjectIds().Contains(projectId))
			{
				return Forbidden();
			}

			Project project = _projectRepo.Find(projectId);
			if (project == null)
			{
				return Invalid("That project doesn't exist.");
			}

			return Ok(
				() => _rateRepo.GetForProjects(new[] { project }),
				(r) => new
				{
					projectBillingRateList = r
				});
		}

		// Several projects' rates at once, for a report across projects:
		// { projectIds }. Gives which list each project uses (null for none) and
		// those lists' rows, each list once however many projects share it. The
		// same rule as for one project: an admin reads any, a manager their own.
		// A POST, so a long list of ids fits.
		[Allow(Role.SignedIn)]
		[Function("GetBillingRatesForProjects")]
		public async Task<IActionResult> RunGetBillingRatesForProjects(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectBillingRate/forProjects/")] HttpRequest req)
		{
			ProjectIdsRequest body = JsonConvert.DeserializeObject<ProjectIdsRequest>(
				await new StreamReader(req.Body).ReadToEndAsync());
			List<string> ids = (body?.ProjectIds ?? new List<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct()
				.ToList();
			if (ids.Count == 0)
			{
				return Invalid("Choose at least one project.");
			}
			if (ids.Count > MaxProjectsAtOnce)
			{
				return Invalid($"Ask for at most {MaxProjectsAtOnce} projects at once.");
			}

			if (!_securityService.IsCurrentUserAdmin())
			{
				var managed = _projectService.ManagedProjectIds();
				if (ids.Any(id => !managed.Contains(id)))
				{
					return Forbidden();
				}
			}

			return Ok(
				() =>
				{
					List<Project> projects = _projectRepo.GetRateListsOf(ids);
					List<string> listIds = projects
						.Where(p => !string.IsNullOrEmpty(p.RateListId))
						.Select(p => p.RateListId)
						.Distinct()
						.ToList();
					return new
					{
						projectRateLists = projects.Select(p => new { projectId = p.Id, rateListId = p.RateListId }).ToList(),
						projectBillingRateList = listIds.Count > 0 ? _rateRepo.GetByLists(listIds) : new List<ProjectBillingRate>(),
					};
				},
				(r) => r);
		}

		private const int MaxProjectsAtOnce = 1000;

		private class ProjectIdsRequest
		{
			public List<string> ProjectIds { get; set; }
		}


		// The usual way to change a rate: add a row starting on the day the new
		// rate applies. If the person's latest row is still open-ended and starts
		// earlier, it is closed the day before, so the handover leaves no gap.
		[Allow(Role.Admin)]
		[Function("CreateProjectBillingRate")]
		public async Task<IActionResult> RunCreateProjectBillingRate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectBillingRate/")] HttpRequest req)
		{
			ProjectBillingRate rate = JsonConvert.DeserializeObject<ProjectBillingRate>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = ValidateRow(rate);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			rate.Id = null;
			rate.ProjectId = null;
			List<ProjectBillingRate> timeline = _rateRepo.GetByList(rate.RateListId, new[] { rate.TeamMemberId });

			ProjectBillingRate latest = timeline.OrderBy(r => r.StartDate).LastOrDefault();
			ProjectBillingRate closed = null;

			if (latest != null && latest.EndDate == null && rate.StartDate > latest.StartDate)
			{
				latest.EndDate = rate.StartDate.AddDays(-1);
				closed = latest;
			}

			invalid = CheckContiguous(timeline.Append(rate));
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			return Ok(
				() =>
				{
					if (closed != null)
					{
						_rateRepo.Update(closed);
					}
					return _rateRepo.Create(rate);
				},
				(r) => new
				{
					projectBillingRate = r,
					closedProjectBillingRate = closed,
				});
		}


		// The list and person can't be changed - that would move the row onto a
		// different timeline. Changing the rate alone always passes; changing
		// dates must keep the timeline unbroken.
		[Allow(Role.Admin)]
		[Function("UpdateProjectBillingRate")]
		public async Task<IActionResult> RunUpdateProjectBillingRate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectBillingRate/{id}/update/")] HttpRequest req, string id)
		{
			ProjectBillingRate existing = _rateRepo.Find(id);
			if (existing == null)
			{
				return Invalid("That billing rate no longer exists.");
			}

			ProjectBillingRate rate =JsonConvert.DeserializeObject<ProjectBillingRate>(
				await new StreamReader(req.Body).ReadToEndAsync());
			if (rate == null)
			{
				return Invalid("No billing rate was sent.");
			}
			rate.Id = id;
			rate.RateListId = existing.RateListId;
			rate.TeamMemberId = existing.TeamMemberId;
			rate.ProjectId = null;

			string invalid = ValidateRow(rate);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			IEnumerable<ProjectBillingRate> after = _rateRepo.GetByList(rate.RateListId, new[] { rate.TeamMemberId })
				.Where(r => r.Id != id)
				.Append(rate);

			invalid = CheckContiguous(after);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			return Ok(
				() => _rateRepo.Update(rate).Item2,
				(r) => new
				{
					projectBillingRate = r
				});
		}


		// Only the first or last row of a timeline may go: removing one from the
		// middle would open a gap.
		[Allow(Role.Admin)]
		[Function("DeleteProjectBillingRate")]
		public async Task<IActionResult> RunDeleteProjectBillingRate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projectBillingRate/{id}/")] HttpRequest req, string id)
		{
			ProjectBillingRate existing = _rateRepo.Find(id);
			if (existing == null)
			{
				return Invalid("That billing rate no longer exists.");
			}

			List<ProjectBillingRate> timeline =_rateRepo.GetByList(existing.RateListId, new[] { existing.TeamMemberId })
				.OrderBy(r => r.StartDate)
				.ToList();

			bool isEnd = timeline.First().Id == id || timeline.Last().Id == id;
			if (!isEnd)
			{
				return Invalid(
					"Only someone's earliest or latest rate can be deleted - removing one in the middle would leave a gap. Change its rate or dates instead.");
			}

			return Ok(
				() => _rateRepo.Delete(id),
				(r) => new
				{
					deletedProjectBillingRate = r
				});
		}


		// Checks the row on its own. Returns null when it's fine.
		private string ValidateRow(ProjectBillingRate rate)
		{
			if (rate == null)
			{
				return "No billing rate was sent.";
			}

			if (!_listRepo.Exists(rate.RateListId))
			{
				return "That rate list doesn't exist.";
			}

			if (!_memberRepo.Exists(rate.TeamMemberId))
			{
				return "Select a person.";
			}

			return CheckRate(rate.HourlyRate, rate.StartDate, rate.EndDate);
		}

		// A rate and its dates, on their own. Null when they're fine.
		internal static string CheckRate(decimal hourlyRate, DateOnly startDate, DateOnly? endDate)
		{
			if (hourlyRate < 0)
			{
				return "The rate can't be negative.";
			}

			if (startDate == default)
			{
				return "A start date is required.";
			}

			if (endDate.HasValue && endDate.Value < startDate)
			{
				return "The end date can't be before the start date.";
			}

			return null;
		}

		// Checks one person's rows together: sorted by start, each must begin the
		// day after the previous ends, and only the last may be open-ended.
		internal static string CheckContiguous(IEnumerable<ProjectBillingRate> rows)
		{
			List<ProjectBillingRate> sorted = rows.OrderBy(r => r.StartDate).ToList();

			for (int i = 0; i < sorted.Count - 1; i++)
			{
				ProjectBillingRate current = sorted[i];
				ProjectBillingRate next = sorted[i + 1];

				if (current.EndDate == null)
				{
					return string.Format(
						"The rate starting {0} is open-ended, but another starts after it on {1}. Only the latest rate can be open-ended.",
						Day(current.StartDate), Day(next.StartDate));
				}

				DateOnly expected = current.EndDate.Value.AddDays(1);

				if (next.StartDate < expected)
				{
					return string.Format(
						"Rates would overlap: one runs to {0} but the next starts {1}.",
						Day(current.EndDate.Value), Day(next.StartDate));
				}

				if (next.StartDate > expected)
				{
					return string.Format(
						"That would leave a gap from {0} to {1} with no rate. Each rate must start the day after the previous one ends.",
						Day(expected), Day(next.StartDate.AddDays(-1)));
				}
			}

			return null;
		}

		private static string Day(DateOnly date)
		{
			return date.ToString("yyyy-MM-dd");
		}
	}
}
