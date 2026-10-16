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

		// The rules for saving a customer, shared by the website's endpoints and
		// Claude's create_client tool. Returns null when the customer is valid,
		// otherwise the reason it isn't. Also tidies what it checks: trims text
		// and defaults the currency to CAD.
		public static string Validate(Customer customer)
		{
			if (customer == null || string.IsNullOrWhiteSpace(customer.Name))
			{
				return "A customer name is required.";
			}

			customer.Name = customer.Name.Trim();
			customer.Code = customer.Code?.Trim();
			customer.Currency = string.IsNullOrWhiteSpace(customer.Currency)
				? "CAD"
				: customer.Currency.Trim().ToUpperInvariant();

			if (customer.Currency.Length != 3 || !customer.Currency.All(char.IsLetter))
			{
				return "Currency must be a 3-letter code such as CAD or USD.";
			}

			return null;
		}
	}
}
