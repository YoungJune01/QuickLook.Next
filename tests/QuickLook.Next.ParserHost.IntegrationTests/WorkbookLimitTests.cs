using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipes;
using System.Security.Cryptography;
using QuickLook.Next.Contracts;
using QuickLook.Next.Core;
using Xunit;

namespace QuickLook.Next.ParserHost.IntegrationTests;

public sealed partial class ParserHostIntegrationTests
{
    [Theory]
    [InlineData("xl/worksheets/sheet1.xml", PreviewErrorCodes.ContentLimitExceeded)]
    [InlineData("xl/sharedStrings.xml", PreviewErrorCodes.ContentLimitExceeded)]
    [InlineData("malformed", PreviewErrorCodes.ContentFailed)]
    public async Task Workbook_handle_limit_errors_are_typed_and_release_the_source(
        string part, string expectedCode)
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "QuickLookNextParserHostTests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDirectory);
        string sourcePath = Path.Combine(tempDirectory, "physical-workbook.bin");
        string logicalPath = Path.Combine(tempDirectory, "missing-workbook.xlsx");
        if (part == "malformed")
            await File.WriteAllTextAsync(sourcePath, "invalid ZIP input");
        else
        {
            using var archive = ZipFile.Open(sourcePath, ZipArchiveMode.Create);
            WriteEntry(archive, part, new string(' ', 16 * 1024 * 1024 + 1));
        }

        string pipeName = $"quicklook_next_parser_test_{Environment.ProcessId}_{RandomNumberGenerator.GetHexString(16)}";
        string token = RandomNumberGenerator.GetHexString(32);
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using Process host = StartHost(pipeName, token);
        try
        {
            using var timeout = new CancellationTokenSource(Timeout);
            await pipe.WaitForConnectionAsync(timeout.Token);
            using var channel = new PipeChannel(pipe);
            await channel.SendAsync(new Hello(Environment.ProcessId, token), timeout.Token);
            Assert.IsType<ParserReady>(await channel.ReceiveAsync(timeout.Token));

            string requestId = Guid.NewGuid().ToString("n");
            var pinned = WindowsHandleTransfer.OpenPinnedReadOnlyFile(sourcePath);
            using (var sourceHandle = pinned.Handle)
            {
                long hostHandle = WindowsHandleTransfer.DuplicateFileToProcess(sourceHandle, host.SafeHandle);
                var probe = new FileProbe(logicalPath, ".xlsx", [0x50, 0x4B, 0x03, 0x04])
                {
                    Kind = "office",
                    Size = pinned.Length,
                };
                await channel.SendAsync(new PreviewOpenHandle(
                    requestId, hostHandle, pinned.Length, logicalPath, probe), timeout.Token);
            }

            PreviewError error = Assert.IsType<PreviewError>(await channel.ReceiveAsync(timeout.Token));
            Assert.Equal(requestId, error.RequestId);
            Assert.Equal(expectedCode, error.Code);
            Assert.Equal("xlsx", error.Format);
            Assert.Equal("native-handle", error.Phase);
            Assert.Equal(PreviewError.StableFailureMessage, error.Message);
            Assert.False(File.Exists(logicalPath));
            await WaitUntilAsync(() => TryOverwriteFile(sourcePath, "released"), timeout.Token);

            // An unsuccessful preview must not retain authority for image follow-ups.
            string imageRequestId = Guid.NewGuid().ToString("n");
            await channel.SendAsync(new OfficeImageOpen(
                imageRequestId, requestId, "xl/media/image1.png", 32, 32), timeout.Token);
            PreviewError imageError = Assert.IsType<PreviewError>(await channel.ReceiveAsync(timeout.Token));
            Assert.Equal(imageRequestId, imageError.RequestId);
            Assert.False(host.HasExited);
        }
        finally
        {
            try { pipe.Dispose(); } catch { }
            await StopHostAsync(host);
            try { Directory.Delete(tempDirectory, recursive: true); } catch { }
        }
    }
}
