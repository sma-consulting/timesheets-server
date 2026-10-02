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
	// Admin-only, reads included: billing rates are commercially sensitive and only
	// admins set them.
	//
	// For each project + role the rates form one unbroken timeline - no overlaps,
	// no gaps, only the latest open-ended - so every day from the first rate on
	// has exactly one price. Each write is checked against the whole timeline as
	// it would look afterwards, and nothing is written if that check fails.
	internal class ProjectBillingRateFunctions : RequestHandler
	{
		private readonly IProjectBillingRateRepo _rateRepo;
		private readonly IProjectRepo _projectRepo;
		private readonly ISecurityService _securityService;
		private readonly DatabaseRepo<ProjectRole> _roleRepo = new DatabaseRepo<ProjectRole>();

		public ProjectBillingRateFunctions(
			IProjectBillingRateRepo rateRepo,
			IProjectRepo projectRepo,
			ISecurityService securityService)
		{
			_rateRepo = rateRepo;
			_projectRepo = projectRepo;
			_securityService = securityService;
		}

		[Allow(Role.Admin)]
		[Function("GetProjectBillingRates")]
		public async Task<IActionResult> RunGetProjectBillingRates(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projectBillingRate/")] HttpRequest req)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			string projectId = req.Query["projectId"];
			if (string.IsNullOrWhiteSpace(projectId))
			{
				return Invalid("A projectId is required.");
			}

			return Ok(
				() => _rateRepo.GetByProject(projectId),
				(r) => new
				{
					projectBillingRateList = r
				});
		}


		// The usual way to change a rate: add a row starting on the day the new
		// rate applies. If the role's latest row is still open-ended and starts
		// earlier, it is closed the day before, so the handover leaves no gap.
		[Allow(Role.Admin)]
		[Function("CreateProjectBillingRate")]
		public async Task<IActionResult> RunCreateProjectBillingRate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectBillingRate/")] HttpRequest req)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			ProjectBillingRate rate = JsonConvert.DeserializeObject<ProjectBillingRate>(
				await new StreamReader(req.Body).ReadToEndAsync());

			string invalid = ValidateRow(rate);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			rate.Id = null;
			List<ProjectBillingRate> timeline = TimelineFor(rate.ProjectId, rate.ProjectRoleId);

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


		// Project and role can't be changed - that would move the row onto a
		// different timeline. Changing the rate alone always passes; changing
		// dates must keep the timeline unbroken.
		[Allow(Role.Admin)]
		[Function("UpdateProjectBillingRate")]
		public async Task<IActionResult> RunUpdateProjectBillingRate(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projectBillingRate/{id}/update/")] HttpRequest req, string id)
		{
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			ProjectBillingRate existing = _rateRepo.Get(id);
			if (existing == null)
			{
				return Invalid("That billing rate no longer exists.");
			}

			ProjectBillingRate rate = JsonConvert.DeserializeObject<ProjectBillingRate>(
				await new StreamReader(req.Body).ReadToEndAsync());
			rate.Id = id;
			rate.ProjectId = existing.ProjectId;
			rate.ProjectRoleId = existing.ProjectRoleId;

			string invalid = ValidateRow(rate);
			if (invalid != null)
			{
				return Invalid(invalid);
			}

			IEnumerable<ProjectBillingRate> after = TimelineFor(rate.ProjectId, rate.ProjectRoleId)
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
			if (!_securityService.IsCurrentUserAdmin())
			{
				return Forbidden();
			}

			ProjectBillingRate existing = _rateRepo.Get(id);
			if (existing == null)
			{
				return Invalid("That billing rate no longer exists.");
			}

			List<ProjectBillingRate> timeline = TimelineFor(existing.ProjectId, existing.ProjectRoleId)
				.OrderBy(r => r.StartDate)
				.ToList();

			bool isEnd = timeline.First().Id == id || timeline.Last().Id == id;
			if (!isEnd)
			{
				return Invalid(
					"Only the earliest or latest rate for a role can be deleted - removing one in the middle would leave a gap. Change its rate or dates instead.");
			}

			return Ok(
				() => _rateRepo.Delete(id),
				(r) => new
				{
					deletedProjectBillingRate = r
				});
		}


		private List<ProjectBillingRate> TimelineFor(string projectId, string projectRoleId)
		{
			return _rateRepo
				.GetByProject(projectId)
				.Where(r => r.ProjectRoleId == projectRoleId)
				.ToList();
		}

		// Checks the row on its own. Returns null when it's fine.
		private string ValidateRow(ProjectBillingRate rate)
		{
			if (rate == null)
			{
				return "No billing rate was sent.";
			}

			if (string.IsNullOrWhiteSpace(rate.ProjectId) || _projectRepo.Get(rate.ProjectId) == null)
			{
				return "That project doesn't exist.";
			}

			if (string.IsNullOrWhiteSpace(rate.ProjectRoleId) || _roleRepo.Get(rate.ProjectRoleId) == null)
			{
				return "Select a role.";
			}

			if (rate.HourlyRate < 0)
			{
				return "The rate can't be negative.";
			}

			if (rate.StartDate == default)
			{
				return "A start date is required.";
			}

			if (rate.EndDate.HasValue && rate.EndDate.Value < rate.StartDate)
			{
				return "The end date can't be before the start date.";
			}

			return null;
		}

		// Checks one role's rows together: sorted by start, each must begin the day
		// after the previous ends, and only the last may be open-ended.
		private static string CheckContiguous(IEnumerable<ProjectBillingRate> rows)
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
