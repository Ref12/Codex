using Microsoft.Build.Locator;

namespace Codex.Application
{
    internal class MSBuildHelper
    {
        private static Lazy<bool> MSBuildRegistration = new Lazy<bool>(RegisterMSBuildCore);

        private static bool RegisterMSBuildCore()
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering MSBuild locator: {ex.Message}");
                return false;
            }
        }

        public static void RegisterMSBuild()
        {
            var ignored = MSBuildRegistration.Value;
        }
    }
}