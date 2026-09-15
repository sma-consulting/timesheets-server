using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectRole : GeneralModel
	{
		public string Name { get; set; }

		public List<string> Assignees { get; set; } = new List<string>();
	}
}
