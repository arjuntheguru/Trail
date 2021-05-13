using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Domain.Settings
{
    public interface IAppSettings
    {
        string Secret { get; set; }
    }

    public class AppSettings : IAppSettings
    {
        public string Secret { get; set; }
    }
}
