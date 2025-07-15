using Sitecore.Collections;
using Sitecore.Globalization;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Security;
using Sitecore.Install.Utils;
using Sitecore.Jobs.AsyncUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Arcwave.Package.Dialogs
{
    public class UiInstallerEvents :
    IItemInstallerEvents,
    IFileInstallerEvents,
    IAccountInstallerEvents
    {
        private readonly List<string> _skippedMessages = new List<string>();

        /// <summary>
        /// Asks the user what to do when we have an item collision.
        /// </summary>
        /// <param name="databaseItem">The database item.</param>
        /// <param name="packageItem">The package item.</param>
        /// <param name="context">The context.</param>
        /// <returns>The user.</returns>
        public Pair<BehaviourOptions, bool> AskUser(
          ItemInfo databaseItem,
          ItemInfo packageItem,
          IProcessingContext context)
        {
            bool part2 = false;
            BehaviourOptions part1 = new BehaviourOptions();
            bool flag1 = databaseItem.ID.Equals(packageItem.ID);
            bool flag2 = flag1 || databaseItem.TemplateID.Equals(packageItem.TemplateID);
            Hashtable parameters = new Hashtable();
            parameters.Add((object)"id", (object)databaseItem.ID);
            parameters.Add((object)"ph", (object)databaseItem.Path);
            parameters.Add((object)"pc", (object)(!flag1).ToString());
            parameters.Add((object)"mo", (object)flag2);
            while (true)
            {
                string str = JobContext.ShowModalDialog(parameters, "Installer.GetPasteMode", "600", "375");
                if (str == "cancel" || str != null && str.Length == 0 || str == null)
                {
                    Thread.CurrentThread.Abort();
                }
                else
                {
                    string[] strArray = str.Split('|');
                    if (strArray.Length != 3)
                    {
                        Thread.CurrentThread.Abort();
                    }
                    else
                    {
                        part1.ItemMode = (InstallMode)Enum.Parse(typeof(InstallMode), strArray[0]);
                        part1.ItemMergeMode = (MergeMode)Enum.Parse(typeof(InstallMode), strArray[1]);
                        part2 = bool.Parse(strArray[2]);
                    }
                }
                if (part1.ItemMode == InstallMode.Undefined)
                    JobContext.Alert(Translate.Text("You should select an install mode"));
                else
                    break;
            }
            return new Pair<BehaviourOptions, bool>(part1, part2);
        }

        /// <summary>Requests calling party for overwrite event</summary>
        /// <param name="virtualPath">File path which is subject of request</param>
        /// <param name="context">Processing context</param>
        /// <returns>
        /// A pair of bools:
        /// <list type="bullet">
        /// 		<item>First: Whether overwrite is allowed or not</item>
        /// 		<item>Second: Whether decision should be applied to all subsequent files</item>
        /// 	</list>
        /// </returns>
        public Pair<bool, bool> RequestOverwrite(string virtualPath, IProcessingContext context)
        {
            switch (JobContext.ShowModalDialog(Translate.Text("Do you wish to overwrite the file \"{0}\"?", (object)virtualPath), "YesNoCancelAll", "700", "190"))
            {
                case "no":
                    return new Pair<bool, bool>(false, false);
                case "no to all":
                    return new Pair<bool, bool>(false, true);
                case "yes":
                    return new Pair<bool, bool>(true, false);
                case "yes to all":
                    return new Pair<bool, bool>(true, true);
                default:
                    Thread.CurrentThread.Abort();
                    return new Pair<bool, bool>(false, true);
            }
        }

        /// <summary>
        /// Event fired before starting the process of actually installing files to their locations in the site.
        /// </summary>
        public void BeforeCommit() => JobContext.SendMessage("installer:commitingFiles");

        public void ShowWarning(string message, string warningType)
        {
            if (this._skippedMessages.Contains(warningType))
                return;
            switch (JobContext.ShowModalDialog(message, "ContinueAlwaysAbort", "500", "190"))
            {
                case "continue":
                    break;
                case "always":
                    this._skippedMessages.Add(warningType);
                    break;
                case "abort":
                    Thread.CurrentThread.Abort();
                    break;
                default:
                    throw new Exception("Unexpected dialog value");
            }
        }
    }
}
