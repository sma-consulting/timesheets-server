using System;

namespace sma.plan
{
	// Who may call an endpoint. Every function declares one with [Allow]; the
	// security middleware refuses any function that doesn't (deny by default),
	// so a forgotten check locks an endpoint rather than opening it.
	internal enum Role
	{
		// No sign-in needed: signing in itself, and asking who you are.
		Anyone,

		// Any signed-in team member. Rules about a specific record - is this
		// time entry yours? - still live in the services.
		SignedIn,

		// Administrators only.
		Admin,
	}

	[AttributeUsage(AttributeTargets.Method)]
	internal sealed class AllowAttribute : Attribute
	{
		public AllowAttribute(Role role)
		{
			Role = role;
		}

		public Role Role { get; }
	}
}
