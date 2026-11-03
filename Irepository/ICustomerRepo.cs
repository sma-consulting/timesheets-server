using System;
using System.Collections.Generic;

namespace sma.plan
{
	internal interface ICustomerRepo
	{
		Customer Create(Customer data);
		Customer Delete(string id);
		Customer Get(string id);
		List<Customer> GetAll();
		Tuple<Customer, Customer> Update(Customer data);

		// Whether a customer with the id exists.
		bool Exists(string id);

		// The customers whose name contains the text, ignoring case - with only
		// their id and name.
		List<Customer> FindByName(string text);

		// The customer with this name, ignoring case and spaces at either end;
		// null if there's none.
		Customer GetByName(string name);
	}
}
