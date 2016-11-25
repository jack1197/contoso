using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace contoso.DataModels
{
    public class LUISResponse
    {
        public class Resolution
        {
        }

        public class Value
        {
            public string entity { get; set; }
            public string type { get; set; }
            public Resolution resolution { get; set; }
        }

        public class Parameter
        {
            public string name { get; set; }
            public string type { get; set; }
            public bool required { get; set; }
            public IList<Value> value { get; set; }
        }

        public class Action
        {
            public bool triggered { get; set; }
            public string name { get; set; }
            public IList<Parameter> parameters { get; set; }
        }

        public class Intent
        {
            public string intent { get; set; }
            public double score { get; set; }
            public IList<Action> actions { get; set; }
        }

        public class Entity
        {
            public string entity { get; set; }
            public string type { get; set; }
            public int startIndex { get; set; }
            public int endIndex { get; set; }
            public double score { get; set; }
            public Resolution resolution { get; set; }
        }

        public class Dialog
        {
            public string contextId { get; set; }
            public string status { get; set; }
        }
        
        public string query { get; set; }
        public Intent topScoringIntent { get; set; }
        public IList<Intent> intents { get; set; }
        public IList<Entity> entities { get; set; }
        public Dialog dialog { get; set; }
        
    }
}