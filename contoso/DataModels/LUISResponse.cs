using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace contoso.DataModels
{
    public class LUISResponse
    {
        public class Intent
        {
            public string intent { get; set; }
            public double score { get; set; }
        }

        public class Entity
        {
            public string entity { get; set; }
            public string type { get; set; }
            public int startIndex { get; set; }
            public int endIndex { get; set; }
            public double score { get; set; }
        }

        public string query { get; set; }
        public Intent topScoringIntent { get; set; }
        public IList<Entity> entities { get; set; }
        
    }
}