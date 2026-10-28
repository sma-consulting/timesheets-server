using System.Collections.Generic;
using System.Linq;

namespace sma.plan
{
	// Makes sure the paid hours off project is there, at startup: everyone logs
	// lunch breaks, stat holidays, vacation, personal, sick and unpaid leave to
	// it.
	//
	//   Z Paid Hours Off            non-billable, internal (status SMA)
	//   └ Paid hours off            its one subproject
	//     ├ SMA Daily Lunch Break   ┐
	//     ├ Statutory Holiday       │
	//     ├ Vacation                │ one task per TimeOffTypes, every one
	//     ├ Personal Leave          │ non-billable
	//     ├ Sick Leave              │
	//     └ Unpaid Leave            ┘
	//
	// Found by its flags, never by name, so renaming any of it is safe. Whatever
	// is missing is added - a task deleted by mistake, or a kind added since,
	// shows up at the next start, since the Staff hours checks rely on them all.
	internal static class PaidTimeOffSeed
	{
		private const string ProjectName = "Z Paid Hours Off";
		private const string SubprojectName = "Paid hours off";

		private static readonly (string Type, string Name)[] Tasks =
		{
			(TimeOffTypes.LunchBreak, "SMA Daily Lunch Break"),
			(TimeOffTypes.StatHoliday, "Statutory Holiday"),
			(TimeOffTypes.Vacation, "Vacation"),
			(TimeOffTypes.PersonalLeave, "Personal Leave"),
			(TimeOffTypes.SickLeave, "Sick Leave"),
			(TimeOffTypes.UnpaidLeave, "Unpaid Leave"),
		};

		public static void Run(IProjectRepo projects, IProjectTaskRepo tasks, IProjectSubTaskRepo subTasks)
		{
			string projectId = projects.IdsPaidTimeOff().FirstOrDefault()
				?? projects.Create(new Project
				{
					Name = ProjectName,
					ProjectType = Project.TypeNonBillable,
					Billable = false,
					Status = Project.StatusSma,
					PaidTimeOff = true,
				}).Id;

			List<ProjectTask> subprojects = tasks.GetByProjects(new[] { projectId });
			string subprojectId = subprojects.FirstOrDefault()?.Id
				?? tasks.Create(new ProjectTask { ProjectId = projectId, Name = SubprojectName }).Id;

			HashSet<string> have = subTasks
				.GetInProjects(new[] { projectId }, subprojects.Select(t => t.Id))
				.Select(s => s.TimeOffType)
				.Where(t => t != null)
				.ToHashSet();

			foreach (var (type, name) in Tasks)
			{
				if (have.Contains(type))
				{
					continue;
				}

				subTasks.Create(new ProjectSubTask
				{
					ProjectId = projectId,
					ProjectTaskId = subprojectId,
					Name = name,
					Billable = false,
					TimeOffType = type,
				});
			}
		}

		// The paid hours off cost codes, by TimeOffTypes - where test data and
		// imports put leave, holidays and lunch breaks.
		public static Dictionary<string, ProjectSubTask> ByType(IProjectRepo projects, IProjectTaskRepo tasks, IProjectSubTaskRepo subTasks)
		{
			List<string> paidTimeOff = projects.IdsPaidTimeOff().ToList();
			return subTasks
				.GetInProjects(paidTimeOff, tasks.GetByProjects(paidTimeOff).Select(t => t.Id))
				.Where(s => s.TimeOffType != null)
				.GroupBy(s => s.TimeOffType)
				.ToDictionary(g => g.Key, g => g.First());
		}
	}
}
