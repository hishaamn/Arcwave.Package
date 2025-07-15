using Sitecore.Diagnostics;
using Sitecore.Install.Framework;
using Sitecore.Install.Utils;

namespace Arcwave.Package.Helpers
{
    public class CustomEntrySorter : EntrySorter
    {
        private bool SkipFile { get; set; }

        public CustomEntrySorter(ISource<PackageEntry> baseSource, bool skipFile = false) : base(baseSource)
        {
            this.SkipFile = skipFile;
        }

        public override void Put(PackageEntry entry)
        {
            if(this.SkipFile && entry.Properties.ContainsKey("type") && entry.Properties["type"].Equals("file"))
            {
                Log.Info($"Skipping file {entry.Key}", this);

                return;
            }

            base.Put(entry);
        }
    }
}
