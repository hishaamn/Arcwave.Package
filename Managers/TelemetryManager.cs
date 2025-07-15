using Sitecore.Nexus.Consumption;

namespace Arcwave.Package.Managers
{
    public class TelemetryManager
    {
        internal static TelemetryClient TelemetryClient { get; } = TelemetryFactory.CreateClient();

        internal sealed class Packager
        {
            internal static readonly string InstallActivated = "XM.Platform.Core.Install.Active|FLAG|K+Ibs71xXcqX3dIvgWHjla6spWtvcZnTuxFe5E4zDbNw9FQL8lP0SLkbHwD9yRWCh8mgR38nAtoiMFisz1uPTQ==";
            internal static readonly string InstallOpened = "XM.Platform.Core.Install.Opened|SUM|uL7Lm9giTUoCyJLZa7SH6vb9QbpBf8SWO/orETZRWQp4ZZsufXCE44JqM1K3WsxGcmEizKW044NNw7QvQeewug==";
            internal static readonly string PackagesInstalled = "XM.Platform.Core.UI.Packager.PackagesInstalled|SUM|yfvMbKz8k//ZqlD4nqqQBX+6V3ADuFo+49ZmG/NAgDkUnBrELyK5IBJzwwYl7/wNeY5ZQFbxAC6yZUiYkyVt+w==";
        }
    }
}
