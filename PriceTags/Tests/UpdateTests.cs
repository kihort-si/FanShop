using System.IO.Compression;
using FanShop.Services;
using FanShop.Updates;
using Xunit;

namespace FanShop.PriceTags.Tests;

public class UpdateTests
{
    [Fact] public void ValidPackageHasVerifiedHashesAndExtractsFlat()
    {
        using var f = new UpdateFixture(); var manifest = f.Package(f.Payload, "2.3.0", "new");
        var zip = f.Zip(f.Payload); var extracted = Path.Combine(f.Root, "extracted");
        var result = UpdatePackage.Extract(zip, extracted, "2.3.0", UpdateManifest.HashFile(zip));
        Assert.Equal(manifest.Version, result.Version); result.Verify(extracted); Assert.True(File.Exists(Path.Combine(extracted, "FanShop.exe")));
    }
    [Fact] public void WrongZipHashIsRejectedBeforeExtraction()
    { using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new"); var zip = f.Zip(f.Payload); Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, f.Install, "2.3.0", new string('0', 64))); Assert.False(Directory.Exists(f.Install)); }
    [Fact] public void FileHashMismatchAndMissingFilesAreRejected()
    {
        using var f = new UpdateFixture(); var manifest = f.Package(f.Payload, "2.3.0", "new");
        File.WriteAllText(Path.Combine(f.Payload, "FanShop.dll"), "corrupted"); Assert.Throws<InvalidDataException>(() => manifest.Verify(f.Payload));
        File.Delete(Path.Combine(f.Payload, "FanShop.dll")); Assert.Throws<InvalidDataException>(() => manifest.Verify(f.Payload));
    }
    [Fact] public void WrongManifestVersionIsRejected()
    { using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new"); var zip = f.Zip(f.Payload); Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, f.Install, "9.0.0", UpdateManifest.HashFile(zip))); }
    [Theory]
    [InlineData("../outside.dll")][InlineData("/outside.dll")][InlineData("C:/outside.dll")][InlineData("folder/../outside.dll")]
    [InlineData("folder\\outside.dll")][InlineData("NUL.dll")][InlineData("foo.dll:stream")][InlineData("file. ")][InlineData(".fanshop-update.lock")]
    public void UnsafePathsAreRejected(string path) => Assert.Throws<InvalidDataException>(() => PackagePaths.Normalize(path));
    [Fact] public void ZipTraversalIsRejectedWithoutWritingOutside()
    {
        using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new"); var zip = f.Zip(f.Payload);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open())) writer.Write("bad");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, Path.Combine(f.Root, "extract"), "2.3.0", UpdateManifest.HashFile(zip)));
        Assert.False(File.Exists(Path.Combine(f.Root, "escape.txt")));
    }
    [Fact] public void DuplicateWindowsPathsAndUnexpectedFilesAreRejected()
    {
        using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new"); var zip = f.Zip(f.Payload);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) using (var writer = new StreamWriter(archive.CreateEntry("fanshop.DLL").Open())) writer.Write("bad");
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, f.Install, "2.3.0", UpdateManifest.HashFile(zip)));
        File.WriteAllText(Path.Combine(f.Payload, "unexpected.txt"), "bad"); Assert.Throws<InvalidDataException>(() => UpdateManifest.Read(f.Payload).Verify(f.Payload));
    }
    [Fact] public void UserDataCannotBePackagedOrDeclaredForDeletion()
    {
        using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new");
        foreach (var name in new[] { "settings.json", "FanShop.db", "session.json", "болванка.docx" })
        {
            File.WriteAllText(Path.Combine(f.Payload, name), "user data");
            Assert.Throws<InvalidDataException>(() => UpdateManifest.Build(f.Payload, "2.3.0")); File.Delete(Path.Combine(f.Payload, name));
        }
    }
    [Fact] public void SuccessfulInstallRemovesOnlyOwnedObsoleteFilesAndPreservesUserData()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old", ("obsolete.dll", "old"), ("nested/obsolete.dll", "old"));
        File.WriteAllText(Path.Combine(f.Install, "notes.txt"), "personal"); File.WriteAllText(Path.Combine(f.Install, "settings.json"), "personal");
        var next = f.Package(f.Payload, "2.3.0", "new", ("new.dll", "new"));
        using var transaction = new InstallationTransaction(f.Install, f.Work);
        var removed = transaction.Apply(f.Payload, next); Assert.Equal(2, removed.Count);
        Assert.False(File.Exists(Path.Combine(f.Install, "obsolete.dll"))); Assert.False(File.Exists(Path.Combine(f.Install, "nested/obsolete.dll")));
        Assert.Equal("personal", File.ReadAllText(Path.Combine(f.Install, "notes.txt"))); Assert.Equal("personal", File.ReadAllText(Path.Combine(f.Install, "settings.json")));
        transaction.Commit(); Assert.False(File.Exists(Path.Combine(f.Install, InstallationTransaction.JournalName))); Assert.True(Directory.Exists(Path.Combine(f.Work, "backup")));
    }
    [Fact] public void ModifiedObsoleteComponentIsPreserved()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old", ("obsolete.dll", "owned"));
        File.WriteAllText(Path.Combine(f.Install, "obsolete.dll"), "user modified"); var next = f.Package(f.Payload, "2.3.0", "new");
        using var transaction = new InstallationTransaction(f.Install, f.Work); Assert.Empty(transaction.Apply(f.Payload, next)); transaction.Commit();
        Assert.Equal("user modified", File.ReadAllText(Path.Combine(f.Install, "obsolete.dll")));
    }
    [Fact] public void UnknownFileCollisionAbortsBeforeInstalledFilesChange()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old");
        File.WriteAllText(Path.Combine(f.Install, "new.dll"), "user file"); var next = f.Package(f.Payload, "2.3.0", "new", ("new.dll", "new"));
        using var transaction = new InstallationTransaction(f.Install, f.Work); Assert.Throws<IOException>(() => transaction.Apply(f.Payload, next));
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.Equal("user file", File.ReadAllText(Path.Combine(f.Install, "new.dll")));
    }
    [Fact] public void CopyFailureRollsBackEveryChangedFileAndManifest()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old", ("obsolete.dll", "old"));
        var oldManifest = File.ReadAllBytes(Path.Combine(f.Install, UpdateManifest.FileName)); var next = f.Package(f.Payload, "2.3.0", "new");
        using var transaction = new InstallationTransaction(f.Install, f.Work);
        Assert.Throws<IOException>(() => transaction.Apply(f.Payload, next, _ => throw new IOException("disk full")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.True(File.Exists(Path.Combine(f.Install, "obsolete.dll")));
        Assert.Equal(oldManifest, File.ReadAllBytes(Path.Combine(f.Install, UpdateManifest.FileName))); Assert.False(File.Exists(Path.Combine(f.Install, InstallationTransaction.JournalName)));
    }
    [Fact] public void UnconfirmedInstallCanRecoverAfterProcessInterruption()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); var next = f.Package(f.Payload, "2.3.0", "new", ("added.dll", "new"));
        using (var interrupted = new InstallationTransaction(f.Install, f.Work)) interrupted.Apply(f.Payload, next);
        Assert.True(File.Exists(Path.Combine(f.Install, InstallationTransaction.JournalName)));
        using (var recovery = new InstallationTransaction(f.Install, f.Work)) recovery.Rollback();
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.False(File.Exists(Path.Combine(f.Install, "added.dll")));
    }
    [Fact] public void CorruptBackupNeverSilentlyDestroysInstalledFiles()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); var next = f.Package(f.Payload, "2.3.0", "new");
        using (var interrupted = new InstallationTransaction(f.Install, f.Work)) interrupted.Apply(f.Payload, next);
        File.WriteAllText(Path.Combine(f.Work, "backup", "FanShop.dll"), "corrupt backup");
        using var recovery = new InstallationTransaction(f.Install, f.Work); Assert.Throws<IOException>(() => recovery.Rollback());
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.True(File.Exists(Path.Combine(f.Install, InstallationTransaction.JournalName)));
    }
    [Fact] public void ConcurrentInstallersAreBlocked()
    { using var f = new UpdateFixture(); using var first = new InstallationTransaction(f.Install, f.Work); Assert.Throws<IOException>(() => new InstallationTransaction(f.Install, Path.Combine(f.Root, "second"))); }
    [Fact] public void LegacyInventoryIsCarriedThroughSubsequentReleases()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.1.0", "old", ("obsolete.dll", "owned")); var oldZip = f.Zip(f.Install);
        f.Package(f.Payload, "2.2.0", "new"); var bridge = UpdateManifest.Build(f.Payload, "2.2.0", oldZip); bridge.Write(f.Payload);
        var bridgeZip = f.Zip(f.Payload); var next = UpdateManifest.Build(f.Payload, "2.3.0", bridgeZip);
        Assert.Contains(next.LegacyFiles, e => e.Path == "obsolete.dll");
        File.Copy(Path.Combine(f.Payload, "FanShop.dll"), Path.Combine(f.Install, "FanShop.dll"), true); bridge.Write(f.Install);
        File.WriteAllText(Path.Combine(f.Install, "personal.dll"), "unknown");
        Assert.Contains("obsolete.dll", LegacyPackageCleanup.Run(f.Install, Path.Combine(f.Root, "legacy-backup")));
        Assert.True(File.Exists(Path.Combine(f.Install, "personal.dll")));
    }
    [Fact] public void LegacyCleanupPreservesChangedFilesAndCanRetry()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.1.0", "old", ("obsolete.dll", "owned")); var zip = f.Zip(f.Install);
        f.Package(f.Payload, "2.2.0", "new"); var manifest = UpdateManifest.Build(f.Payload, "2.2.0", zip); manifest.Write(f.Install);
        File.WriteAllText(Path.Combine(f.Install, "obsolete.dll"), "changed"); Assert.Empty(LegacyPackageCleanup.Run(f.Install, Path.Combine(f.Root, "backup")));
        File.WriteAllText(Path.Combine(f.Install, "obsolete.dll"), "owned"); Assert.Single(LegacyPackageCleanup.Run(f.Install, Path.Combine(f.Root, "backup")));
    }
    [Fact] public async Task RunnerCommitsOnlyAfterHealthyStartup()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); f.Package(f.Payload, "2.3.0", "new");
        var host = new FakeUpdateHost { Healthy = true }; var request = f.Request(); Assert.True(await new UpdateRunner(host).RunAsync(request));
        Assert.True(host.Ready); Assert.True(host.Waited); Assert.Equal(0, host.Restarts); Assert.True(host.Cleared); Assert.Null(host.FailedVersion);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll")));
    }
    [Fact] public async Task FailedStartupRollsBackAndRestartsPreviousVersion()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); f.Package(f.Payload, "2.3.0", "new", ("added.dll", "new"));
        var host = new FakeUpdateHost(); Assert.False(await new UpdateRunner(host).RunAsync(f.Request()));
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.False(File.Exists(Path.Combine(f.Install, "added.dll")));
        Assert.Equal(1, host.Restarts); Assert.Equal("2.3.0", host.FailedVersion); Assert.True(host.Cleared);
    }
    [Fact] public async Task ParentTimeoutNeverKillsOrRestartsTheStillRunningApp()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); f.Package(f.Payload, "2.3.0", "new");
        var host = new FakeUpdateHost { ParentTimeout = true }; Assert.False(await new UpdateRunner(host).RunAsync(f.Request()));
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.Equal(0, host.Restarts);
    }
    [Fact] public async Task RecoveryDoesNotUndoACommittedInstallation()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); f.Package(f.Payload, "2.3.0", "new");
        var request = f.Request(); Assert.True(await new UpdateRunner(new FakeUpdateHost { Healthy = true }).RunAsync(request));
        Assert.True(await new UpdateRunner(new FakeUpdateHost()).RunAsync(request, true)); Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll")));
    }
    [Fact] public void ReleaseAssetSelectionRequiresExactVersionAndChecksum()
    {
        var release = new ReleaseInfo { Assets = [new() { Name = "other.zip" }, new() { Name = "FanShop2.3.0.zip" }] };
        Assert.Null(UpdateService.SelectPackage(release, "2.3.0"));
        release.Assets = [..release.Assets, new() { Name = "FanShop2.3.0.zip.sha256" }]; Assert.NotNull(UpdateService.SelectPackage(release, "2.3.0"));
        Assert.Null(UpdateService.SelectPackage(release, "2.4.0"));
    }
    [Fact] public void CacheRetainsTwoLatestBackupsAndNeverDeletesPendingOrUnknownDirectories()
    {
        using var f = new UpdateFixture(); var directories = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var path = Path.Combine(f.Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); directories.Add(path);
            var marker = Path.Combine(path, "completed.json"); AtomicFiles.WriteJson(marker, new { Success = true, Version = "2.3.0" });
            File.SetLastWriteTimeUtc(marker, new DateTime(2026, 1, i + 1));
        }
        var pending = Path.Combine(f.Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(pending);
        var unknown = Path.Combine(f.Root, "personal"); Directory.CreateDirectory(unknown);
        Assert.Equal(2, UpdateCache.Cleanup(f.Root)); Assert.All(directories.Take(2), p => Assert.False(Directory.Exists(p)));
        Assert.All(directories.Skip(2), p => Assert.True(Directory.Exists(p))); Assert.True(Directory.Exists(pending)); Assert.True(Directory.Exists(unknown));
    }
    [Fact] public void MultipleHistoricalArchivesIdentifyOlderObsoleteFiles()
    {
        using var f = new UpdateFixture(); var history = Path.Combine(f.Root, "history"); Directory.CreateDirectory(history);
        f.Package(f.Install, "1.4.0", "old", ("ancient.dll", "ancient")); File.Copy(f.Zip(f.Install), Path.Combine(history, "v1.zip"));
        var middle = Path.Combine(f.Root, "middle"); f.Package(middle, "2.1.0", "middle", ("recent.dll", "recent")); File.Copy(f.Zip(middle), Path.Combine(history, "v2.zip"));
        f.Package(f.Payload, "2.2.0", "new"); var next = UpdateManifest.Build(f.Payload, "2.2.0", history);
        Assert.Contains(next.LegacyFiles, e => e.Path == "ancient.dll"); Assert.Contains(next.LegacyFiles, e => e.Path == "recent.dll");
    }
    [Fact] public void SymbolicLinksCannotRedirectInstallationOrExtraction()
    {
        using var f = new UpdateFixture(); f.Package(f.Payload, "2.3.0", "new"); var zip = f.Zip(f.Payload);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("link"); entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var writer = new StreamWriter(entry.Open()); writer.Write("/outside");
        }
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(zip, f.Install, "2.3.0", UpdateManifest.HashFile(zip)));
        if (!OperatingSystem.IsWindows())
        {
            var outside = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(outside); Directory.CreateDirectory(f.Install);
            Directory.CreateSymbolicLink(Path.Combine(f.Install, "linked"), outside);
            Assert.Throws<IOException>(() => PackagePaths.Resolve(f.Install, "linked/file.dll"));
            Directory.Delete(Path.Combine(f.Install, "linked"));
        }
    }
    [Fact] public async Task RunnerRecoversAnInterruptedInstallationBeforeRestarting()
    {
        using var f = new UpdateFixture(); f.Package(f.Install, "2.2.0", "old"); var next = f.Package(f.Payload, "2.3.0", "new");
        using (var interrupted = new InstallationTransaction(f.Install, f.Work)) interrupted.Apply(f.Payload, next);
        var host = new FakeUpdateHost(); Assert.True(await new UpdateRunner(host).RunAsync(f.Request(), true));
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "FanShop.dll"))); Assert.Equal(1, host.Restarts); Assert.True(host.Cleared);
    }
}

internal sealed class FakeUpdateHost : IUpdateHost
{
    public bool Healthy { get; init; }
    public bool ParentTimeout { get; init; }
    public bool Ready { get; private set; }
    public bool Waited { get; private set; }
    public bool Cleared { get; private set; }
    public int Restarts { get; private set; }
    public string? FailedVersion { get; private set; }
    public Task WaitForParentAsync(UpdateRequest request, CancellationToken cancellationToken)
    { if (ParentTimeout) throw new TimeoutException(); Waited = true; return Task.CompletedTask; }
    public void EnsureNoOtherInstances(string installDirectory) { }
    public void RegisterRecovery(string requestPath, string payloadDirectory) { }
    public void SignalReady(string requestPath, UpdateRequest request) => Ready = true;
    public void ClearRecovery(string installDirectory) => Cleared = true;
    public Task<bool> StartAndVerifyAsync(UpdateRequest request, string requestPath, CancellationToken cancellationToken) => Task.FromResult(Healthy);
    public void RestartPrevious(UpdateRequest request) => Restarts++;
    public void RecordFailure(string version, string reason) => FailedVersion = version;
    public void Log(string message, Exception? exception = null) { }
}
internal sealed class UpdateFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "fanshop-update-test-" + Guid.NewGuid());
    public string Install => Path.Combine(Root, "installed app");
    public string Payload => Path.Combine(Work, "payload");
    public string Work => Path.Combine(Root, "work");
    public UpdateFixture() => Directory.CreateDirectory(Root);
    public UpdateManifest Package(string directory, string version, string content, params (string Path, string Content)[] extras)
    {
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "FanShop.exe", "FanShop.dll", "FanShop.runtimeconfig.json" }) File.WriteAllText(Path.Combine(directory, name), content);
        foreach (var (relative, text) in extras) { var path = PackagePaths.Resolve(directory, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
        var manifest = UpdateManifest.Build(directory, version); manifest.Write(directory); return manifest;
    }
    public string Zip(string directory)
    { var path = Path.Combine(Root, Guid.NewGuid() + ".zip"); ZipFile.CreateFromDirectory(directory, path); return path; }
    public string Request()
    {
        Directory.CreateDirectory(Work); var path = Path.Combine(Work, "request.json");
        AtomicFiles.WriteJson(path, new UpdateRequest(Install, Payload, "2.3.0", 1234, 1, ["--price-tags"], new string('A', 64))); return path;
    }
    public void Dispose() => Directory.Delete(Root, true);
}
