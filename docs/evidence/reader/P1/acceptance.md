# P1 原生分页实验验收（2026-10-22）

结论：按用户本轮授权的 Linux 验收口径通过 P1，可以进入 P2。P2 尚未开始；Windows 兼容性检查保留，尚未实测。

依据：output/reader-plan-v2 的总体规划、接口契约和逐阶段验收表。用户当前明确“你可以用linux，windows保留”，优先于原 P1-07 的 Windows 实机前置条件。该调整只改变当前阶段验证平台，不把 Linux 实测当作 Windows 支持证明，也不改写原规划历史文件。

功能分支 feature/reader-native；实现与全部原始探针证据提交 **bd6eb50db701c914c29b7adb847fc173faa98455**。P0 已推送到同名远端分支，真实远端 SHA 为 8465e495496702054474b1f03c45ddf96c90856a；本轮 P1 两次本地提交独立保留，未再推送，main 未修改。

## 范围与技术选择

实验在 docs/experiments/ReaderPrototype，未接入生产 Code、SQLite、Library 或 AI。README 说明运行方式与算法。采用同一 ReaderSurface/TextLayout/TextLine 进行绘制、命中和选区计算；默认只读 TextBox 模板实测因 padding/border 产生不同换行，因此不选该默认组合。这个实验不证明所有精调 TextBox 都不可用。

规范化 LF 文本及 SHA-256、UTF-16 字符簇边界与页范围分离。最多 4096 单元的段落优先块支持在任意锚点附近排版，几何布局最多缓存 8 块；页号未就绪显示计算中，后台索引每块让出 UI 调度，支持取消。LayoutId、打开版本和取消令牌隔离过期结果，页范围成功解析后形成冻结文本快照。

## 逐项结果

| 用例 | 结论与证据 |
|---|---|
| P1-01 | 通过。8 类 TXT 包括百万字和 14 万单元长段落，逐页拼接逐单元等于规范化全文；无缝范围、字符簇边界和缓存上限逐页断言。|
| P1-02 | 通过。中文/英文/缩进/标点/空行/超长段落/emoji/组合字符；原生行高度与页范围检查、Unicode 命中网格、实际字体截图复核。非法视口、空正文、异常编码和超长单字符簇明确拒绝。|
| P1-03 | 通过。真实 XTest 反向鼠标拖选、Shift+Right 跨页和 Ctrl+C；原文、应用剪贴板及独立 xclip 读取一致。截图与原始偏移见各 scale 目录。|
| P1-04 | 通过。200 次字号/行高/宽度重排保持 UTF-16 锚点；实际窗口 1160→1040 逻辑宽度后锚点 750000 保持；旧布局页引用拒绝。|
| P1-05 | 通过。百万字首屏只排首块；旧慢打开结果被忽略、旧页索引取消且不能覆盖新布局。|
| P1-06 | 通过（当前记录机器）。每档 200 次预热翻页及 200 次重排含栅格耗时均低于门槛，进程入口首屏低于 2 秒，原始数组保留；详见下表。|
| P1-07 | 按用户平台调整通过 Linux 检查。实际 RenderScaling=1/1.5/2，配套物理窗口、输入坐标与栅格尺寸同步缩放；三字体、缺失字体回退与 Numeric 输入框左右键焦点检查通过。Windows 及跨显示器检查保留未测。|
| P1-08 | 通过。未知全书页数显示计算中；已知范围返回准确快照，取消拒绝部分快照，索引期间 UI 心跳持续，页索引可终止。|

最终自动化测试：实验 **14/14**（prototype-linux-fontfix.trx），现有生产 **96/96**（production-baseline.trx）。最终原生桌面证据为 **linux-fontfix-scale-1、linux-fontfix-scale-1.5、linux-fontfix-scale-2**；各 native/probe.json 的 passedLocalProbe=true，process.json 退出码 0。历史 probe-final、linux-scale-* 和较早 TRX 只代表中间状态，不能替代最终结果。

## 性能、内存与测量边界

Debian Linux / AMD EPYC 9V74（当前逻辑 CPU 配额记录为 3）、.NET SDK 8.0.424 / runtime 8.0.30、Avalonia 11.0.10、Noto CJK / DejaVu。独立 Xvfb/Openbox 真实窗口；不是 Headless 性能结果。

| 实际缩放 | 进程首屏 ms | 再打开+排版+栅格 ms | 翻页 P95 ms | 重排 P95 ms | 索引最大心跳间隔 ms |
|---|---:|---:|---:|---:|---:|
| 100% | 870.23 | 44.82 | 1.28 | 4.21 | 20.31 |
| 150% | 919.09 | 64.06 | 2.35 | 4.62 | 22.59 |
| 200% | 912.68 | 43.24 | 3.49 | 5.55 | 33.11 |

首屏从托管进程入口至首份文档、首块布局和栅格绘制，未计 dotnet host 入口前时间，未清操作系统文件缓存。翻页/重排是同 UI 线程操作与 RenderTargetBitmap 绘制耗时，不是屏幕硬件延迟；同次进程已预热字体/相邻页。全书索引与首屏分开，不要求索引完成才显示首屏。

每档连续切书 20 次托管堆原始记录保留；最终约 44.7 MiB，低于实验本机 128 MiB 预算。retainedGrowthBytes 取第 20 次减第 6 次（预热后），不能解释为整个进程的总增长或无泄漏证明；不涵盖原生/私有工作集或 Windows 性能。

## 实测发现及修复

1. 裸 Xvfb 缺少 CLIPBOARD/TARGETS 等协议 atom，Avalonia 11.0.10 将不存在 atom 缓存为 0：早期探针复制失败、xclip 无内容。最终 helper 在私有测试显示启动应用前建立标准协议 atom，不设置 owner/内容；before/after 和失败日志全部保留。根因依据官方 X11Atoms/X11Clipboard 源码，非仅凭输入焦点推断。
2. 缺失字体的自动探针曾通过，但截图发现首行裁切（linux-scale-2/native/unicode-missing-font.png）。显式“所选字体→Noto Sans CJK SC→DejaVu Sans”回退修复；新增缺失字体基线与行长度回归、真实桌面基线检查，重新跑 14 项与三档桌面，并复核最终缺失字体截图。此历史截图是已修缺陷，不是当前通过证据。
3. 早期构建及原生输入时序失败日志保留；在窗口实际可见之后操作，使用真实鼠标/键盘输入，避免将测试脚本的焦点错误误判为产品缺陷。

## 重现与后续限制

通用构建/测试和 Windows 后续补验见 docs/experiments/ReaderPrototype/README.md。当前 Linux 桌面重现（准备样本、依赖和锁定构建后，输出目录必须新建）：

```sh
export LD_LIBRARY_PATH=/tmp/readjournal-p0-x11/root/usr/lib/x86_64-linux-gnu
export XDG_CONFIG_DIRS=/tmp/readjournal-p0-x11/root/etc/xdg:/etc/xdg
export XDG_DATA_DIRS=/tmp/readjournal-p0-x11/root/usr/share:/usr/share
python3 docs/tools/reader/run_p1_probe.py \
  --dotnet /tmp/readjournal-review-sdk/dotnet \
  --xvfb /tmp/readjournal-p0-x11/root/usr/bin/Xvfb \
  --wm /tmp/readjournal-p0-x11/root/usr/bin/openbox \
  --app docs/experiments/ReaderPrototype/bin/Release/net8.0/ReaderPrototype.dll \
  --samples /tmp/readjournal-p0-samples-a \
  --clipboard-reader /tmp/readjournal-p0-x11/root/usr/bin/xclip \
  --scale 2 --evidence /tmp/p1-new-evidence-scale-2
```

另以 --scale 1 和 1.5、各自新目录重现其他档。临时工具路径是本次环境，换机器需安装对应 .NET/X11/Openbox/xclip/ImageMagick/libXtst 与字体。样本由 P0 确定性生成器生成；哈希与 P0 清单对应，未用用户私有书籍。

本实验为 TXT 技术验证；章节/块尾可以留白，未实现 EPUB 图片样式、跨块填满页尾、正式数据库/会话/AI 集成。P2 将稳定实现迁入正式分层并补契约，不直接把实验入口当成正式功能发布。Windows 脚本 Test-P1Windows.ps1 保留但未运行，100/150/200% 实机字体、焦点及跨屏缩放须后续记录。

核对 git diff 8465e49 -- Code raw output 无差异；默认用户数据目录未创建，独立数据 override 未被实验使用，无数据库写入或 DeepSeek 调用。实现文件校验表 experiment-files-sha256.json、平台与摘要 environment-and-results.json。既有正式源码包/报告不重做、不覆盖。

官方外部参考（只用于诊断，引用日期 2026-10-22）：

- https://github.com/AvaloniaUI/Avalonia/blob/11.0.10/src/Avalonia.X11/X11Clipboard.cs
- https://github.com/AvaloniaUI/Avalonia/blob/11.0.10/src/Avalonia.X11/X11Atoms.cs
- https://github.com/AvaloniaUI/Avalonia/blob/11.0.10/src/Avalonia.X11/X11Platform.cs

准确 blob SHA 记录在 environment-and-results.json；未复制外部库实现进入项目。
