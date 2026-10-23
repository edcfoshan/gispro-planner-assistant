**QQ 交流群：826861025**

# 极思G 规划工作台

> **Demo · 社区预览版 `0.1.0-demo.1`**
>
> 当前版本用于验证工作台形态和已有模块，功能尚未完整，部分模块仍为骨架或实验功能，不应直接承担生产交付。

面向城乡规划从业者的 ArcGIS Pro 加载项工作台：把前期调研、影像底图、地图跳转、成果整理等高频动作收进同一个 Add-in，少切换、少重复。

> 本 Demo 仅承诺实测通过的 ArcGIS Pro 版本，其他版本列为待验证。

## 功能一览

以下是当前 Demo 已接入或可试用的能力，不代表全部功能已经达到稳定交付状态。

| 模块 | 能力 |
|---|---|
| 前期调研 | 行政区边界核对、高德/百度 POI 采集、地址地理编码/逆地理编码、坐标系转换（GCJ-02 / BD-09 → WGS84），双源结果统一排序输出 |
| 影像底图 | 天地图、吉林一号年度一张图、Esri 世界影像、WFS 服务接入，密钥分区管理与图层缓存 |
| 地图跳转 | 在 ArcGIS Pro 中选中坐标一键跳转百度/高德地图与街景 |
| 面转文本 | 要素面导出为可读文本 |
| 系统设置 | 工具路径、默认输出库、UI 密度与日志统一管理 |

## ❤️ 赞助商

<a href="https://jizhanai.top/register?ref=PBUXGDHTFUAS">
  <img src="./content/sponsors/jizhan-banner.svg" width="100%" alt="极栈：填写邀请码 PBUXGDHTFUAS 减 5 元，点击注册开通" />
</a>

<table>
  <tr>
    <td width="24%" align="center">
      <a href="https://jizhanai.top/register?ref=PBUXGDHTFUAS"><img src="./content/sponsors/jizhan-wordmark.svg" width="180" alt="极栈 jizhanai.top，点击通过邀请链接注册" /></a>
    </td>
    <td>感谢 <a href="https://jizhanai.top/register?ref=PBUXGDHTFUAS"><b>极栈 · jizhanai.top</b></a> 赞助支持本项目！极栈提供 <b>ChatGPT 会员充值</b>服务，支持<b>支付宝支付</b>。选购 <b>ChatGPT 激活码</b>后，按站内指引前往码港兑换。<br><br>通过<a href="https://jizhanai.top/register?ref=PBUXGDHTFUAS">邀请链接注册开通</a>，填写邀请码 <code>PBUXGDHTFUAS</code>，可减 <b>5 元</b>。</td>
  </tr>
</table>

## 环境要求

- ArcGIS Pro 3.4 / 3.5 目标范围；当前本机实测 `3.5.2`，首次 Release 只承诺实测通过的版本。
- .NET SDK 8.0 及以上（仅构建需要）

## 安装

从 [Releases](../../releases) 下载 `JisigG-Planner-Workbench_0.1.0-demo.1_x64.esriAddinX` 双击安装即可。Demo 更新采用“应用内检查版本 → 打开 Release 下载新包 → 双击安装”的方式，不静默覆盖正在运行的 ArcGIS Pro Add-in。

## 密钥与配置（重要）

- **影像内置密钥（天地图 / 吉林一号）已加密**：仓库中只有 AES-GCM 密文，输入管理员密码（`全局设置 → 配置设置 → 解锁基础密钥`）解锁后即可使用内置影像底图。Demo 暂不公开密码；没有密码时可填写自己的补充密钥。
- **自备密钥同样可用**：百度 AK / 高德 Key 在同一界面填写即可，或直接编辑自动生成的 `research-settings.json`。
- **请勿在 Issue / PR 中讨论或提交内置密钥、管理员密码、`keys.local.json` 或其他凭据。** 安全报告请遵循 [SECURITY.md](SECURITY.md)。

## 构建

```bash
git clone --recurse-submodules https://github.com/edcfoshan/gispro-planner-assistant.git
cd gispro-planner-assistant
dotnet build GisProAddinWorkbench.sln -c Release
```

> 前期调研模块依赖工具箱 [ArcGISpro-baidu-amap-geocode-pyt](https://github.com/edcfoshan/ArcGISpro-baidu-amap-geocode-pyt)（以 git submodule 引入）。
> 已经克隆过仓库？一条命令同步到工具箱最新版：
>
> ```bash
> git submodule update --remote
> ```

## 关注与赞赏

扫码关注「极思G工作室」公众号、加入 QQ 交流群，获取工具更新、交流使用经验：

<img src="./content/求关注-极思G-20261014.png" width="360" alt="关注极思G：公众号与 QQ 交流群" />

如果这些工具对你有帮助，欢迎赞赏支持。赞赏完全自愿，感谢你的支持。

<img src="./content/求赞赏-极思G-20261014.png" width="360" alt="支持极思G：微信赞赏与爱发电" />

## 文档

- [交接文档](docs/handover.md)
- [架构说明](docs/architecture.md)
- 更多见 [`docs/`](docs/)

## License

[MIT](LICENSE)
