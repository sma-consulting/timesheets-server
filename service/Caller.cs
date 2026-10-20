namespace sma.plan
{
	// Who's making the current request: worked out once by SecurityService from
	// the sign-in header and the User/TeamMember records, then read by every
	// check. Lives for one request; never saved.
	internal sealed class Caller
	{
		public string Email { get; init; }
		public string UserId { get; init; }
		public string TeamMemberId { get; init; }

		// Includes the sandbox "Testing as" switch, as IsCurrentUserAdmin does.
		public bool IsAdmin { get; init; }

		// What [Allow(Role.SignedIn)] means: a known person with a TeamMember.
		public bool IsSignedIn => TeamMemberId != null;

		// Nobody could be identified: no sign-in, or no User for the email.
		public static readonly Caller Nobody = new Caller();
	}
}
