using System.Buffers.Binary;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Security;

/// <summary>Passwords are stored as salted PBKDF2 hashes in ASP.NET Core Identity's v3 format (FR-004).</summary>
public sealed class PasswordStorageTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Hashes_are_identity_v3_pbkdf2_sha512_salted_and_never_the_password()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await AuthClient.Create(factory).RegisterAsync("one@example.com", "same password for both");
        await AuthClient.Create(factory).RegisterAsync("two@example.com", "same password for both");

        await using var scope = factory.CreateDbScope();
        var hashes = await scope.Db.Users.AsNoTracking().Select(u => u.PasswordHash!).ToListAsync();

        Assert.Equal(2, hashes.Count);
        Assert.NotEqual(hashes[0], hashes[1]); // per-user salt
        foreach (var hash in hashes)
        {
            Assert.DoesNotContain("same password", hash, StringComparison.Ordinal);
            var bytes = Convert.FromBase64String(hash);
            Assert.Equal(0x01, bytes[0]); // format marker: v3
            var prf = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4));
            var iterations = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));
            var saltLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9, 4));
            Assert.Equal(2u, prf); // KeyDerivationPrf.HMACSHA512
            Assert.True(iterations >= 100_000, $"iterations = {iterations}");
            Assert.True(saltLength >= 16, $"salt = {saltLength} bytes");
        }
    }
}
