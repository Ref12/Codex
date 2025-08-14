using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.Import;
using Codex.ObjectModel;

namespace Codex.Analysis.Files
{
    public class RepoFileAnalyzer
    {
        public static readonly RepoFileAnalyzer Default = new RepoFileAnalyzer();

        public static readonly RepoFileAnalyzer Null = new NullRepoFileAnalyzer();

        public virtual string[] SupportedExtensions => new string[0];

        public virtual bool LoadContent => true;

        public Task Analyze(RepoFile file)
        {
            if (!SdkFeatures.AmbientFileAnalysisFilter.Value.Invoke(file))
            {
                return Task.CompletedTask;
            }

            return Analyze(file.PrimaryProject.Repo.AnalysisServices, file);
        }

        public virtual void Initialize(Repo repo)
        {
        }

        public virtual void Finalize(Repo repo)
        {
        }

        public virtual SourceFileInfo AugmentSourceFileInfo(SourceFileInfo info)
        {
            return info;
        }

        protected virtual async Task Analyze(AnalysisServices services, RepoFile file)
        {
            try
            {
                ReportStartAnalyze(file);

                SourceFile sourceFile = file.InMemorySourceFileBuilder?.SourceFile ?? CreateSourceFile(services, file);

                BoundSourceFileBuilder binder = file.InMemorySourceFileBuilder ?? CreateBuilder(sourceFile, file, file.PrimaryProject.ProjectId);

                await AnnotateAndUpload(services, file, binder);
            }
            catch (Exception ex)
            {
                services.Logger.LogExceptionError($"Analyzing file: {file.FilePath}", ex);
            }
        }

        protected virtual SourceFile CreateSourceFile(AnalysisServices services, RepoFile file)
        {
            SourceEncodingInfo encodingInfo = default;
            int size = file.InMemoryContent?.Length ?? 0;
            var content = LoadContent
                ? file.InMemoryContent ?? services.ReadAllText(file.FilePath, out encodingInfo, out size)
                : null;

            var info = AugmentSourceFileInfo(new SourceFileInfo()
            {
                Language = "text",
                ProjectRelativePath = file.LogicalPath,
                RepoRelativePath = file.RepoRelativePath,
                EncodingInfo = encodingInfo,
                Size = size
            });

            return new SourceFile()
            {
                Content = content,
                Info = info,
            };
        }

        protected virtual void BeforeUpload(AnalysisServices services, RepoFile file, BoundSourceFile boundSourceFile)
        {
        }

        private async Task AnnotateAndUpload(
            AnalysisServices services,
            RepoFile file,
            BoundSourceFileBuilder binder)
        {
            await AnnotateFileAsync(services, file, binder);

            foreach (var processor in services.Processors)
            {
                processor.BeforeBuild(binder);
            }

            var boundSourceFile = binder.Build();

            BeforeUpload(services, file, boundSourceFile);

            await UploadSourceFile(services, file, boundSourceFile);
        }

        protected static void ReportStartAnalyze(RepoFile file)
        {
            int analyzeCount = Interlocked.Increment(ref file.PrimaryProject.Repo.AnalyzeCount);
            file.PrimaryProject.Repo.AnalysisServices.Logger.WriteLine($"Analyzing source: '{file.PrimaryProject.ProjectId}::{file.LogicalPath}' ({analyzeCount} of {file.PrimaryProject.Repo.FileCount})");
        }

        protected BoundSourceFileBuilder CreateBuilder(SourceFile sourceFile, RepoFile repoFile, string projectId)
        {
            var builder = CreateBuilderCore(sourceFile, projectId);
            //if (sourceFile.Info.RepoRelativePath != null && repoFile.PrimaryProject != repoFile.PrimaryProject.Repo.DefaultRepoProject)
            //{
            //    // Add a file definition for the repo relative path in the default repo project
            //    builder.AnnotateReferences(0, 0, BoundSourceFileBuilder.CreateFileReferenceSymbol(
            //        sourceFile.Info.RepoRelativePath,
            //        repoFile.PrimaryProject.Repo.DefaultRepoProject.ProjectId,
            //        isDefinition: true));
            //}

            return builder;
        }

        protected virtual BoundSourceFileBuilder CreateBuilderCore(SourceFile sourceFile, string projectId)
        {
            return new BoundSourceFileBuilder(sourceFile, projectId);
        }

        protected static async Task UploadSourceFile(AnalysisServices services, RepoFile file, BoundSourceFile boundSourceFile)
        {
            boundSourceFile.RepositoryName = file.PrimaryProject.Repo.Name;
            boundSourceFile.SourceFile.Info.RepositoryName = file.PrimaryProject.Repo.Name;

            await services.RepositoryStore.AddBoundFilesAsync(new[] { boundSourceFile });

            RepoFileUpload(file, boundSourceFile);
        }

        public static void RepoFileUpload(RepoFile file, BoundSourceFile boundSourceFile)
        {
            int uploadCount = Interlocked.Increment(ref file.PrimaryProject.Repo.UploadCount);
            file.PrimaryProject.Repo.AnalysisServices.Logger.WriteLine(
                $"Adding source (include: {boundSourceFile.IsRequired}): '{boundSourceFile.ProjectId}::{boundSourceFile.ProjectRelativePath}' ({uploadCount} of {file.PrimaryProject.Repo.FileCount}) [R:{boundSourceFile.ReferenceCount}/D:{boundSourceFile.DefinitionCount}/C:{boundSourceFile.Classifications.Count}]");
        }

        protected virtual Task AnnotateFileAsync(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            AnnotateFile(services, file, binder);
            return Task.FromResult(true);
        }

        protected virtual void AnnotateFile(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            AnnotateFile(binder);
        }

        protected virtual void AnnotateFile(BoundSourceFileBuilder binder)
        {
            var text = binder.SourceFile.Content;
            if (XmlAnalyzer.IsXml(text))
            {
                XmlAnalyzer.Analyze(binder);
            }
        }

        public class NullRepoFileAnalyzer : RepoFileAnalyzer
        {
            protected override Task Analyze(AnalysisServices services, RepoFile file)
            {
                return Task.CompletedTask;
            }
        }
    }
}
