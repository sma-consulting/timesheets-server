using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	// A kind of work, e.g. "Senior Developer". Global: one row is shared by every
	// project. Who holds a role on a given project is recorded on
	// ProjectAssignment, not here.
	internal class ProjectRole : GeneralModel
	{
		// Optional short reference, e.g. "SDEV".
		public string Code { get; set; }

		public string Name { get; set; }
	}
}
