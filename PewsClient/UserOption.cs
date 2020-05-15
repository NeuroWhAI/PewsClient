using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PewsClient
{
    class UserOption : OptionBase
    {
        public double HomeLatitude { get; set; } = -1;
        public double HomeLongitude { get; set; } = -1;
        public bool HomeAvailable => !(HomeLatitude < 0 || HomeLongitude < 0);

        protected override void AfterLoad()
        {
            HomeLatitude = GetProperty(nameof(HomeLatitude), (s) => double.Parse(s), -1);
            HomeLongitude = GetProperty(nameof(HomeLongitude), (s) => double.Parse(s), -1);
        }

        protected override void BeforeSave()
        {
            SetProperty(nameof(HomeLatitude), HomeLatitude);
            SetProperty(nameof(HomeLongitude), HomeLongitude);
        }

        public void RemoveHome()
        {
            HomeLatitude = -1;
            HomeLongitude = -1;
        }
    }
}
