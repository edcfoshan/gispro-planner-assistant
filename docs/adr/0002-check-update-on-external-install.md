# 检查更新与应用外安装

ArcGIS Pro Add-in 在 Pro 运行期间不适合由插件自行覆盖程序集。因此首次 Demo 的“检查更新”只查询 GitHub 最新 Release、比较版本并打开下载页；用户关闭相关窗口后双击新的 `.esriAddinX` 安装，再重启 ArcGIS Pro。后续只有在真实覆盖安装、失败回退和多版本兼容均验证通过后，才考虑自动更新。
