namespace sma.plan
{
	// A user-facing error. Key is what the frontend translates, from its
	// serverErrors.json; Message is the English text, for anything that shows it
	// as it is (Claude through the MCP tools, logs, a frontend that doesn't know
	// the key yet); Params are any values the text needs, e.g. new { count }.
	// Every one is defined in Errors.
	internal sealed record ApiError(string Key, string Message, object Params = null);
}
