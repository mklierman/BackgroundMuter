using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundMuter.Models
{
    public class ProcessItemModel
    {
        public string ProcessName { get; set; }
        public string? ProcessWindowTitle { get; set; }
        public string DisplayName { get; set; }
        public nint ProcessHandle { get; set; }
        public Process Process { get; set; }
        public bool IsBeingWatched { get; set; }
    }
}
