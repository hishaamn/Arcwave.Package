using Sitecore.Install.Framework;

namespace Arcwave.Package.Context
{
    public class ExtSimpleProcessingContext : SimpleProcessingContext
    {
        public ExtSimpleProcessingContext(bool skip)
        {
            this.SkipFile = skip;
        }

        public bool SkipFile { get; set; }
    }
}
