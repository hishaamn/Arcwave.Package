using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Install.BlobData;
using Sitecore.Install.Events;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Metadata;
using Sitecore.Install.Security;
using Sitecore.Install.Utils;
using Sitecore.Install.Zip;
using Sitecore.IO;
using Sitecore.Reflection;
using Sitecore.Web;
using Sitecore;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Arcwave.Package.Context;
using Arcwave.Package.Helpers;

namespace Arcwave.Package.Managers
{
    public class InstallerManager : MarshalByRefObject
    {
        /// <summary>Executes the post step.</summary>
        /// <param name="action">The action.</param>
        /// <param name="context">The context.</param>
        public void ExecutePostStep(string action, IProcessingContext context)
        {
            if (string.IsNullOrEmpty(action))
                return;
            try
            {
                Event.RaiseEvent("packageinstall:poststep:starting", (object)new InstallationEventArgs((IEnumerable<ItemUri>)new List<ItemUri>(), (IEnumerable<FileCopyInfo>)new List<FileCopyInfo>(), "packageinstall:poststep:starting"));
                action = action.Trim();
                if (action.StartsWith("/", StringComparison.InvariantCulture))
                    action = Globals.ServerUrl + action;
                if (action.IndexOf("://", StringComparison.InvariantCulture) > -1)
                {
                    try
                    {
                        WebUtil.ExecuteWebPage(action);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error executing post step for package", ex, (object)this);
                    }
                }
                else
                {
                    object obj = (object)null;
                    try
                    {
                        obj = ReflectionUtil.CreateObject(action);
                    }
                    catch
                    {
                    }
                    if (obj != null)
                    {
                        if (obj is IPostStep)
                        {
                            ITaskOutput output = context.Output;
                            NameValueCollection metadata = new MetadataView(context).Metadata;
                            (obj as IPostStep).Run(output, metadata);
                        }
                        else
                            ReflectionUtil.CallMethod(obj, "RunPostStep");
                    }
                    else
                        Log.Error(string.Format("Execution of post step failed: Class '{0}' wasn't found.", (object)action), (object)this);
                }
            }
            finally
            {
                Event.RaiseEvent("packageinstall:poststep:ended", (object)new InstallationEventArgs((IEnumerable<ItemUri>)new List<ItemUri>(), (IEnumerable<FileCopyInfo>)new List<FileCopyInfo>(), "packageinstall:poststep:ended"));
                Event.RaiseEvent("packageinstall:ended", (object)new InstallationEventArgs((IEnumerable<ItemUri>)new List<ItemUri>(), (IEnumerable<FileCopyInfo>)new List<FileCopyInfo>(), "packageinstall:ended"));
            }
        }

        /// <summary>Gets the filename.</summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The filename.</returns>
        public static string GetFilename(string filename)
        {
            Error.AssertString(filename, nameof(filename), true);
            string filename1 = filename;
            if (!FileUtil.IsFullyQualified(filename1))
                filename1 = FileUtil.MakePath(Settings.PackagePath, filename1);
            return filename1;
        }

        /// <summary>Gets the post step.</summary>
        /// <param name="context">The context.</param>
        /// <returns>The post step.</returns>
        public static string GetPostStep(IProcessingContext context) => StringUtil.GetString(new MetadataView(context).PostStep);

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        public void InstallPackage(string path) => this.InstallPackage(path, CreateInstallationContext());

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="registerInstallation">if set to <c>true</c> the package installation will be registered.</param>
        public void InstallPackage(string path, bool registerInstallation) => this.InstallPackage(path, registerInstallation, CreateInstallationContext());

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="source">The source.</param>
        public void InstallPackage(string path, ISource<PackageEntry> source) => this.InstallPackage(path, source, CreateInstallationContext());

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="registerInstallation">if set to <c>true</c> the package installation will be registered.</param>
        /// <param name="source">The source.</param>
        public void InstallPackage(
          string path,
          bool registerInstallation,
          ISource<PackageEntry> source)
        {
            this.InstallPackage(path, registerInstallation, source, CreateInstallationContext());
        }

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="context">The processing context.</param>
        public void InstallPackage(string path, IProcessingContext context)
        {
            ISource<PackageEntry> source = (ISource<PackageEntry>)new PackageReader(path);
            this.InstallPackage(path, source, context);
        }

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="registerInstallation">if set to <c>true</c> package installation will be registered.</param>
        /// <param name="context">The processing context.</param>
        public void InstallPackage(string path, bool registerInstallation, IProcessingContext context)
        {
            ISource<PackageEntry> source = (ISource<PackageEntry>)new PackageReader(path);
            this.InstallPackage(path, registerInstallation, source, context);
        }

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="source">The source.</param>
        /// <param name="context">The processing context.</param>
        public void InstallPackage(
          string path,
          ISource<PackageEntry> source,
          IProcessingContext context)
        {
            this.InstallPackage(path, true, source, context);
        }

        /// <summary>Installs the package.</summary>
        /// <param name="path">The path.</param>
        /// <param name="registerInstallation">if set to <c>true</c> [register installation].</param>
        /// <param name="source">The source.</param>
        /// <param name="context">The processing context.</param>
        public void InstallPackage(
          string path,
          bool registerInstallation,
          ISource<PackageEntry> source,
          IProcessingContext context)
        {
            Event.RaiseEvent("packageinstall:starting", (object)new InstallationEventArgs((IEnumerable<ItemUri>)null, (IEnumerable<FileCopyInfo>)null, "packageinstall:starting"));
            Log.Info("Installing package: " + path, (object)this);
            using (new PackageInstallationContext())
            {
                using (ConfigWatcher.PostponeEvents())
                {
                    ISink<PackageEntry> installerSink = CreateInstallerSink(context);
                    
                    if(context is ExtSimpleProcessingContext extSimpleProcessingContext)
                    {
                        new CustomEntrySorter(source, extSimpleProcessingContext.SkipFile).Populate(installerSink);
                    }
                    else
                    {
                        new CustomEntrySorter(source).Populate(installerSink);
                    }
                    
                    installerSink.Flush();
                    installerSink.Finish();
                    if (registerInstallation)
                        this.RegisterPackage(context);
                    foreach (IProcessor<IProcessingContext> postAction in (IEnumerable<IProcessor<IProcessingContext>>)context.PostActions)
                        postAction.Process(context, context);
                }
            }
            TelemetryManager.TelemetryClient.Track(TelemetryManager.Packager.PackagesInstalled, 1UL);
        }

        /// <summary>Installs the security accounts from the package.</summary>
        /// <param name="path">The path to the package file.</param>
        public void InstallSecurity(string path)
        {
            Assert.ArgumentNotNullOrEmpty(path, nameof(path));
            this.InstallSecurity(path, (IProcessingContext)new SimpleProcessingContext());
        }

        /// <summary>Installs the security accounts from the package.</summary>
        /// <param name="path">The path to the package file.</param>
        /// <param name="context">The context.</param>
        public void InstallSecurity(string path, IProcessingContext context)
        {
            Assert.ArgumentNotNullOrEmpty(path, nameof(path));
            Assert.ArgumentNotNull((object)context, nameof(context));
            Log.Info("Installing security from package: " + path, (object)this);
            PackageReader packageReader = new PackageReader(path);
            AccountInstaller accountInstaller = new AccountInstaller();
            accountInstaller.Initialize(context);
            packageReader.Populate((ISink<PackageEntry>)accountInstaller);
            accountInstaller.Flush();
            accountInstaller.Finish();
        }

        /// <summary>Creates an installer sink.</summary>
        /// <returns>An installer sink.</returns>
        public static ISink<PackageEntry> CreateInstallerSink(IProcessingContext context)
        {
            SinkDispatcher installerSink = new SinkDispatcher(context);
            installerSink.AddSink(Sitecore.Install.Constants.MetadataPrefix, (ISink<PackageEntry>)new MetadataSink(context));
            installerSink.AddSink(Sitecore.Install.Constants.BlobDataPrefix, (ISink<PackageEntry>)new BlobInstaller(context));
            installerSink.AddSink(Sitecore.Install.Constants.ItemsPrefix, (ISink<PackageEntry>)new LegacyItemUnpacker((ISink<PackageEntry>)new ItemInstaller(context)));
            installerSink.AddSink(Sitecore.Install.Constants.FilesPrefix, (ISink<PackageEntry>)new FileInstaller(context));
            return (ISink<PackageEntry>)installerSink;
        }

        /// <summary>Creates the installation context.</summary>
        /// <returns>The installation context.</returns>
        public static IProcessingContext CreateInstallationContext(bool skipFile = false) => (IProcessingContext)new ExtSimpleProcessingContext(skipFile);

        /// <summary>Creates the preview context.</summary>
        /// <returns>The preview context.</returns>
        public static IProcessingContext CreatePreviewContext() => (IProcessingContext)new SimpleProcessingContext()
        {
            SkipData = true,
            SkipErrors = true,
            SkipCompression = true,
            ShowSourceInfo = true
        };

        /// <summary>
        /// Creates an item with information about package installation.
        /// </summary>
        /// <param name="context">The context.</param>
        protected virtual void RegisterPackage(IProcessingContext context)
        {
            Assert.ArgumentNotNull((object)context, nameof(context));
            MetadataView metadataView = new MetadataView(context);
            string name = metadataView.PackageName;
            bool flag;
            try
            {
                flag = ItemUtil.IsItemNameValid(name);
            }
            catch (Exception ex)
            {
                flag = false;
            }
            if (!flag && name.Length > 0)
                name = ItemUtil.ProposeValidItemName(name);
            if (name.Length == 0)
                name = Translate.Text("Unnamed Package");
            Item registrationItem = this.CreateRegistrationItem(name);
            if (registrationItem != null)
            {
                registrationItem.Editing.BeginEdit();
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageName] = metadataView.PackageName;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageID] = metadataView.PackageID;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageVersion] = metadataView.Version;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageAuthor] = metadataView.Author;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackagePublisher] = metadataView.Publisher;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageReadme] = metadataView.Readme;
                registrationItem[Sitecore.Install.PackageRegistrationFieldIDs.PackageRevision] = metadataView.Revision;
                registrationItem.Editing.EndEdit();
            }
            else
                Log.Error("Could not get registration item for package: " + name, (object)this);
        }

        /// <summary>Creates the registration item.</summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        private Item CreateRegistrationItem(string name)
        {
            Database database = Factory.GetDatabase("core");
            if (database != null)
            {
                TemplateItem template1 = database.Templates[TemplateIDs.Node];
                TemplateItem template2 = database.Templates[TemplateIDs.PackageRegistration];
                if (template1 != null && template2 != null)
                {
                    string path = "/sitecore/system/Packages/Installation history/" + name + "/" + DateUtil.IsoNow;
                    return database.CreateItemPath(path, template1, template2);
                }
            }
            return (Item)null;
        }

        /// <summary>Restarts the sitecore server.</summary>
        public static void RestartServer() => new FileInfo(FileUtil.MapPath("/web.config")).LastWriteTimeUtc = DateTime.UtcNow;
    }
}
