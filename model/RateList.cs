namespace sma.plan
{
	// A list of what people cost: each person's hourly rate over time (the
	// ProjectBillingRate rows that name it). Every project uses one list - its
	// own, or one it shares with other projects on the same contract - so a
	// yearly increase is entered once for all of them (Project.RateListId).
	internal class RateList : GeneralModel
	{
		// "LRT rates".
		public string Name { get; set; }
	}
}
