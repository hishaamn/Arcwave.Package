using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Install.Events;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Metadata;
using Sitecore.Install.Utils;
using Sitecore.Install.Zip;
using Sitecore.IO;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Jobs;
using Sitecore.Shell.Framework;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI;
using Sitecore.Web;
using Sitecore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Arcwave.Package.Managers;
using Sitecore.Configuration;
using Sitecore.SecurityModel;
using Sitecore.Data.Engines;
using Sitecore.Install.Items;
using Sitecore.Install.Security;
using Arcwave.Package.Helpers;

namespace Arcwave.Package.Dialogs
{
    public class InstallPackageForms : WizardForm
    {
        /// <summary></summary>
        protected Edit PackageFile;
        /// <summary></summary>
        protected Edit PackageName;
        /// <summary></summary>
        protected Edit Version;
        /// <summary></summary>
        protected Edit Author;
        /// <summary></summary>
        protected Edit Publisher;
        /// <summary></summary>
        protected Border LicenseAgreement;
        /// <summary></summary>
        protected Memo ReadmeText;
        /// <summary></summary>
        protected Radiobutton Decline;
        /// <summary></summary>
        protected Radiobutton Accept;
        /// <summary></summary>
        protected Checkbox Restart;
        /// <summary></summary>
        protected Checkbox RestartServer;
        /// <summary></summary>
        protected JobMonitor Monitor;
        /// <summary></summary>
        protected Literal FailingReason;
        /// <summary></summary>
        protected Literal ErrorDescription;
        /// <summary></summary>
        protected Border SuccessMessage;
        /// <summary></summary>
        protected Border ErrorMessage;
        /// <summary></summary>
        protected Border AbortMessage;

        protected DataContext DataContext;

        protected Treeview Treeview;

        protected Listview ViewPackageContent;

        protected Checkbox SkipFile;

        /// <summary>Synchronization object for current step</summary>
        private readonly object CurrentStepSync = new object();

        /// <summary>
        /// Gets or sets a value indicating whether this instance has license.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has license; otherwise, <c>false</c>.
        /// </value>
        public bool HasLicense
        {
            get => MainUtil.GetBool(Sitecore.Context.ClientPage.ServerProperties[nameof(HasLicense)], false);
            set => Sitecore.Context.ClientPage.ServerProperties[nameof(HasLicense)] = (object)value.ToString();
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has readme.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has readme; otherwise, <c>false</c>.
        /// </value>
        public bool HasReadme
        {
            get => MainUtil.GetBool(Sitecore.Context.ClientPage.ServerProperties["Readme"], false);
            set => Sitecore.Context.ClientPage.ServerProperties["Readme"] = (object)value.ToString();
        }

        /// <summary>Gets or sets the post action.</summary>
        /// <value>The post action.</value>
        private string PostAction
        {
            get => StringUtil.GetString(this.ServerProperties["postAction"]);
            set => this.ServerProperties["postAction"] = (object)value;
        }

        /// <summary>
        /// Gets or sets the installation step. 0 means installing items and files, 1 means installing security accounts.
        /// </summary>
        /// <value>The installation step.</value>
        private InstallPackageForms.InstallationSteps CurrentStep
        {
            get => (InstallPackageForms.InstallationSteps)this.ServerProperties["installationStep"];
            set
            {
                lock (this.CurrentStepSync)
                    this.ServerProperties["installationStep"] = (object)(int)value;
            }
        }

        /// <summary>Gets or sets the package version.</summary>
        /// <value>The package version.</value>
        private int PackageVersion
        {
            get => int.Parse(StringUtil.GetString(this.ServerProperties["packageType"], "1"));
            set => this.ServerProperties["packageType"] = (object)value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this installation is successful.
        /// </summary>
        /// <value><c>true</c> if successful; otherwise, <c>false</c>.</value>
        private bool Successful
        {
            get => !(this.ServerProperties[nameof(Successful)] is bool serverProperty) || serverProperty;
            set => this.ServerProperties[nameof(Successful)] = (object)value;
        }

        /// <summary>Gets or sets the main installation task ID.</summary>
        /// <value>The main installation task ID.</value>
        private string MainInstallationTaskID
        {
            get => StringUtil.GetString(this.ServerProperties["taskID"]);
            set => this.ServerProperties["taskID"] = (object)value;
        }

        private bool Cancelling
        {
            get => MainUtil.GetBool(Sitecore.Context.ClientPage.ServerProperties["__cancelling"], false);
            set => Sitecore.Context.ClientPage.ServerProperties["__cancelling"] = (object)value;
        }

        private string OriginalNextButtonHeader
        {
            get => StringUtil.GetString(Sitecore.Context.ClientPage.ServerProperties["next-header"]);
            set => Sitecore.Context.ClientPage.ServerProperties["next-header"] = (object)value;
        }

        /// <summary>On "Cancel" click</summary>
        public new void Cancel()
        {
            int num = this.Pages.IndexOf(this.Active);
            if (num == 0 || num == this.Pages.Count - 1)
            {
                this.Cancelling = num == 0;
                this.EndWizard();
            }
            else
            {
                this.Cancelling = true;
                Sitecore.Context.ClientPage.Start((object)this, "Confirmation");
            }
        }

        /// <summary></summary>
        protected override void OnLoad(EventArgs e)
        {
            TelemetryManager.TelemetryClient.Track(TelemetryManager.Packager.InstallActivated, 1UL);
            TelemetryManager.TelemetryClient.Track(TelemetryManager.Packager.InstallOpened, 1UL);

            this.DataContext.GetFromQueryString();

            if (!Sitecore.Context.ClientPage.IsEvent)
                this.OriginalNextButtonHeader = this.NextButton.Header;
            base.OnLoad(e);
            this.Monitor = DialogUtils.AttachMonitor(this.Monitor);
            if (!Sitecore.Context.ClientPage.IsEvent)
            {
                this.ViewPackageContent = new Listview();
                this.PackageFile.Value = Registry.GetString("Packager/File");
                this.Decline.Checked = true;
                this.Restart.Checked = true;
                this.RestartServer.Checked = false;
            }
            this.Monitor.JobFinished += new EventHandler(this.Monitor_JobFinished);
            this.Monitor.JobDisappeared += new EventHandler(this.Monitor_JobDisappeared);
            this.WizardCloseConfirmationText = "Are you sure you want to cancel installing a package.";
        }

        /// <summary>Called when the active page is changing.</summary>
        /// <param name="page">The page that is being left.</param>
        /// <param name="newpage">The new page that is being entered.</param>
        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            bool flag1 = base.ActivePageChanging(page, ref newpage);
            if (page == "LoadPackage" && newpage == "License")
            {
                bool flag2 = this.LoadPackage();

                if (flag2)
                {
                    this.LoadPackageContent();
                }

                if (!this.HasLicense)
                {
                    newpage = "Readme";
                    if (!this.HasReadme)
                        newpage = "InstallationSelector";
                }
                return flag2;
            }
            if (page == "License" && newpage == "Readme")
            {
                if (!this.HasReadme)
                    newpage = "InstallationSelector";
                return flag1;
            }
            if (page == "InstallationSelector" && newpage == "Ready")
            {
                if (!this.HasReadme)
                    newpage = "Ready";
                return flag1;
            }
            if (page == "Ready" && newpage == "Readme")
            {
                if (!this.HasReadme)
                {
                    newpage = "License";
                    if (!this.HasLicense)
                        newpage = "LoadPackage";
                }
                return flag1;
            }
            if (page == "Readme" && newpage == "License" && !this.HasLicense)
                newpage = "LoadPackage";
            return flag1;
        }

        /// <summary>Called when the active page has been changed.</summary>
        /// <param name="page">The page that has been entered.</param>
        /// <param name="oldPage">The page that was left.</param>
        protected override void ActivePageChanged(string page, string oldPage)
        {
            base.ActivePageChanged(page, oldPage);
            this.NextButton.Header = this.OriginalNextButtonHeader;
            if (page == "License" && oldPage == "LoadPackage")
                this.NextButton.Disabled = !this.Accept.Checked;
            if (page == "Installing")
            {
                this.BackButton.Disabled = true;
                this.NextButton.Disabled = true;
                this.CancelButton.Disabled = true;
                Sitecore.Context.ClientPage.SendMessage((object)this, "installer:startInstallation");
            }
            if (page == "Ready")
                this.NextButton.Header = Translate.Text("Install");
            if (page == "LastPage")
                this.BackButton.Disabled = true;
            if (this.Successful)
                return;
            this.CancelButton.Header = Translate.Text("Close");
            this.Successful = true;
        }

        /// <summary></summary>
        protected override void EndWizard()
        {
            if (!this.Cancelling)
            {
                if (this.RestartServer.Checked)
                    InstallerManager.RestartServer();
                if (this.Restart.Checked)
                    Sitecore.Context.ClientPage.ClientResponse.Broadcast(Sitecore.Context.ClientPage.ClientResponse.SetLocation(string.Empty), "Shell");
            }
            Windows.Close();
        }

        /// <summary></summary>
        protected override void OnCancel(object sender, EventArgs formEventArgs) => this.Cancel();

        /// <summary></summary>
        protected void Done()
        {
            this.Active = "LastPage";
            this.BackButton.Disabled = true;
            this.NextButton.Disabled = true;
            this.CancelButton.Disabled = false;
        }

        /// <summary>Starts the installation.</summary>
        /// <param name="message">The message.</param>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:startInstallation")]
        protected void StartInstallation(Message message)
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            this.CurrentStep = InstallationSteps.MainInstallation;
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

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:browse", true)]
        protected void Browse(ClientPipelineArgs args) => DialogUtils.Browse(args, this.PackageFile);

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:upload", true)]
        protected void Upload(ClientPipelineArgs args) => DialogUtils.Upload(args, this.PackageFile);

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:savePostAction")]
        protected void SavePostAction(Message msg) => this.PostAction = msg.Arguments[0];

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:doPostAction")]
        protected void DoPostAction(Message msg)
        {
            if (string.IsNullOrEmpty(this.PostAction))
                return;
            this.StartPostAction();
        }

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:aborted")]
        protected void OnInstallerAborted(Message message)
        {
            this.GotoLastPage(InstallPackageForms.Result.Abort, string.Empty, string.Empty);
            this.CurrentStep = InstallPackageForms.InstallationSteps.Failed;
        }

        /// <summary></summary>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:failed")]
        protected void OnInstallerFailed(Message message)
        {
            BaseJob job = JobManager.GetJob(this.Monitor.JobHandle);
            Assert.IsNotNull((object)job, "Job is not available");
            Exception result = job.Status.Result as Exception;
            Error.AssertNotNull((object)result, "Cannot get any exception details");
            this.GotoLastPage(InstallPackageForms.Result.Failure, InstallPackageForms.GetShortDescription(result), InstallPackageForms.GetFullDescription(result));
            this.CurrentStep = InstallPackageForms.InstallationSteps.Failed;
        }

        /// <summary></summary>
        [ProcessorMethod]
        protected void Agree()
        {
            this.NextButton.Disabled = false;
            Sitecore.Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        /// <summary></summary>
        [ProcessorMethod]
        protected void Disagree()
        {
            this.NextButton.Disabled = true;
            Sitecore.Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        /// <summary></summary>
        protected void RestartInstallation()
        {
            this.Active = "Ready";
            this.CancelButton.Visible = true;
            this.CancelButton.Disabled = false;
            this.NextButton.Visible = true;
            this.NextButton.Disabled = false;
            this.BackButton.Visible = false;
        }

        private static string GetFullDescription(Exception e) => e.ToString();

        private static string GetShortDescription(Exception e)
        {
            string message = e.Message;
            int num = message.IndexOf("(method:", StringComparison.InvariantCulture);
            return num > -1 ? message.Substring(0, num - 1) : message;
        }

        private static void SetVisibility(Control control, bool visible) => Sitecore.Context.ClientPage.ClientResponse.SetStyle(control.ID, "display", visible ? "" : "none");

        /// <summary>Sets the task ID.</summary>
        /// <param name="message">The message.</param>
        [Sitecore.Web.UI.Sheer.HandleMessage("installer:setTaskId")]
        private void SetTaskID(Message message)
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            Assert.IsNotNull((object)message["id"], "id");
            this.MainInstallationTaskID = message["id"];
        }

        [Sitecore.Web.UI.Sheer.HandleMessage("installer:commitingFiles")]
        private void OnCommittingFiles(Message message)
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            lock (this.CurrentStepSync)
            {
                if (this.CurrentStep != InstallPackageForms.InstallationSteps.MainInstallation)
                    return;
                this.CurrentStep = InstallPackageForms.InstallationSteps.WaitForFiles;
                this.WatchForInstallationStatus();
            }
        }

        private void Monitor_JobFinished(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull((object)e, nameof(e));
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallPackageForms.InstallationSteps.MainInstallation:
                        this.CurrentStep = InstallPackageForms.InstallationSteps.WaitForFiles;
                        this.WatchForInstallationStatus();
                        break;
                    case InstallPackageForms.InstallationSteps.WaitForFiles:
                        this.CurrentStep = InstallPackageForms.InstallationSteps.InstallSecurity;
                        this.StartInstallingSecurity();
                        break;
                    case InstallPackageForms.InstallationSteps.InstallSecurity:
                        this.CurrentStep = InstallPackageForms.InstallationSteps.RunPostAction;
                        if (string.IsNullOrEmpty(this.PostAction))
                        {
                            this.GotoLastPage(InstallPackageForms.Result.Success, string.Empty, string.Empty);
                            break;
                        }
                        this.StartPostAction();
                        break;
                    case InstallPackageForms.InstallationSteps.RunPostAction:
                        this.GotoLastPage(InstallPackageForms.Result.Success, string.Empty, string.Empty);
                        break;
                }
            }
        }

        private void Monitor_JobDisappeared(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull((object)e, nameof(e));
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallPackageForms.InstallationSteps.MainInstallation:
                        this.GotoLastPage(InstallPackageForms.Result.Failure, Translate.Text("Installation could not be completed."), Translate.Text("Installation job was interrupted unexpectedly."));
                        break;
                    case InstallPackageForms.InstallationSteps.WaitForFiles:
                        this.WatchForInstallationStatus();
                        break;
                    default:
                        this.Monitor_JobFinished(sender, e);
                        break;
                }
            }
        }

        private void GotoLastPage(
          InstallPackageForms.Result result,
          string shortDescription,
          string fullDescription)
        {
            this.ErrorDescription.Text = fullDescription;
            this.FailingReason.Text = shortDescription;
            this.Cancelling = result != 0;
            InstallPackageForms.SetVisibility((Control)this.SuccessMessage, result == InstallPackageForms.Result.Success);
            InstallPackageForms.SetVisibility((Control)this.ErrorMessage, result == InstallPackageForms.Result.Failure);
            InstallPackageForms.SetVisibility((Control)this.AbortMessage, result == InstallPackageForms.Result.Abort);
            Event.RaiseEvent("packageinstall:ended", (object)new InstallationEventArgs((IEnumerable<ItemUri>)new List<ItemUri>(), (IEnumerable<FileCopyInfo>)new List<FileCopyInfo>(), "packageinstall:ended"));
            this.Successful = result == InstallPackageForms.Result.Success;
            this.Active = "LastPage";
        }

        private bool LoadPackage()
        {
            string str = this.PackageFile.Value;
            if (Path.GetExtension(str).Trim().Length == 0)
            {
                str = Path.ChangeExtension(str, ".zip");
                this.PackageFile.Value = str;
            }
            if (str.Trim().Length == 0)
            {
                Sitecore.Context.ClientPage.ClientResponse.Alert("Please specify a package.");
                return false;
            }
            string filename = InstallerManager.GetFilename(str);
            if (!FileUtil.FileExists(filename))
            {
                Sitecore.Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" file does not exist.", (object)filename));
                return false;
            }
            IProcessingContext previewContext = InstallerManager.CreatePreviewContext();
            ISource<PackageEntry> source = (ISource<PackageEntry>)new PackageReader(MainUtil.MapPath(filename));
            MetadataView view = new MetadataView(previewContext);
            MetadataSink metadataSink = new MetadataSink(view);
            metadataSink.Initialize(previewContext);
            source.Populate((ISink<PackageEntry>)metadataSink);
            if (previewContext == null || previewContext.Data == null)
            {
                Sitecore.Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" could not be loaded.\n\nThe file maybe corrupt.", (object)filename));
                return false;
            }
            this.PackageVersion = previewContext.Data.ContainsKey("installer-version") ? 2 : 1;
            this.PackageName.Value = view.PackageName;
            this.Version.Value = view.Version;
            this.Author.Value = view.Author;
            this.Publisher.Value = view.Publisher;
            this.LicenseAgreement.InnerHtml = view.License;
            this.ReadmeText.Value = view.Readme;
            this.HasLicense = view.License.Length > 0;
            this.HasReadme = view.Readme.Length > 0;
            this.PostAction = view.PostStep;
            Registry.SetString("Packager/File", this.PackageFile.Value);
            return true;
        }

        private void LoadPackageContent()
        {
            var filePath = MainUtil.MapPath(InstallerManager.GetFilename(this.PackageFile.Value));

            ISource<PackageEntry> source = new PackageReader(filePath);

            var packageSinkHelper = new PackageSinkHelper(this.ViewPackageContent);
            packageSinkHelper.Initialize(InstallerManager.CreatePreviewContext());
            new EntrySorter(source).Populate(packageSinkHelper);

            var view = packageSinkHelper.PackageView();

            this.ViewPackageContent = view;

            Sitecore.Context.ClientPage.ClientResponse.Refresh(this.ViewPackageContent);
        }

        private void StartTask(string packageFile, bool skipFile) => this.Monitor.Start("Install", "Install", new ThreadStart(new InstallPackageForms.AsyncHelper(packageFile, skipFile).Install));

        private void WatchForInstallationStatus() => this.Monitor.Start("WatchStatus", "Install", new ThreadStart(new InstallPackageForms.AsyncHelper().SetStatusFile(FileInstaller.GetStatusFileName(this.MainInstallationTaskID)).WatchForStatus));

        private void StartInstallingSecurity() => this.Monitor.Start("InstallSecurity", "Install", new ThreadStart(new InstallPackageForms.AsyncHelper(InstallerManager.GetFilename(this.PackageFile.Value), false).InstallSecurity));

        private void StartPostAction()
        {
            if (this.Monitor.JobHandle != Handle.Null)
            {
                Log.Info("Waiting for installation task completion", (object)this);
                SheerResponse.Timer("installer:doPostAction", 100);
            }
            else
            {
                string postAction = this.PostAction;
                this.PostAction = string.Empty;
                if (postAction.IndexOf("://", StringComparison.InvariantCulture) < 0 && postAction.StartsWith("/", StringComparison.InvariantCulture))
                    postAction = WebUtil.GetServerUrl() + postAction;
                this.Monitor.Start("RunPostAction", "Install", new ThreadStart(new InstallPackageForms.AsyncHelper(postAction, this.GetContextWithMetadata()).ExecutePostStep));
            }
        }

        private IProcessingContext GetContextWithMetadata()
        {
            string filename = InstallerManager.GetFilename(this.PackageFile.Value);
            IProcessingContext previewContext = InstallerManager.CreatePreviewContext();
            ISource<PackageEntry> source = (ISource<PackageEntry>)new PackageReader(MainUtil.MapPath(filename));
            MetadataSink metadataSink = new MetadataSink(new MetadataView(previewContext));
            metadataSink.Initialize(previewContext);
            source.Populate((ISink<PackageEntry>)metadataSink);
            return previewContext;
        }

        private class AsyncHelper
        {
            private string _packageFile;
            private bool _skipFile;
            private string _postAction;
            private IProcessingContext _context;
            private StatusFile _statusFile;
            private Language _language;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForms.AsyncHelper" /> class.
            /// </summary>
            /// <param name="package">The package.</param>
            public AsyncHelper(string package, bool skipFile)
            {
                this._packageFile = package;
                this._skipFile = skipFile;
                this._language = Sitecore.Context.Language;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForms.AsyncHelper" /> class.
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
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForms.AsyncHelper" /> class.
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

            /// <summary>Installs the security.</summary>
            public void InstallSecurity() => this.CatchExceptions((ThreadStart)(() =>
            {
                using (new LanguageSwitcher(this._language))
                {
                    IProcessingContext installationContext = InstallerManager.CreateInstallationContext();
                    installationContext.AddAspect<IAccountInstallerEvents>(new UiInstallerEvents());
                    new InstallerManager().InstallSecurity(PathUtils.MapPath(this._packageFile), installationContext);
                }
            }));

            /// <summary>Sets the status file.</summary>
            /// <param name="filename">The filename.</param>
            /// <returns>The status file.</returns>
            public InstallPackageForms.AsyncHelper SetStatusFile(string filename)
            {
                this._statusFile = new StatusFile(filename);
                return this;
            }

            /// <summary>Watches for status.</summary>
            /// <exception cref="T:System.Exception"><c>Exception</c>.</exception>
            public void WatchForStatus() => this.CatchExceptions((ThreadStart)(() =>
            {
                Assert.IsNotNull((object)this._statusFile, "Internal error: status file not set.");
                bool flag = false;
                do
                {
                    StatusFile.StatusInfo statusInfo = this._statusFile.ReadStatus();
                    if (statusInfo != null)
                    {
                        switch (statusInfo.Status)
                        {
                            case StatusFile.Status.Finished:
                                flag = true;
                                break;
                            case StatusFile.Status.Failed:
                                throw new Exception("Background process failed: " + statusInfo.Exception.Message, statusInfo.Exception);
                        }
                        Thread.Sleep(100);
                    }
                }
                while (!flag);
            }));

            /// <summary></summary>
            public void ExecutePostStep() => this.CatchExceptions((ThreadStart)(() => new InstallerManager().ExecutePostStep(this._postAction, this._context)));

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
