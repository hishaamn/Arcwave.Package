using Sitecore.Globalization;
using Sitecore.Install;
using Sitecore.Install.Framework;
using Sitecore.Install.Utils;
using Sitecore.Web.UI.HtmlControls;
using System.Text;

namespace Arcwave.Package.Helpers
{
    public class PackageSinkHelper : BaseSink<PackageEntry>
    {
        private readonly Listview view;

        public PackageSinkHelper(Listview view)
        {
            this.view = view;
        }   

        public Listview PackageView()
        {
            return this.view;
        }

        public override void Put(PackageEntry entry)
        {            
            ListviewItem listviewItem = this.AddItem(entry.Key);
            listviewItem.ColumnValues["key"] = (object)entry.Key;
            listviewItem.ColumnValues["source"] = (object)PackageUtils.TryGetValue<string, string>(entry.Properties, "source");
            listviewItem.ColumnValues["options"] = (object)this.FormatInstallOptions(new BehaviourOptions(entry.Properties, Constants.IDCollisionPrefix));
            string str;
            if (entry.Attributes.TryGetValue("icon", out str))
                listviewItem.Icon = str;
            //++this.count;
            //if (this.count >= 100)
            //    throw new SourceViewer.LoopAbortException();
        }

        private ListviewItem AddItem(string header)
        {
            ListviewItem listviewItem = new ListviewItem();
            listviewItem.ID = Control.GetUniqueID("I");
            Sitecore.Context.ClientPage.AddControl(this.view, listviewItem);
            listviewItem.Header = header;
            return listviewItem;
        }

        private string FormatInstallOptions(BehaviourOptions options)
        {
            if (!options.IsDefined)
                return Translate.Text("Undefined");
            InstallMode itemMode = options.ItemMode;
            if (itemMode == InstallMode.Undefined)
                return Translate.Text("Ask User");
            StringBuilder stringBuilder = new StringBuilder(50);
            stringBuilder.Append(Translate.Text(itemMode.ToString()));
            if (itemMode == InstallMode.Merge)
            {
                stringBuilder.Append(" / ");
                stringBuilder.Append(Translate.Text(options.ItemMergeMode.ToString()));
            }
            return stringBuilder.ToString();
        }
    }
}
