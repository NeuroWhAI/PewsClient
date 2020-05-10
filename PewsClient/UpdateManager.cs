using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace PewsClient
{
    public static class UpdateManager
    {
        public static Version GetServerVersion()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string info = client.DownloadString(new Uri("https://raw.githubusercontent.com/NeuroWhAI/PewsClient/master/PewsClient/Properties/AssemblyInfo.cs"));

                    int index = info.LastIndexOf("AssemblyVersion");
                    index = info.IndexOf('\"', index + 1);

                    int endIndex = info.IndexOf('\"', index + 1);

                    if (endIndex > index)
                    {
                        return Version.Parse(info.Substring(index + 1, endIndex - index - 1));
                    }
                }
            }
            catch (WebException)
            {
                return null;
            }


            return null;
        }

        public static bool CheckUpdate()
        {
            var serverVersion = GetServerVersion();

            if (serverVersion == null)
            {
                return false;
            }

            var thisVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            return serverVersion > thisVersion;
        }
    }
}
