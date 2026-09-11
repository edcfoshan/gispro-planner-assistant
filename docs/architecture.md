# 城乡规划作业插件架构说明（可扩展版）

## 1. 目标

插件采用“一个主插件内分模块”的模式，面向城乡规划从业者提供：

- 低门槛：默认简化输入，关键参数优先展示
- 可扩展：新模块可按统一契约接入，不改 Shell 核心代码
- 可学习：每个模块自带步骤引导、检查项和示例
- 可维护：模块配置独立存储，互不污染

## 2. 当前代码结构（已落地）

### 2.1 Shell 层

- `DockPanes/WorkbenchDockPaneViewModel.cs`
  - DockPane 主入口
  - 负责激活/展示统一壳层
- `DockPanes/WorkbenchDockPaneView.xaml`
  - 三段布局：左侧模块导航 / 中间工作区 / 右侧步骤引导
  - 顶部固定流程：版本与声明 -> 设置 -> 执行
- `DockPanes/WorkbenchShellViewModel.cs`
  - 模块注册加载、搜索、收藏、最近使用
  - 工作区切换、引导内容切换、壳层日志管理

### 2.2 Core 契约层

- `Core/Modules/IModuleDefinition.cs`
- `Core/Modules/IModuleWorkspace.cs`
- `Core/Modules/IModuleGuideProvider.cs`
- `Core/Modules/IModuleSettings.cs`
- `Core/Modules/IModuleSettingsStore.cs`
- `Core/Modules/ModuleContext.cs`
- `Core/Modules/ModuleDescriptor.cs`
- `Core/Modules/ModuleRegistry.cs`
- `Core/Modules/JsonModuleSettingsStore.cs`

核心思路：模块必须通过契约接入，禁止直接耦合其它模块实现。

### 2.3 首期模块（已接入）

- `Modules/Poi/*`
  - 复用现有 POI 查询能力（区划核对/POI/编码）
  - 作为标准样板模块
- `Modules/Imagery/*`
  - 影像模块骨架（简化输入 + 高级设置 + 模块配置）
- `Modules/Info/*`
  - 资讯模块骨架（筛选、置顶、配置持久化）

## 3. UI 设计约束（面向低门槛）

每个模块必须遵循：

1. 默认简化区：最多展示核心必填项
2. 高级设置区：用折叠面板承载复杂参数
3. 步骤引导区：至少 3 步（做什么、检查什么、示例是什么）
4. 状态反馈区：明确“待执行/执行中/成功/失败/需复核”

不允许：

- 在按钮事件里直接写完整业务流程
- 把所有参数一次性平铺到首屏
- 把模块配置写入全局单文件

## 4. 配置与内容策略

### 4.1 配置分层

- 全局配置：插件级通用配置（版本策略、默认输出库、网络超时）
- 模块配置：`%APPDATA%/GuotuGiserToolbox/modules/module.<id>.json`

### 4.2 教程内容

- 每个模块通过 `IModuleGuideProvider` 提供结构化步骤
- 后续支持按模块扩展为外置内容文件（便于本地化与运营维护）

## 5. 模块开发规范（未来编写必须遵守）

新增模块时，至少包含以下目录或职责文件：

- `Definition`（元信息：名称/分类/排序/能力）
- `Workspace`（模块工作区入口）
- `GuideProvider`（步骤引导）
- `Settings`（模块配置模型）
- `View + ViewModel`（简化输入/高级设置/状态）

开发流程建议：

1. 先定义 `IModuleDefinition` 和引导步骤
2. 再做简化视图（可执行闭环）
3. 最后补高级参数和配置持久化

## 6. 里程碑建议

### M1（已完成）

- Shell + 模块契约 + POI 模块接入
- 影像/资讯模块骨架接入

### M2

- 影像模块接 ArcGIS 图层加载 API
- 资讯模块接外部数据源或本地知识库

### M3

- 配色模块（图层样式与制图配色）
- 批次报批模块（模板、清单、状态追踪、导出）

### M4

- 模块健康检查、错误分级、兼容迁移、发布治理
