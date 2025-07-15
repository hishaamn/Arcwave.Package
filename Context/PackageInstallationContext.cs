using Sitecore.Common;

namespace Arcwave.Package.Context
{
    internal class PackageInstallationContext : Switcher<bool, PackageInstallationContext>
    {
        public PackageInstallationContext() : base(true)
        {
        }

        public static bool IsActive => Switcher<bool, PackageInstallationContext>.CurrentValue;
    }
}
