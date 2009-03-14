﻿//-----------------------------------------------------------------------
// <copyright file="TeamBuild.cs">(c) http://www.codeplex.com/MSBuildExtensionPack. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
// Task Contributors: Jakob Ehn and Steve Nuchia
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.Tfs
{
    using System.Globalization;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.TeamFoundation.Build.Client;
    using Microsoft.TeamFoundation.Client;

    /// <summary>
    /// <b>Valid TaskActions are:</b>
    /// <para><i>GetLatest</i> (<b>Required: </b>TeamFoundationServerUrl, TeamProject <b>Optional: </b>BuildDefinitionName, Status <b>Output: </b>Info)</para>
    /// <para><i>Queue</i> (<b>Required: </b>TeamFoundationServerUrl, TeamProject, BuildDefinitionName)</para>
    /// <para><b>Remote Execution Support:</b> NA</para>
    /// </summary>
    /// <example>
    /// <code lang="xml"><![CDATA[
    /// <Project ToolsVersion="3.5" DefaultTargets="Default" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    ///     <PropertyGroup>
    ///         <TPath>$(MSBuildProjectDirectory)\..\MSBuild.ExtensionPack.tasks</TPath>
    ///         <TPath Condition="Exists('$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks')">$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks</TPath>
    ///     </PropertyGroup>
    ///     <Import Project="$(TPath)"/>
    ///     <Target Name="Default">
    ///         <!-- Get information on the latest build -->
    ///         <MSBuild.ExtensionPack.Tfs.TeamBuild TaskAction="GetLatest" TeamFoundationServerUrl="$(TeamFoundationServerUrl)" TeamProject="SpeedCMMI" BuildDefinitionName="DemoBuild">
    ///             <Output ItemName="BuildInfo" TaskParameter="Info"/>
    ///         </MSBuild.ExtensionPack.Tfs.TeamBuild>
    ///         <Message Text="BuildAgentDirectory: %(BuildInfo.BuildAgentDirectory)"/>
    ///         <Message Text="BuildAgentBuildServerVersion: %(BuildInfo.BuildAgentBuildServerVersion)"/>
    ///         <Message Text="BuildAgentDescription: %(BuildInfo.BuildAgentDescription)"/>
    ///         <Message Text="BuildAgentFullPath: %(BuildInfo.BuildAgentFullPath)"/>
    ///         <Message Text="BuildAgentMachineName: %(BuildInfo.BuildAgentMachineName)"/>
    ///         <Message Text="BuildAgentMaxProcesses: %(BuildInfo.BuildAgentMaxProcesses)"/>
    ///         <Message Text="BuildAgentName: %(BuildInfo.BuildAgentName)"/>
    ///         <Message Text="BuildAgentPort: %(BuildInfo.BuildAgentPort)"/>
    ///         <Message Text="BuildAgentUri: %(BuildInfo.BuildAgentUri)"/>
    ///         <Message Text="BuildDefinitionUri: %(BuildInfo.BuildDefinitionUri)"/>
    ///         <Message Text="BuildFinished: %(BuildInfo.BuildFinished)"/>
    ///         <Message Text="BuildNumber: %(BuildInfo.BuildNumber)"/>
    ///         <Message Text="BuildUri: %(BuildInfo.BuildUri)"/>
    ///         <Message Text="CompilationStatus: %(BuildInfo.CompilationStatus)"/>
    ///         <Message Text="CompilationSuccess: %(BuildInfo.CompilationSuccess)"/>
    ///         <Message Text="ConfigurationFolderPath: %(BuildInfo.ConfigurationFolderPath)"/>
    ///         <Message Text="ConfigurationFolderUri: %(BuildInfo.ConfigurationFolderUri)"/>
    ///         <Message Text="DropLocation: %(BuildInfo.DropLocation)"/>
    ///         <Message Text="FinishTime: %(BuildInfo.FinishTime)"/>
    ///         <Message Text="KeepForever: %(BuildInfo.KeepForever)"/>
    ///         <Message Text="LabelName: %(BuildInfo.LabelName)"/>
    ///         <Message Text="LastChangedBy: %(BuildInfo.LastChangedBy)"/>
    ///         <Message Text="LastChangedOn: %(BuildInfo.LastChangedOn)"/>
    ///         <Message Text="LogLocation: %(BuildInfo.LogLocation)"/>
    ///         <Message Text="Quality: %(BuildInfo.Quality)"/>
    ///         <Message Text="Reason: %(BuildInfo.Reason)"/>
    ///         <Message Text="RequestedBy: %(BuildInfo.RequestedBy)"/>
    ///         <Message Text="RequestedFor: %(BuildInfo.RequestedFor)"/>
    ///         <Message Text="SourceGetVersion: %(BuildInfo.SourceGetVersion)"/>
    ///         <Message Text="StartTime: %(BuildInfo.StartTime)"/>
    ///         <Message Text="TestStatus: %(BuildInfo.TestStatus)"/>
    ///         <Message Text="TestSuccess: %(BuildInfo.TestSuccess)"/>
    ///         <!-- Queue a new build -->
    ///         <MSBuild.ExtensionPack.Tfs.TeamBuild TaskAction="QueueBuild" TeamFoundationServerUrl="$(TeamFoundationServerUrl)" TeamProject="SpeedCMMI" BuildDefinitionName="DemoBuild"/>
    ///     </Target>
    /// </Project>
    /// ]]></code>    
    /// </example>
    [HelpUrl("")]
    public class TeamBuild : BaseTask
    {
        private const string GetLatestTaskAction = "GetLatest";
        private const string QueueTaskAction = "Queue";
        private TeamFoundationServer tfs;
        private IBuildServer buildServer;
        private IBuildDetail buildDetails;
        private string buildDefinition;
        private string buildStatus;
        private string teamProject;

        /// <summary>
        /// Sets the TaskAction.
        /// </summary>
        [DropdownValue(GetLatestTaskAction)]
        [DropdownValue(QueueTaskAction)]
        public override string TaskAction
        {
            get { return base.TaskAction; }
            set { base.TaskAction = value; }
        }

        /// <summary>
        /// The Url of the Team Foundation Server.
        /// </summary>
        [TaskAction(GetLatestTaskAction, true)]
        [TaskAction(QueueTaskAction, true)]
        [Required]
        public string TeamFoundationServerUrl { get; set; }

        /// <summary>
        /// The name of the Team Project containing the build
        /// </summary>
        [TaskAction(GetLatestTaskAction, true)]
        [TaskAction(QueueTaskAction, true)]
        [Required]
        public string TeamProject
        {
            get { return this.buildDetails != null ? this.buildDetails.BuildDefinition.TeamProject : this.teamProject; }
            set { this.teamProject = value; }
        }

        /// <summary>
        /// The name of the build definition.
        /// </summary>
        [TaskAction(GetLatestTaskAction, false)]
        [TaskAction(QueueTaskAction, true)]
        public string BuildDefinitionName
        {
            get { return this.buildDetails != null ? this.buildDetails.BuildDefinition.Name : this.buildDefinition; }
            set { this.buildDefinition = value; }
        }

        /// <summary>
        /// Set the Status property of the build to filter the search. Supports: Failed, InProgress, NotStarted, PartiallySucceeded, Stopped, Succeeded
        /// </summary>
        [TaskAction(GetLatestTaskAction, false)]
        public string Status
        {
            get { return this.buildDetails != null ? this.buildDetails.Status.ToString() : this.buildStatus; }
            set { this.buildStatus = value; }
        }

        /// <summary>
        /// Gets the Build information
        /// </summary>
        [Output]
        public ITaskItem Info { get; set; }

        /// <summary>
        /// Performs the action of this task.
        /// </summary>
        protected override void InternalExecute()
        {
            using (this.tfs = new TeamFoundationServer(this.TeamFoundationServerUrl))
            {
                this.buildServer = (IBuildServer) this.tfs.GetService(typeof(IBuildServer));

                switch (this.TaskAction)
                {
                    case GetLatestTaskAction:
                        this.GetLatestInfo();
                        break;
                    case QueueTaskAction:
                        this.QueueBuild();
                        break;
                    default:
                        this.Log.LogError(string.Format(CultureInfo.CurrentCulture, "Invalid TaskAction passed: {0}", this.TaskAction));
                        return;
                }
            }
        }

        private void QueueBuild()
        {
            if (string.IsNullOrEmpty(this.BuildDefinitionName))
            {
                Log.LogError("BuildDefinitionName is required to queue a build");
                return;
            }

            this.LogTaskMessage(string.Format(CultureInfo.CurrentCulture, "Queueing Build: {0}", this.BuildDefinitionName));
            IBuildDefinition definition = this.buildServer.GetBuildDefinition(this.TeamProject, this.BuildDefinitionName);
            IBuildRequest request = definition.CreateBuildRequest();
            this.buildServer.QueueBuild(request, QueueOptions.None);
        }

        private void GetLatestInfo()
        {
            this.LogTaskMessage(string.Format(CultureInfo.CurrentCulture, "Getting Latest Build Information for: {0}", this.BuildDefinitionName));
            IBuildDetailSpec buildDetailSpec = this.buildServer.CreateBuildDetailSpec(this.TeamProject);
            if (this.BuildDefinitionName != null)
            {
                buildDetailSpec.DefinitionSpec.Name = this.BuildDefinitionName;
            }
            
            // Only get latest
            buildDetailSpec.MaxBuildsPerDefinition = 1; 
            buildDetailSpec.QueryOrder = BuildQueryOrder.FinishTimeDescending; 
            if (!string.IsNullOrEmpty(this.Status))
            {
                this.LogTaskMessage(MessageImportance.Low, string.Format(CultureInfo.CurrentCulture, "Filtering on Status: {0}", this.Status));
                buildDetailSpec.Status = (BuildStatus)System.Enum.Parse(typeof(BuildStatus), this.buildStatus);
            }
            
            // do the search and extract the details from the singleton expected result
            IBuildQueryResult results = this.buildServer.QueryBuilds(buildDetailSpec);

            if (results.Failures.Length == 0 && results.Builds.Length == 1)
            {
                this.buildDetails = results.Builds[0];
                ITaskItem ibuildDef = new TaskItem(this.BuildDefinitionName);
                ibuildDef.SetMetadata("BuildAgentDirectory", this.buildDetails.BuildAgent.BuildDirectory ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentBuildServerVersion", this.buildDetails.BuildAgent.BuildServer.BuildServerVersion.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentDescription", this.buildDetails.BuildAgent.Description ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentFullPath", this.buildDetails.BuildAgent.FullPath ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentMachineName", this.buildDetails.BuildAgent.MachineName ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentMaxProcesses", this.buildDetails.BuildAgent.MaxProcesses.ToString(CultureInfo.CurrentUICulture) ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentName", this.buildDetails.BuildAgent.Name ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentPort", this.buildDetails.BuildAgent.Port.ToString(CultureInfo.CurrentUICulture) ?? string.Empty);
                ibuildDef.SetMetadata("BuildAgentUri", this.buildDetails.BuildAgentUri.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("BuildDefinitionUri", this.buildDetails.BuildDefinitionUri.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("BuildFinished", this.buildDetails.BuildFinished.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("BuildNumber", this.buildDetails.BuildNumber ?? string.Empty);
                ibuildDef.SetMetadata("BuildUri", this.buildDetails.Uri.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("CompilationStatus", this.buildDetails.CompilationStatus.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("CompilationSuccess", this.buildDetails.CompilationStatus == BuildPhaseStatus.Succeeded ? "true" : "false");
                ibuildDef.SetMetadata("ConfigurationFolderPath", this.buildDetails.ConfigurationFolderPath ?? string.Empty);
                ibuildDef.SetMetadata("ConfigurationFolderUri", this.buildDetails.ConfigurationFolderUri.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("DropLocation", this.buildDetails.DropLocation ?? string.Empty);
                ibuildDef.SetMetadata("FinishTime", this.buildDetails.FinishTime.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("KeepForever", this.buildDetails.KeepForever.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("LabelName", this.buildDetails.LabelName ?? string.Empty);
                ibuildDef.SetMetadata("LastChangedBy", this.buildDetails.LastChangedBy ?? string.Empty);
                ibuildDef.SetMetadata("LastChangedOn", this.buildDetails.LastChangedOn.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("LogLocation", this.buildDetails.LogLocation ?? string.Empty);
                ibuildDef.SetMetadata("Quality", this.buildDetails.Quality ?? string.Empty);
                ibuildDef.SetMetadata("Reason", this.buildDetails.Reason.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("RequestedBy", this.buildDetails.RequestedBy ?? string.Empty);
                ibuildDef.SetMetadata("RequestedFor", this.buildDetails.RequestedFor ?? string.Empty);
                ibuildDef.SetMetadata("SourceGetVersion", this.buildDetails.SourceGetVersion ?? string.Empty);
                ibuildDef.SetMetadata("StartTime", this.buildDetails.StartTime.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("Status", this.buildDetails.Status.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("TestStatus", this.buildDetails.TestStatus.ToString() ?? string.Empty);
                ibuildDef.SetMetadata("TestSuccess", this.buildDetails.TestStatus == BuildPhaseStatus.Succeeded ? "true" : "false");
                this.Info = ibuildDef;
            }
        }
    }
}