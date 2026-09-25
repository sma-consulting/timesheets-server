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
	}
}
