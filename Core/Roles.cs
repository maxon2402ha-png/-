using System.Linq;

namespace КР_Ханников.Core
{
  
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Support = "Support";
        public const string Supervisor = "Supervisor";
        public const string Client = "Client";

        public static readonly string[] All = new[] { Admin, Support, Supervisor, Client };

        public static bool IsValid(string role) => All.Contains(role);
    }
}