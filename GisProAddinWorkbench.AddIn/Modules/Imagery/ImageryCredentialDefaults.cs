namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

// 影像底图“基础密钥”入口：明文密钥一律经 ImageryCredentialVault 加密内置，
// 管理员密码解锁后生效；自备密钥可在 全局设置→配置设置→基础密钥 中填写。
internal static class ImageryCredentialDefaults
{
    internal static IReadOnlyList<string> TianDiTuKeys => ImageryCredentialVault.TianDiTuKeys;

    internal static IReadOnlyList<string> JilinApiKeys => ImageryCredentialVault.JilinKeys;
}
