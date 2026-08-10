using System.Runtime.CompilerServices;
using PasswordManager.Services;

namespace BaiPass.Tests;

internal static class TestInit
{
    // Dial Argon2id down for the whole test assembly so the many derive calls stay fast.
    // Production defaults are exercised explicitly in the timing/parameter test.
    [ModuleInitializer]
    internal static void Init()
    {
        CryptoService.Argon2MemoryKiB = 1024; // 1 MiB
        CryptoService.Argon2Iterations = 1;
        CryptoService.Argon2Parallelism = 1;
    }
}
