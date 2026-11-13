# Changelog

[English](CHANGELOG.md) | **简体中文**

## 0.2.0 - 2026-11-14

- **针对显示设备丢失的崩溃加固**：新增 `GlassStabilityGuard`，在编译中、资源导入中、播放模式切换、从长时间空闲返回，以及域重载结束后的短时间窗内，暂停窗口改造与抓屏——这几处正是 D3D12 设备重建与标题栏接管撞车的时刻。
- 标题栏装机时机改为冷启动后约 0.15 秒、域重载后约 0.8 秒（原来统一 3 秒）；冻结幕布的判据补上「持久化样式仍属本插件」这一条，重载期间不再回退到原生标题栏。
- 抓屏单次超时或异常后自动退避 2–10 秒；`WDA_EXCLUDEFROMCAPTURE` 改为只清一次，不再逐帧设置。
- **许可变更**：由 MIT 改为 GNU GPL v3.0（`GPL-3.0-only`），`LICENSE` 已替换为 GPLv3 全文，`package.json` 与 README 同步更新。
- 移除源码及配置文件中的注释（C# / USS / 打包脚本 / SVG / `.gitignore`），代码行为不变。
- 为源码文件补版权与 SPDX 许可头（`SPDX-License-Identifier: GPL-3.0-only`）。
- 文档改为中英双语、以英文为主：`README.md` 为英文首页，中文见 `README.zh-CN.md`；`docs/macos-chrome.md` 为英文主文档，中文见 `docs/macos-chrome.zh-CN.md`；`CHANGELOG.md` 为英文，中文见 `CHANGELOG.zh-CN.md`；示意图英文标签在 `architecture.svg`，中文在 `architecture.zh-CN.svg`。
- 重新生成 `dist/` 下三种分发包，使包内源码与协议和仓库保持一致。

## 0.1.0 - 2026-11-13

首个公开版本。基线环境：**Unity 6000.3.8f1 / Windows 11**。

- **macOS 风格标题栏**：红黄绿圆点（关 / 最小化 / 最大化）、居中项目名、圆角窗口，圆点颜色跟随窗口激活状态。保留原生拖动、双击最大化、贴边分屏与四边八向缩放。
- **屏幕顶部菜单栏**：独立置顶分层窗口，完全自绘的下拉菜单，支持级联子菜单、勾选、分隔线与悬停高亮；菜单命令回传 Unity，菜单逻辑零重写。
- **外观配置**：菜单栏底色（自动 / 透明 / 浅色 / 深色 / 自定义）、二级菜单配色（跟随系统 / 浅色 / 深色 / 自定义）。
- **焦点白名单**：前台是代码编辑器时不抢前台，避免点菜单被踢出 IDE。
- **生命周期**：开关状态持久化在 `EditorPrefs`，域重载 / 重启编辑器后自动恢复。
- **急救与诊断**：`Restore Native Frame`、`Run Self Test`、窗口截图，以及 `Library/mactitlebar.log` 日志。
- **三种分发包**：`dist/unity-macos-chrome-0.1.0.unitypackage`（一键导入）、`.zip`（解压即用）、`com.liquidglass.macos-chrome-0.1.0.tgz`（UPM tarball），均由 `tools/make-unitypackage.py` 生成。
