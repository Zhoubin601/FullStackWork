# P1 原生分页实验

这是独立可运行的技术实验，位于 docs/experiments，不接入生产应用、Library、SQLite、受管书库或 AI。固定 .NET 8/Avalonia 11.0.10，依赖通过 packages.lock.json 锁定。现有 Code 和正式交付不因实验而改变。

## 构建、运行与测试

从仓库根目录执行（需要 .NET SDK 8.0.424/兼容补丁）：

```sh
python3 docs/tools/reader/generate_samples.py --output /tmp/p1-samples
dotnet restore docs/experiments/ReaderPrototype.Tests/ReaderPrototype.Tests.csproj --locked-mode
dotnet test docs/experiments/ReaderPrototype.Tests/ReaderPrototype.Tests.csproj -c Release --no-restore
dotnet run --project docs/experiments/ReaderPrototype/ReaderPrototype.csproj -c Release --no-build --no-restore -- --file /tmp/p1-samples/million.txt
```

Windows 将样本目录改为本机新路径。GB18030 使用 `--encoding gb18030`；`--anchor <UTF-16偏移>` 可验证按正文位置重新打开。同一规范化文档的哈希和偏移相同，不使用原布局页号恢复。

正文获得焦点后：左右键翻页，Shift+左右键扩展跨页选区，Ctrl+C 复制。支持反向拖选、拖至阅读区域外翻页、章节跳转、字号/字体/行高/窗口变化后恢复锚点。工具栏输入获得焦点时，正文控件不会收到其左右键。

## 已选实现与边界

- 统一 ReaderSurface/TextLayout：同一批 TextLine 同时用于绘制、命中、选择矩形与原文范围，没有再用另一份 TextBox 重排页面。
- 已实测默认只读 TextBox 模板的 padding/border 使 26 单元的原生行变为 22+4 单元；不将这份默认模板当作等价渲染器。本实验不声称所有精确调参后的 TextBox 都不能一致。
- 正文为不可变 LF 文本、SHA-256 与 UTF-16 半开区间，StringInfo 字符簇边界保证不切断 emoji、代理对或组合字符。空文档、异常编码、超长单字符簇、非法视口明确拒绝。
- 按章节顺序构造最多 4096 UTF-16 单元的段落优先块，长段落可继续分块；分页按实际 TextLine 高度累计。整本正文和字符边界数组目前在内存，几何布局最多缓存 8 块；页范围元数据可以独立保留。实验末块/章节末页可留白，没有实现跨块填满页尾。
- 任意锚点直接找到附近块并排版，不从书首重排；新 LayoutId 失效旧页引用。窗口/DPI/字体变化取消旧索引，旧打开任务或布局结果不会替换新上下文。
- 全书页索引在 UI 上分小段推进，每块后让出调度权，支持取消；索引未就绪不编造总页数。指定范围必须解析成功后才返回冻结文本快照，取消不返回部分快照。
- 未实现 EPUB 图片/样式、正式平台适配与数据库契约；P2 才把稳定契约/布局实现接入正式分层。位置不是阅读覆盖率，实验也不生成会话记录。

## Linux 真实桌面验证

使用 `docs/tools/reader/run_p1_probe.py` 在独立 Xvfb/Openbox 桌面运行真实应用；参数见脚本 `--help`。需要 xwininfo/xprop/ImageMagick import/libXtst，`--clipboard-reader /path/to/xclip` 可同时验证独立进程读回。

裸 Xvfb 没有剪贴板管理器，启动前 helper 仅创建标准 ICCCM 剪贴板协议 atom，不设置任何剪贴板 owner 或内容，避免 Avalonia 11.0.10 将不存在的 CLIPBOARD 缓存为 0。失败探针与修正后的 before/after 保留在 evidence，不删除或伪装通过。普通 Linux 桌面通常已建立这些协议标识；此 helper 的初始化只发生在独立测试显示中。

本轮在三个独立桌面用 `--scale 1`、`--scale 1.5`、`--scale 2` 运行，设置 Avalonia 实际渲染缩放并核对 `RenderScaling`；输入坐标、物理窗口和栅格像素同步缩放。Linux Xvfb 无法覆盖 Windows 多显示器切换。字体使用“所选字体 → Noto Sans CJK SC → DejaVu Sans”的明确回退顺序，避免不存在字体落到本机异常默认字体度量。

探针包含：8 类文本逐页覆盖、真实字体排版、首屏、200 次翻页/重排、真实反向鼠标拖选和跨页键盘复制、剪贴板内外读回、实际窗口大小变化、冻结范围、过期任务、可取消全书索引、主线程心跳和 20 次切书托管堆记录。

性能口径是同一 UI 线程的导航/布局及 RenderTargetBitmap 栅格绘制耗时，不能解释为硬件显示延迟。首屏分别记录进程入口至首屏，以及已有进程内读文件/正文生成/首块布局/栅格；未清空操作系统文件缓存，全书索引另行记录。内存记录包括托管堆和 8 块缓存预算，不替代进程私有内存、长期稳定性或目标 Windows 预算。

## Windows 后续兼容性补验

在实际 Windows 桌面依次设置 100%、150%、200% 显示缩放，每档重新打开终端/应用，执行（替换为真实新目录）：

```powershell
./docs/experiments/ReaderPrototype/Test-P1Windows.ps1 -DisplayScale 100 -SamplesDirectory C:/p1-samples -NewEvidenceDirectory C:/p1-evidence/scale-100
```

脚本记录 OS/CPU/字体、运行测试和真实窗口探针，并核对 Avalonia 实际 RenderScaling。不能只改命令行的 DisplayScale 来冒充系统 DPI；实际缩放不符会失败。此脚本在本轮 Linux 中未执行，属于待 Windows 验证工具。

每档还须正常打开 unicode.txt、mixed-lf.txt 和 million.txt，保存真实截图并记录：

1. 中文、标点、段首缩进、emoji、组合字符的可见字形/字体回退，无裁切、异常空行或缺字。
2. 正反向鼠标拖选、跨页选择，复制粘贴到独立文本编辑器与原文一致；输入框左右键不翻正文页。
3. 修改字体/字号/行高、拖动窗口、跨缩放显示器时保持同一正文锚点，旧布局不能盖回新页面。
4. 全书索引期间持续操作，取消/关闭可响应；实际性能和内存与本机目标对照。

逐项写入 `docs/evidence/reader/P1/windows-*/manual.md` 并附真实截图、脚本输出和缺陷编号。本轮用户明确“你可以用linux，windows保留”，因此当前 P1 按 Linux 验收，Windows 补验保留为后续兼容性项，不阻塞当前阶段。未运行上述 Windows 检查前，不宣称 Windows 字体、DPI 和输入兼容性通过。
