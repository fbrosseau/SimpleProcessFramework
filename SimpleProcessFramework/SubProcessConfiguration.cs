using System;

namespace Spfx
{
    public class SubProcessConfiguration
    {
        public TimeSpan DefaultProcessInitTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}