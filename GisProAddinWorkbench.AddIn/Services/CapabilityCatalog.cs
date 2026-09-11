using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class CapabilityCatalog
{
    internal static IReadOnlyList<CapabilityModule> CreateModules()
    {
        return
        [
            new CapabilityModule(
                "imagery",
                "影像加载",
                "基础能力",
                "统一挂接本地影像、服务影像、瓦片和项目内图层目录，减少人工拖拽。",
                "Add-in 负责图层入口、参数面板和作业日志，底层通过 ArcGIS Pro Map/Layer API 完成图层挂接。",
                "影像路径、服务地址、坐标系要求、项目模板。",
                "地图中的影像图层、加载日志、失败原因提示。",
                "第一版先做本地路径和 URL 两种入口，避免一开始把遥感预处理也塞进来。",
                "适合首批实现"),
            new CapabilityModule(
                "poi",
                "POI 采集",
                "外部协同",
                "把 POI 抓取、去重、坐标转换和落库纳入统一流程。",
                "推荐做成 Add-in + 外部采集适配器。Add-in 管理任务、行政区范围、结果预览和导出，采集端通过独立服务或脚本适配不同来源。",
                "关键字、行政区、分页策略、数据源凭证。",
                "POI 要素类、标准字段表、采集报告。",
                "采集来源合规性需要提前确认，SDK 适合做流程编排和质检，不建议把所有采集逻辑硬编码进按钮事件。",
                "需要服务配合"),
            new CapabilityModule(
                "survey",
                "前期调研",
                "流程能力",
                "沉淀数据目录、调研清单、字段标准和范围核对，形成项目级模板。",
                "DockPane 展示任务树和填报表单，规则可存在 JSON 配置或 geodatabase 配套表。",
                "行政区、专题清单、样点字段、数据来源说明。",
                "调研台账、缺失项提示、项目初始化清单。",
                "这里最适合先产品化，因为它对外部接口依赖最少，最容易快速见成果。",
                "建议和影像加载一起首发"),
            new CapabilityModule(
                "analysis",
                "后期分析",
                "计算能力",
                "把缓冲区、叠加、统计、时空分析等操作组合成固定作业模板。",
                "Add-in 调度 ArcGIS Pro Geoprocessing 或 Python 工具箱，界面只负责参数组织、进度和结果回显。",
                "输入要素、分析半径、统计字段、输出库。",
                "分析结果图层、指标表、导出图件或报告。",
                "复杂分析更适合下沉到 Python/GP 工具箱，避免后期维护大量 C# 空间分析细节。",
                "适合第二阶段实现"),
            new CapabilityModule(
                "qa",
                "质检",
                "规则能力",
                "统一做字段完整性、拓扑规则、值域检查和成果抽检。",
                "Add-in 提供规则集选择与结果看板，规则执行可以调用 Data Reviewer、GP 工具或自定义 Python 逻辑。",
                "待检数据、质检规则集、抽检比例。",
                "错误图层、问题表、修复建议。",
                "如果后续要做企业级质检，建议把规则定义外置，不要把检查项写死在代码里。",
                "建议作为第二批核心能力"),
            new CapabilityModule(
                "auth",
                "扫码登录",
                "系统能力",
                "实现账号鉴权、角色识别和令牌缓存，让 Add-in 能按权限开放功能。",
                "推荐 Add-in 内嵌 WebView2 登录页或二维码页，鉴权状态由服务端维护，客户端只保存短期令牌。",
                "登录二维码地址、回调协议、令牌刷新策略。",
                "登录态、用户信息、权限列表。",
                "这是明显的 Add-in + 服务端场景，单靠本地插件无法完成安全闭环。",
                "需要后端先定义协议"),
            new CapabilityModule(
                "update",
                "一键更新",
                "运维能力",
                "让插件可以检查版本、下载安装包，并提示用户重启 ArcGIS Pro 完成更新。",
                "推荐做成版本检查接口 + 下载新 esriAddinX 包 + 重启提示，不建议承诺运行中热替换。",
                "当前版本号、发布通道、下载地址、校验值。",
                "升级提示、下载进度、版本记录。",
                "ArcGIS Pro 启动阶段会管理 add-in 加载，因此更新链路要围绕包分发来设计。",
                "适合系统模块首批预埋")
        ];
    }

    internal static IReadOnlyList<DeliveryMilestone> CreateMilestones()
    {
        return
        [
            new DeliveryMilestone("第 1 步", "先把 DockPane、Ribbon、项目配置和日志底座跑通，确保后续所有模块都有统一入口。"),
            new DeliveryMilestone("第 2 步", "优先落地影像加载 + 前期调研，这两块依赖最少，最容易形成稳定作业闭环。"),
            new DeliveryMilestone("第 3 步", "把后期分析和质检下沉到 Python/GP 工具箱，Add-in 负责参数编排和结果展示。"),
            new DeliveryMilestone("第 4 步", "接入扫码登录和版本检查服务，完成企业化部署和权限控制。")
        ];
    }

    internal static IReadOnlyList<WorkbenchNote> CreateNotes()
    {
        return
        [
            new WorkbenchNote("功能分层", "把 UI、流程编排、计算规则、外部服务拆开，后续加功能不会互相缠死。"),
            new WorkbenchNote("版本策略", "代码现在按 ArcGIS Pro 3.4 SDK 建仓，版本定位以 3.4 环境为准，正式交付前应在 3.4.x 环境做一轮编译和回归。"),
            new WorkbenchNote("UI 沟通", "不用先给高保真，先把页签、主窗格、输入输出和流程状态说清，我就能继续落界面。")
        ];
    }

    internal static string BuildUiBriefTemplate()
    {
        return string.Join(
            Environment.NewLine,
            [
                "Tab: 数据作业台",
                "Group: 影像 / POI / 调研 / 分析 / 质检 / 系统",
                "主窗格: 左侧任务树, 中间参数区, 右侧结果表, 底部日志",
                "流程: 选范围 -> 抓取或加载 -> 清洗 -> 分析 -> 质检 -> 导出",
                "状态: 未开始 / 运行中 / 成功 / 失败 / 需复核",
                "特殊: 登录入口在右上角, 更新入口在系统组"
            ]);
    }
}
