using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
	internal class ProjectTemplate : GeneralModel
	{
		public List<ProjectPhaseTemplate> PhaseTemplates { get; set; } = new List<ProjectPhaseTemplate>();
	}
}
