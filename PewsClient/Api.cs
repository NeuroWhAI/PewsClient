using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace PewsClient
{
    static class Api
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
