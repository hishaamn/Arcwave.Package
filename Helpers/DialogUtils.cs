using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Shell.Applications.Install;
using Sitecore.Shell.Applications.Install.Dialogs;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arcwave.Package.Helpers
{
    internal class DialogUtils
    {
        /// <summary>Checks whether a directory for packages exists</summary>
        public static void CheckPackageFolder()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(ApplicationContext.PackagePath);
            bool flag1 = FileUtil.FolderExists(directoryInfo.FullName);
            bool flag2 = directoryInfo.Parent != null && FileUtil.FolderExists(directoryInfo.Parent.FullName);
            bool flag3 = FileUtil.FilePathHasInvalidChars(ApplicationContext.PackagePath);
            if (flag2 && !flag3 && !flag1)
            {
                Directory.CreateDirectory(ApplicationContext.PackagePath);
                Log.Warn(string.Format("The '{0}' folder was not found and has been created. Please check your Sitecore configuration.", (object)ApplicationContext.PackagePath), (object)typeof(DialogUtils));
            }
            if (!Directory.Exists(ApplicationContext.PackagePath))
                throw new ClientAlertException(string.Format(Translate.Text("Cannot access path '{0}'. Please check PackagePath setting in the web.config file."), (object)ApplicationContext.PackagePath));
        }

        /// <summary></summary>
        public static void Browse(ClientPipelineArgs args, Edit fileEdit)
        {
            try
            {
                DialogUtils.CheckPackageFolder();
                if (args.IsPostBack)
                {
                    if (!args.HasResult || fileEdit == null)
                        return;
                    fileEdit.Value = args.Result;
                }
                else
                {
                    BrowseDialog.BrowseForOpen(ApplicationContext.PackagePath, "*.zip", "Choose Package", "Click the package that you want to install and then click Open.", "People/16x16/box.png");
                    args.WaitForPostBack();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to browse file", ex, typeof(DialogUtils));
                SheerResponse.Alert(ex.Message);
            }
        }

        /// <summary></summary>
        public static void Upload(ClientPipelineArgs args, Edit fileEdit)
        {
            try
            {
                DialogUtils.CheckPackageFolder();
                if (!args.IsPostBack)
                {
                    UploadPackageForm.Show(ApplicationContext.PackagePath, true);
                    args.WaitForPostBack();
                }
                else
                {
                    if (!args.Result.StartsWith("ok:", StringComparison.InvariantCulture))
                        return;
                    string[] strArray = args.Result.Substring("ok:".Length).Split('|');
                    if (strArray.Length < 1 || fileEdit == null)
                        return;
                    fileEdit.Value = strArray[0];
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to upload file: " + args.Result, ex, typeof(DialogUtils));
                SheerResponse.Alert(ex.Message);
            }
        }

        /// <summary></summary>
        public static JobMonitor AttachMonitor(JobMonitor monitor)
        {
            if (monitor == null)
            {
                if (Sitecore.Context.ClientPage.IsEvent)
                {
                    monitor = Sitecore.Context.ClientPage.FindControl("Monitor") as JobMonitor;
                }
                else
                {
                    monitor = new JobMonitor();
                    monitor.ID = "Monitor";
                    Sitecore.Context.ClientPage.Controls.Add((System.Web.UI.Control)monitor);
                }
            }
            return monitor;
        }
    }
}
