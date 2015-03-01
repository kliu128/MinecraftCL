using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MinecraftLaunchLibrary
{
    public class AuthenticationInformation
    {
        public string MinecraftUsername { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public string UserType { get; set; }
    }
}
