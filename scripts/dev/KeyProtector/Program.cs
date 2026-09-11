// 本地工具：把 keys.local.json 中的明文影像密钥加密后写回 ImageryCredentialVault.cs。
// 用法：dotnet run --project scripts/dev/KeyProtector -- keys.local.json
// 密钥文件格式：{"TianDiTuKeys": ["..."], "JilinKeys": ["..."]}
// 明文密钥与密码只存在于本机；keys.local.json 已被 .gitignore 排除，严禁提交。

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.WriteLine("用法: dotnet run --project scripts/dev/KeyProtector -- keys.local.json [password]");
    Console.WriteLine("  password 可选：自动化场景直接传入；缺省交互式输入（输入不回显）。");
    return 1;
}

var keysPath = args[0];
if (!File.Exists(keysPath))
{
    Console.WriteLine($"找不到密钥文件: {keysPath}");
    return 1;
}

var vaultPath = FindVaultFile();
if (vaultPath is null)
{
    Console.WriteLine("找不到 ImageryCredentialVault.cs，请在仓库根目录运行。");
    return 1;
}

string password;
if (args.Length >= 2)
{
    password = args[1];
}
else
{
    Console.Write("设置管理员解锁密码: ");
    password = ReadMasked();
    Console.WriteLine();
    if (password.Length < 8)
    {
        Console.WriteLine("密码至少 8 位。");
        return 1;
    }

    Console.Write("再输一遍确认: ");
    var confirmed = ReadMasked();
    Console.WriteLine();
    if (confirmed != password)
    {
        Console.WriteLine("两次输入不一致，未做任何修改。");
        return 1;
    }
}

JsonElement json;
try
{
    json = JsonDocument.Parse(File.ReadAllText(keysPath)).RootElement;
}
catch (JsonException ex)
{
    Console.WriteLine($"密钥文件不是合法 JSON: {ex.Message}");
    return 1;
}

var tianditu = json.GetProperty("TianDiTuKeys").EnumerateArray()
    .Select(v => v.GetString() ?? string.Empty)
    .Where(v => v.Length > 0)
    .ToArray();
var jilin = json.GetProperty("JilinKeys").EnumerateArray()
    .Select(v => v.GetString() ?? string.Empty)
    .Where(v => v.Length > 0)
    .ToArray();

var salt = RandomNumberGenerator.GetBytes(16);
var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

var content = File.ReadAllText(vaultPath);
content = Regex.Replace(content, @"SaltBase64 = ""[^""]*"";",
    $"SaltBase64 = \"{Convert.ToBase64String(salt)}\";");
content = Regex.Replace(content, @"ProtectedTianDiTuKeysBase64 = ""[^""]*"";",
    $"ProtectedTianDiTuKeysBase64 = \"{Encrypt(key, tianditu)}\";");
content = Regex.Replace(content, @"ProtectedJilinKeysBase64 = ""[^""]*"";",
    $"ProtectedJilinKeysBase64 = \"{Encrypt(key, jilin)}\";");
File.WriteAllText(vaultPath, content, new UTF8Encoding(false));

Console.WriteLine($"已加密 {tianditu.Length} 个天地图 key、{jilin.Length} 个吉林一号 key，并写回:");
Console.WriteLine($"  {vaultPath}");
Console.WriteLine("请把该文件改动提交入库；keys.local.json 留在本机，不要提交。");
return 0;

static string? FindVaultFile()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "GisProAddinWorkbench.AddIn", "Modules", "Imagery",
            "ImageryCredentialVault.cs");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static string Encrypt(byte[] key, string[] values)
{
    var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
    var nonce = RandomNumberGenerator.GetBytes(12);
    var ciphertext = new byte[plaintext.Length];
    var tag = new byte[16];
    using var aes = new AesGcm(key, 16);
    aes.Encrypt(nonce, plaintext, ciphertext, tag);
    return Convert.ToBase64String(nonce.Concat(ciphertext).Concat(tag).ToArray());
}

static string ReadMasked()
{
    var buffer = new StringBuilder();
    while (true)
    {
        var keyInfo = Console.ReadKey(intercept: true);
        if (keyInfo.Key is ConsoleKey.Enter)
        {
            return buffer.ToString();
        }

        if (keyInfo.Key is ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
            }

            continue;
        }

        if (!char.IsControl(keyInfo.KeyChar))
        {
            buffer.Append(keyInfo.KeyChar);
        }
    }
}
