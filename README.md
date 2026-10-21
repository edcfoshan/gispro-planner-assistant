# GIS Pro 规划师助手

面向城乡规划师的 ArcGIS Pro 加载项工作台：把前期调研、影像底图、地图跳转、成果整理等高频动作收进同一个 Add-in，少切换、少重复。

## 功能一览

| 模块 | 能力 |
|---|---|
| 前期调研 | 行政区边界核对、高德/百度 POI 采集、地址地理编码/逆地理编码、坐标系转换（GCJ-02 / BD-09 → WGS84），双源结果统一排序输出 |
| 影像底图 | 天地图、吉林一号年度一张图、Esri 世界影像、WFS 服务接入，密钥分区管理与图层缓存 |
| 地图跳转 | 在 ArcGIS Pro 中选中坐标一键跳转百度/高德地图与街景 |
| 面转文本 | 要素面导出为可读文本 |
| 系统设置 | 工具路径、默认输出库、UI 密度与日志统一管理 |

## 赞助商

感谢 **jizhanai.top** 赞助支持！通过[邀请链接注册开通](https://jizhanai.top/register?ref=PBUXGDHTFUAS)，填写邀请码 **`PBUXGDHTFUAS`** 可减 **5 元**。

## 环境要求

- ArcGIS Pro 3.4 / 3.5
- .NET SDK 8.0 及以上（仅构建需要）

## 安装

从 [Releases](../../releases) 下载 `.esriAddinX` 双击安装即可；也可以自行构建（见下）。版本产物统一走 Release 分发，仓库内不保存构建产物。

## 密钥与配置（重要）

- **影像内置密钥（天地图 / 吉林一号）已加密**：仓库中只有 AES-GCM 密文，输入管理员密码（`全局设置 → 配置设置 → 解锁基础密钥`）解锁后即可使用内置影像底图。密码由项目维护者线下分发，请勿在 Issue / PR 中讨论。
- **自备密钥同样可用**：不解锁也能正常使用本插件——百度 AK / 高德 Key 在同一界面填写即可，或直接编辑自动生成的 `research-settings.json`。
- **维护者更换密钥或密码**：编辑本机 `keys.local.json`（已被 .gitignore 排除，严禁提交）后运行：

  ```bash
  dotnet run --project scripts/dev/KeyProtector -- keys.local.json
  ```

  `ImageryCredentialVault.cs` 中的加密常量会自动替换，重新提交即可。
- **诚实声明**：内置密钥为密文分发，防的是仓库扫描与随意搬运；对刻意逆向不设防，介意者请自备密钥。

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

<img src="./content/求关注-极思G-20261014.png" width="720" alt="关注极思G：公众号与 QQ 交流群" />

如果这些工具对你有帮助，欢迎赞赏支持。赞赏完全自愿，感谢你的支持。

<img src="./content/求赞赏-极思G-20261014.png" width="720" alt="支持极思G：微信赞赏与爱发电" />

## 文档

- [交接文档](docs/handover.md)
- [架构说明](docs/architecture.md)
- 更多见 [`docs/`](docs/)

## License

[MIT](LICENSE)
