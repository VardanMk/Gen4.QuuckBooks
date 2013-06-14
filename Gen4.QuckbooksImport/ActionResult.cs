using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gen4.QuckbooksImport
{
    public class ActionResult
    {
        public bool IsErrorPresent { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public object Result { get; set; }
    }
}
