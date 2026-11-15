using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using QuickLook.Next.Contracts;
using QuickLook.Next.Core;

namespace QuickLook.Next.App;

/// <summary>Headless deployment check; does not install, register startup, or create a UI.</summary>
internal static class PortableStorageSmoke
{
    public static int Run()
    {
        AppStorage storage = AppStorage.Current;
        if (!storage.IsPortable)
            return 33;
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            if (UiStrings.AppName == "⟦AppName⟧")
                throw new InvalidDataException("Portable WinUI resource loading failed.");
            if (!AppSettings.SaveTextLineNumbers(true))
                throw new IOException("Portable settings could not be saved.");
            FirstRunExperience.MarkShown();
            // Validate availability without enabling startup or touching any startup entry.
            if (FirstRunExperience.ShouldShow || !AutoStart.IsSupported)
                throw new InvalidOperationException("Portable state/startup policy is invalid.");
            storage.EnsureDirectory(storage.PdfCacheDirectory);
            DiagLog.Init("portable-smoke.log");
            DiagLog.Write("Portable", "storage check passed");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            CheckParserAsync(timeout.Token).GetAwaiter().GetResult();
            CheckRasterAsync(timeout.Token).GetAwaiter().GetResult();
            string resultPath = Path.Combine(storage.PortableRoot!, "Data", "portable-check.json");
            File.WriteAllText(resultPath, JsonSerializer.Serialize(new
            {
                storage.PortableRoot,
                storage.SettingsDirectory,
                storage.LogDirectory,
                storage.PdfCacheDirectory,
                TemporaryDirectory = Path.GetTempPath(),
                RuntimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(),
                ParserHandlePreview = true,
                RasterHandshake = true,
                UiResources = true,
                AutoStartAvailable = AutoStart.IsSupported,
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 34;
        }
    }

    private static async Task CheckParserAsync(CancellationToken cancellationToken)
    {
        string sourcePath = Path.Combine(AppStorage.Current.TemporaryDirectory, "portable-smoke.json");
        const string text = "{\"portable\":true}";
        await File.WriteAllTextAsync(sourcePath, text, cancellationToken);
        var supervisor = new ParserHostSupervisor(HostExecutablePath.Resolve(PreviewHostKind.Parser, AppContext.BaseDirectory));
        try
        {
            await supervisor.EnsureStartedAsync(cancellationToken);
            var pinned = WindowsHandleTransfer.OpenPinnedReadOnlyFile(sourcePath);
            using (pinned.Handle)
            {
                var probe = new FileProbe(sourcePath, ".json", []) { Kind = "text", Size = pinned.Length };
                var (_, completion) = supervisor.BeginOpenHandle(sourcePath, probe, pinned.Handle, pinned.Length, TimeSpan.FromSeconds(10));
                if (await completion.WaitAsync(cancellationToken) is not PreviewReady ready || ready.TextContent != text)
                    throw new InvalidDataException("Portable ParserHost returned unexpected content.");
            }
        }
        finally
        {
            supervisor.Stop();
            File.Delete(sourcePath);
        }
    }

    private static async Task CheckRasterAsync(CancellationToken cancellationToken)
    {
        string pipeName = $"quicklook_portable_{Environment.ProcessId}_{Guid.NewGuid():N}";
        string token = Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var job = new HostProcessJob((nint)(1024L * 1024 * 1024));
        using Process host = HostProcessLauncher.StartRestricted(
            HostExecutablePath.Resolve(PreviewHostKind.Raster, AppContext.BaseDirectory),
            ["--pipe", pipeName, "--session-token", token], job);
        try
        {
            await server.WaitForConnectionAsync(cancellationToken);
            WindowsHandleTransfer.VerifyNamedPipeClientProcess(server.SafePipeHandle, host.Id);
            using var channel = new PipeChannel(server);
            await channel.SendAsync(new Hello(Environment.ProcessId, token), cancellationToken);
            if (await channel.ReceiveAsync(cancellationToken) is not HostReady)
                throw new InvalidDataException("Portable RasterHost did not authenticate.");
        }
        finally
        {
            job.Dispose();
            if (!host.WaitForExit(5_000))
                host.Kill(entireProcessTree: true);
        }
    }
}
