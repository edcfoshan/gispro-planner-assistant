# 快速安装与升级

## 环境

当前 Demo 目标是 ArcGIS Pro 3.4 / 3.5；本次发布验证环境为 ArcGIS Pro `3.5.2`。ArcGIS Pro 运行时请先保存工程，再安装新的 Add-in。

## 首次安装

1. 打开 [Releases](https://github.com/edcfoshan/gispro-planner-assistant/releases)。
2. 下载最新的 `JisigG-Planner-Workbench_0.1.0-demo.1_x64.esriAddinX`。
3. 核对 Release 中公布的 SHA256。
4. 关闭安装器提示的相关 ArcGIS Pro 窗口，双击 `.esriAddinX` 安装。
5. 重新启动 ArcGIS Pro，在功能区寻找「极思G 规划工作台」。

## 升级 Demo

1. 在功能区点击「检查更新」。
2. 有新版本时，应用会打开 GitHub Release 页面。
3. 下载新的 `.esriAddinX` 并核对 SHA256。
4. 双击安装新包，重启 ArcGIS Pro。

不会在 ArcGIS Pro 运行期间静默覆盖 Add-in。若新版本未能启动，请从 Release 重新安装上一版并提交脱敏日志。

## 影像服务凭据

内置天地图 / 吉林一号密钥需要管理员密码；Demo 暂不公开密码。也可以在设置中填写自己的补充密钥。禁止在 Issue、PR 或日志中公开凭据。
