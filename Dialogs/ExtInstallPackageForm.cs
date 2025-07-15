using Arcwave.Package.Managers;
using Sitecore.Configuration;
using Sitecore.Data.Engines;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Utils;
using Sitecore.IO;
using Sitecore.Jobs.AsyncUI;
using Sitecore.SecurityModel;
using Sitecore.Shell.Applications.Install.Dialogs.InstallPackage;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using System;
using System.Threading;

namespace Arcwave.Package.Dialogs
{
    public class ExtInstallPackageForm : InstallPackageForm
    {
        protected Checkbox SkipFile;

        [HandleMessage("installer:startInstallation")]
        protected new void StartInstallation(Message message)
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            this.ServerProperties["installationStep"] = InstallationSteps.MainInstallation;

            string filename = InstallerManager.GetFilename(this.PackageFile.Value);
            
            if (FileUtil.IsFile(filename))
            {
                this.StartTask(filename, this.SkipFile.Checked);
            }
            else
            {
                Sitecore.Context.ClientPage.ClientResponse.Alert("Package not found");
                this.Active = "Ready";
                this.BackButton.Disabled = true;
            }
        }

        private void StartTask(string packageFile, bool skipFile) => this.Monitor.Start("Install", "Install", new ThreadStart(new AsyncHelper(packageFile, skipFile).Install));

        private class AsyncHelper
        {
            private string _packageFile;
            private bool _skipFile;
            private string _postAction;
            private IProcessingContext _context;
            private StatusFile _statusFile;
            private Language _language;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            /// <param name="package">The package.</param>
            public AsyncHelper(string package, bool skipFile)
            {
                this._packageFile = package;
                this._skipFile = skipFile;
                this._language = Sitecore.Context.Language;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            /// <param name="postAction">The post action.</param>
            /// <param name="context">The context.</param>
            public AsyncHelper(string postAction, IProcessingContext context)
            {
                this._postAction = postAction;
                this._context = context;
                this._language = Sitecore.Context.Language;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            public AsyncHelper() => this._language = Sitecore.Context.Language;

            /// <summary>Performs installation.</summary>
            public void Install() => this.CatchExceptions((ThreadStart)(() =>
            {
                using (new SecurityDisabler())
                {
                    using (new SyncOperationContext())
                    {
                        using (new LanguageSwitcher(this._language))
                        {
                            using (VirtualDrive virtualDrive = new VirtualDrive(FileUtil.MapPath(Settings.TempFolderPath)))
                            {
                                SettingsSwitcher settingsSwitcher = (SettingsSwitcher)null;
                                try
                                {
                                    if (!string.IsNullOrEmpty(virtualDrive.Name))
                                        settingsSwitcher = new SettingsSwitcher("TempFolder", virtualDrive.Name);
                                    IProcessingContext installationContext = InstallerManager.CreateInstallationContext(this._skipFile);
                                    JobContext.PostMessage("installer:setTaskId(id=" + installationContext.TaskID + ")");
                                    installationContext.AddAspect<IItemInstallerEvents>(new UiInstallerEvents());
                                    installationContext.AddAspect<IFileInstallerEvents>(new UiInstallerEvents());
                                    new InstallerManager().InstallPackage(PathUtils.MapPath(this._packageFile), installationContext);
                                }
                                finally
                                {
                                    settingsSwitcher?.Dispose();
                                }
                            }
                        }
                    }
                }
            }));

            private void CatchExceptions(ThreadStart start)
            {
                try
                {
                    start();
                }
                catch (ThreadAbortException ex)
                {
                    if (!Environment.HasShutdownStarted)
                        Thread.ResetAbort();
                    Log.Info("Installation was aborted", (object)this);
                    JobContext.PostMessage("installer:aborted");
                    JobContext.Flush();
                }
                catch (Exception ex)
                {
                    Log.Error("Installation failed: " + (object)ex, (object)this);
                    JobContext.Job.Status.Result = (object)ex;
                    JobContext.PostMessage("installer:failed");
                    JobContext.Flush();
                }
            }
        }

        private enum InstallationSteps
        {
            MainInstallation,
            WaitForFiles,
            InstallSecurity,
            RunPostAction,
            None,
            Failed,
        }

        private enum Result
        {
            Success,
            Failure,
            Abort,
        }
    }
}
