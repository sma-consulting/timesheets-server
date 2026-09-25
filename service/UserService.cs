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
		// both records the first time an email is seen. The TeamMember points at
		// the User, not the other way round - planning placeholders ("Summer Co-op
		// 2028") are staffable but have no login, so their UserId stays null.
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
				TeamMember existing = _teamMemberRepo.GetByUserId(user.Id);

				if (existing != null)
				{
					return existing;
				}
			}
			else
			{
				// The User is created first so the TeamMember has an id to point at.
				user = _userRepo.Create(new User
				{
					Email = email,
					DisplayName = NameFromEmail(email),
				});
			}

			// Reached when the User exists but its TeamMember is missing, as well as
			// on a genuine first login - either way the pair needs completing.
			return _teamMemberRepo.Create(new TeamMember
			{
				Name = string.IsNullOrWhiteSpace(user.DisplayName)
					? NameFromEmail(email)
					: user.DisplayName,
				UserId = user.Id,
				PercentAvailable = 100,
			});
		}

		public bool IsAdmin(string email)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				return false;
			}

			User user = _userRepo.GetByEmail(email.Trim());
			return user != null && user.Privileges != null &&
				user.Privileges.Contains(User.AdminPrivilege);
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
