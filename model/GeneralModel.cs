using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    // Tolerate stored fields the class no longer declares. The schema keeps
    // evolving, and a leftover element in an old document shouldn't take down
    // the endpoint that reads it.
    [BsonIgnoreExtraElements(Inherited = true)]
    internal abstract class GeneralModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    }
}
