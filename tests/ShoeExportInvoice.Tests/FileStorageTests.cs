using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Tests;

public class FileStorageTests
{
    [Fact]
    public async Task LocalUpload_SurvivesNewStorageInstance_AndDoesNotOverwriteExistingKey()
    {
        var root = Path.Combine(Path.GetTempPath(), "shoedocx-storage-" + Guid.NewGuid());
        try
        {
            var first = new LocalFileStorage(root + Path.DirectorySeparatorChar);
            var bytes = new byte[] { 1, 2, 3 };
            var reference = await first.SaveAsync("customs/test.xlsx", bytes, "application/octet-stream");
            var afterRestart = new LocalFileStorage(root);
            Assert.Equal(bytes, await afterRestart.ReadAsync(reference));
            await Assert.ThrowsAsync<IOException>(() => afterRestart.SaveAsync(reference, new byte[] { 9 }, "application/octet-stream"));
            Assert.Equal(bytes, await afterRestart.ReadAsync(reference));
            await afterRestart.DeleteAsync(reference);
            Assert.Null(await first.ReadAsync(reference));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("customs/../../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("r2:../secret")]
    [InlineData("customs\\secret")]
    public async Task StorageKeys_CannotEscapeTheStorageRoot(string key)
    {
        var storage = new LocalFileStorage(Path.GetTempPath());
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync(key));
    }
}
