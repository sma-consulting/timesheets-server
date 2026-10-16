using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	// Who the work is billed to. Top of the Customer > Project > Subproject > Task
	// hierarchy; projects point here through CustomerId, the name the field
	// already had across the models before this entity existed.
	internal class Customer : GeneralModel
	{
		public string Name { get; set; }

		// Short reference used on invoices and in Harvest, e.g. "CITY-EDM".
		public string Code { get; set; }

		// ISO 4217. Time entries copy it when written, so an invoiced entry keeps
		// the currency it was billed in even if the customer's changes later.
		public string Currency { get; set; } = "CAD";
	}
}
