using System.Security.Cryptography;
using System.Text;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

// 影像底图内置密钥保险库：天地图/吉林一号密钥以 AES-GCM 密文随程序分发，
// 仓库与反编译均不可见明文；使用者输入管理员密码解锁后方可使用。
// —— 仓库里只有密文，明文密钥与密码永不入库（keys.local.json 已被 .gitignore 排除）。
// —— 更换密钥或密码：编辑仓库根目录 keys.local.json 后运行
//      dotnet run --project scripts/dev/KeyProtector -- keys.local.json
//    本文件中的三个常量会被自动替换，重新提交即可。
internal static class ImageryCredentialVault
{
    // ---- 以下常量由 scripts/dev/KeyProtector 生成并维护，请勿手工改动格式 ----
    internal const string SaltBase64 = "VES1o910XZbc8C55/BYLEg==";
    private const int Pbkdf2Iterations = 100_000;
    internal const string ProtectedTianDiTuKeysBase64 = "mCSQVs4EyXWIEznIHyeU4gZPpb2AQRBhucLhdJtxBq1gTU0PuCMr52QCH27+5feUI1bx9AzdJeu7nN4/1aZN2oiOZl+g7wSah8M+4YlQN3nK639tjY1cCKTFX3NAWAEOj/RZqpUptqqtPB/+1UIe9Qavz7rIPOHB6G8BrMUFcDYiobZed3D9PBq02fVOpDD8yfQJ/6eRDfdPllS98htpZqeBjl0nuZdcJK4t3AwfZ+XnhhpjyygdUQpuakhPbi7N2n4jagjR6YKCQw0R";
    internal const string ProtectedJilinKeysBase64 = "lqF+B9rsQcRZjc/jEsTi9nkk4a0ahwzHqfKCZs2Kz6cXdqrISArALzjk0GofooTuFzMidJJhfSQjUPga33Qy/FmepLYlGvOmDB2fj/VhYOSBuU325oyvxs7Iw05ZcNGnuz8s2ox/aS9EATU2iY5p9vgXnsgorsMCiWNq6rLZQzH1ubI2bnf9SfFgXyT9TjoFKeVjU1pzJQcnL/Usr5t+6jx5Kmx5fErYKXp2YMLxYcoiXsgg8sDb5bDQeAq6WJw7tSIOuwuJti/PxxSJ";
    // ------------------------------------------------------------------------

    internal static bool IsUnlocked => TianDiTuKeys.Count > 0 || JilinKeys.Count > 0;

    internal static IReadOnlyList<string> TianDiTuKeys { get; private set; } = [];

    internal static IReadOnlyList<string> JilinKeys { get; private set; } = [];

    internal static bool TryUnlock(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(
                password.Trim(),
                Convert.FromBase64String(SaltBase64),
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                32);

            // AES-GCM 认证标签同时充当密码校验：解密失败即密码错误。
            var tianditu = Decrypt(key, ProtectedTianDiTuKeysBase64);
            var jilin = Decrypt(key, ProtectedJilinKeysBase64);
            if (tianditu.Count == 0 && jilin.Count == 0)
            {
                return false;
            }

            TianDiTuKeys = tianditu;
            JilinKeys = jilin;
            return true;
        }
        catch (CryptographicException)
        {
            Reset();
            return false;
        }
        catch (FormatException)
        {
            Reset();
            return false;
        }
    }

    internal static void Lock()
    {
        Reset();
    }

    private static void Reset()
    {
        TianDiTuKeys = [];
        JilinKeys = [];
    }

    private static IReadOnlyList<string> Decrypt(byte[] key, string cipherBase64)
    {
        var blob = Convert.FromBase64String(cipherBase64);
        const int nonceSize = 12;
        const int tagSize = 16;
        if (blob.Length <= nonceSize + tagSize)
        {
            return [];
        }

        var nonce = blob.AsSpan(0, nonceSize);
        var tag = blob.AsSpan(blob.Length - tagSize, tagSize);
        var ciphertext = blob.AsSpan(nonceSize, blob.Length - nonceSize - tagSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return System.Text.Json.JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(plaintext)) ?? [];
    }
}
