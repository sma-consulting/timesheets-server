using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{

	internal class UserService: IUserService
	{
		private IUserRepo _userRepo;
		private ITeamMemberRepo _teamMemberRepo;

		public UserService(IUserRepo userRepo, ITeamMemberRepo teamMemberRepo)
		{
			_userRepo = userRepo;
			_teamMemberRepo = teamMemberRepo;
		}

		// Maps a login email to the TeamMember representing that person, creating
		// both records the first time an email is seen. Not every TeamMember has a
		// User - planning placeholders ("Summer Co-op 2028") never log in.
		public TeamMember ResolveOrCreate(string email)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				return null;
			}

			email = email.Trim();
			User user = _userRepo.GetByEmail(email);

			if (user != null)
			{
				return _teamMemberRepo.Get(user.TeamMemberId);
			}

			TeamMember teamMember = _teamMemberRepo.Create(new TeamMember
			{
				Name = NameFromEmail(email),
				Email = email,
				StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
				EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)),
				PercentAvailable = 100,
			});

			_userRepo.Create(new User
			{
				Email = email,
				TeamMemberId = teamMember.Id,
			});

			return teamMember;
		}

		// "farhan.naim@example.com" -> "Farhan Naim"
		private string NameFromEmail(string email)
		{
			var localPart = email.Split('@').FirstOrDefault();

			if (string.IsNullOrWhiteSpace(localPart))
			{
				return email;
			}

			var words = localPart
				.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower());

			return string.Join(" ", words);
		}
	}
}
