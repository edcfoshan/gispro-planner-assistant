# GisPro Addin Workbench 交接文档

本文档用于新接手工程师/Agent 快速上手当前仓库，重点覆盖：

- 前期规划基线
- 当前实现进度
- 已遇到问题与解决过程
- 运行与排障手册
- 下一步建议

## 1. 项目背景与目标

本项目是一个 ArcGIS Pro Add-in（目标桌面版本 3.5），当前聚焦“前期调研”模块，首版统一接入百度/高德开放平台，支持三类任务：

- 行政区范围核对
- POI 采集
- 地址编码/逆编码

统一原则：

- 数据源可选：百度 / 高德 / 都要
- 双源模式统一写入同一批输出成果，按 `provider_rank` 排序（百度优先）
- 所有输出坐标统一为 `WGS84`

## 2. 当前版本基线（2026-03-14）

- Add-in 版本：`1.0.0`
- 更新时间：`2026-03-14 11:46`
- 变更标签：`DockPane primary + Survey VM init fix + standalone fallback`
- 目标框架：`net8.0-windows7.0`
- ArcGIS Pro SDK 包：`Esri.ArcGISPro.Extensions30 3.5.0.57366`

说明：版本信息与代码位置已对齐：

- `GisProAddinWorkbench.AddIn/Config.daml`
- `GisProAddinWorkbench.AddIn/Buttons/ShowVersionInfoButton.cs`
- `GisProAddinWorkbench.AddIn/GisProAddinWorkbench.AddIn.csproj`

## 3. 前期规划基线（首版范围）

首版纳入：

- 高德：行政区查询、POI 搜索、地理编码/逆地理编码
- 百度：地点检索、地理编码/逆地理编码、行政区核对记录

首版不纳入：

- 导航/路径规划
- 实时路况
- 移动端定位能力
- 复杂跨源融合去重

## 4. 当前实现进度

### 4.1 已完成

- Ribbon 入口已可用：`Open Workbench`、`Survey Window`、`版本与声明`
- DockPane 主入口已恢复：`WorkbenchDockPaneViewModel.Show()` 激活并加载内容
- Standalone Survey Window 兜底入口可用（DockPane 异常时可绕行）
- 前期调研 UI（参数区、状态区、结果区、日志区）已接入 ViewModel
- Provider 抽象已落地：
  - `IResearchProvider`
  - `BaiduResearchProvider`
  - `AmapResearchProvider`
- 调度与结果排序已落地：
  - `ResearchCoordinator` 支持单源/双源并发
  - 双源结果按 `provider_rank asc`、`created_at asc` 聚合
- 输出写入已落地：
  - `ResearchOutputWriter` 支持结果表输出
  - 点结果转要素（`XYTableToPoint`）
  - 面结果（行政区边界）支持 GeoJSON 转要素（`JSONToFeatures`）
- 坐标统一转换已实现：
  - `GCJ-02 -> WGS84`
  - `BD-09 -> WGS84`
- 用户级配置与模板机制可用：
  - `research-settings.json` 自动生成模板
  - 支持 `BaiduAk`、`AmapKey`、超时、分页、默认输出库配置
- 诊断日志机制可用：
  - 按钮点击、打开流程、异常堆栈均写入本地日志

### 4.2 部分完成

- 行政区边界入库能力：
  - 高德：可解析边界字符串时可生成面要素
  - 百度：首版以核对记录为主，很多场景会落 `need_review`（需复核）

### 4.3 未开始或后续

- 自动融合去重策略（跨源同名/近邻合并）
- 服务端中转与统一鉴权
- 与其它业务模块（后期分析/质检/登录/更新）的深度联动

## 5. 关键接口与类型快照

### 5.1 服务层主干

- `IResearchProvider`
  - `SearchPoiAsync`
  - `QueryDistrictAsync`
  - `GeocodeAsync`
  - `ReverseGeocodeAsync`
- `ResearchCoordinator`
  - 根据 `ResearchProviderType` 分派 provider
  - 轮转选择可用 Key
  - 双源并发调用并统一聚合排序
- `ResearchOutputWriter`
  - 写出结果表、点要素、面要素
  - 处理“行政区无边界”告警

### 5.2 核心模型

- `ResearchQuery`
- `ResearchRecord`
- `ResearchTaskResult`
- `ResearchProviderType`（Baidu / Amap / Both）
- `ResearchTaskType`（District / Poi / Geocode）

## 6. 关键问题复盘（已踩坑）

### 问题 1：按钮灰色、命令不可用、提示“类型未找到”

- 症状：Ribbon 按钮不可点或点击无响应，Tooltip 显示找不到类型。
- 根因：ArcGIS Pro 缓存中的旧程序集与最新 `Config.daml`/类定义不一致。
- 处理：
  - 统一 `Config.daml` 的 `className` 指向完整限定名
  - 命令类改为 `public`
  - 重新打包注册 Add-in
  - 清理本机 Add-in/Assembly 缓存后重启 ArcGIS Pro
- 验证：`版本与声明`、`Open Workbench`、`Survey Window` 三个按钮恢复可点击。

### 问题 2：DockPane 白屏（右侧“GIS 数据作业台”无内容）

- 症状：DockPane 标题存在但内容区域空白。
- 根因：DockPane 内容创建链路与 ViewModel 初始化阶段曾不稳定。
- 处理：
  - DockPane 改为返回 `SurveyWorkbenchView` 并绑定共享 `SurveyWorkbenchViewModel`
  - 在 `Show/OnShow` 主动触发 `EnsureInitializedAsync`
  - 保留 Standalone 窗口作为旁路验证入口
- 验证：DockPane/Standalone 均可加载同一套交互 UI。

### 问题 3：打开工程卡住，出现 `TypeInitializationException`

- 症状：打开窗口时报 `WorkbenchWindow` 类型初始化异常，工程加载卡住。
- 根因：`SurveyWorkbenchViewModel` 构造阶段触发命令状态刷新时存在空引用（初始化顺序问题）。
- 处理：
  - 调整构造期命令初始化与属性赋值顺序
  - 在命令刷新函数增加空值防护
  - 保持异常可见并写入 `addin-diagnostics.log`
- 验证：日志后续出现 `DockPane opened`、`Standalone window opened` 正常记录。

### 问题 4：兜底窗口不稳定

- 症状：`Survey Window` 偶发无弹窗。
- 根因：UI 线程调度与异常反馈不足。
- 处理：
  - 通过 `Application.Current.Dispatcher.BeginInvoke` 打开
  - 异常统一弹窗并落日志
- 验证：窗口可重复打开并激活。

## 7. 运行与排障 Runbook

### 7.1 本地构建

```powershell
dotnet build .\GisProAddinWorkbench.sln -c Debug
```

### 7.2 打包并注册 Add-in

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\build-addin.ps1 -Register
```

### 7.3 常用启动入口（双击）

- 开发刷新并打开工程：`.\launchers\dev-refresh-and-open.bat`
- 修复并启动 ArcGIS Pro：`.\launchers\fix-and-start-arcgispro.bat`
- 仅维护仓库内 `launchers` 入口，不维护桌面独立 `.bat`
- 兼容入口（保留旧名称）：
  - `.\launchers\调试效果重新打开.bat`
  - `.\launchers\修复并启动ArcGISPro.bat`

### 7.4 脚本分层约定

- `scripts/dev`：开发流程脚本（构建、刷新、调试打开）
- `scripts/ops`：运维/修复脚本（环境修复、启动兜底）
- `launchers`：双击入口（仅做参数转发，不承载业务逻辑）

路径规范：

- 入口 `.bat` 与 `.ps1` 必须通过 `%~dp0` / `$PSScriptRoot` 推导相对路径。
- 禁止硬编码仓库绝对路径（例如 `C:\Users\...\addin_for-gdupi\...`）。
- 系统安装路径（例如 `C:\Program Files\ArcGIS\...`）可保留绝对路径。

### 7.5 关键路径

- 诊断日志：`%LOCALAPPDATA%\GuotuGiserToolbox\addin-diagnostics.log`
- 配置文件：`%APPDATA%\GuotuGiserToolbox\research-settings.json`

### 7.6 常见故障处理顺序

1. 先点 `版本与声明`，确认版本号与更新时间是否匹配当前发布包。
2. 重新执行打包注册命令，确保最新 `.esriAddinX` 已注册。
3. 清理 ArcGIS Pro 的 Add-in/Assembly 缓存（避免旧 DLL 残留）。
4. 完全重启 ArcGIS Pro 再验证按钮与窗口行为。
5. 打开诊断日志，按时间线定位错误堆栈。

## 8. 配置与安全约束

Key 管理策略：

- 仓库内不保存任何百度/高德 Key 明文。
- 仅在用户级配置文件中维护：
  - `BaiduAk`
  - `AmapKey`
- 其他配置项：
  - `DefaultOutputGdb`
  - `RequestTimeoutSeconds`
  - `DefaultPageSize`

`research-settings.json` 示例（字段结构）：

```json
{
  "BaiduAk": [],
  "AmapKey": [],
  "DefaultOutputGdb": "",
  "RequestTimeoutSeconds": 30,
  "DefaultPageSize": 20
}
```

## 9. 给下一位 Codex 的接手建议（减少弯路）

第一优先级：

- 先修复中文字符串编码异常（当前部分源码/文档存在乱码显示风险）。
- 清理版本展示文案不一致问题（例如旧 Build 文案残留）。
- 增加最小回归验证脚本或手工清单：
  - 按钮可用性
  - DockPane 加载
  - Standalone 窗口加载
  - 单源/双源查询
  - 输出写入（表/点/面）

第二优先级：

- 行政区边界能力增强（尤其百度侧）
- 双源去重融合策略（保持可配置且默认关闭）

## 10. 验收清单（交接完成标准）

- 新接手者仅阅读本 README，即可回答：
  - 当前做到哪一步
  - 代码主要在哪些类实现
  - 本地如何编译注册
  - 出问题先查哪里
  - 下一步优先做什么
- 文档中不出现任何完整 Key。
- 文档中版本、命令、路径、类型名与当前仓库一致。


