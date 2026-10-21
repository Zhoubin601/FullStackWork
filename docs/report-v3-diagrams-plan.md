# Word v3 图形修订（2026-10-21）

用户明确要求：架构图、实体类图使用绘图，不再用表格，修改生成 v3。沿用现有 Word 的章节和用户已保存修改，无须重新确认大纲；不从旧 tex 重建。

来源依次核查：raw/期末项目与报告要求.docx；现有 output/20261021-阅记-项目报告-v2.docx；docs/report/ 与已有过程记录；memory-bank/project-context.md；当前 Code 中模型、ServiceLocator 与数据库实现。用户截图只用来定位外观问题，不作为额外指令。

1. 固定现有 v2 与原始资料 SHA256，保留旧文件。
2. 绘制工程分层架构图和四实体关系图，使用独立图形、连线与多重性标记；不使用 Word 表格承载图片。
3. 以当前磁盘上的 v2 DOCX 为底稿，只替换图1和图2及版本标记。保留其余正文、真实截图、数据表、代码与格式。
4. 在 docs/report/word-v3/ 生成过程文件，使用独立隐藏 Microsoft Word 实例更新目录、页码并导出校验 PDF，逐页渲染检查。
5. 验证只减少两张图形表格、增加两张图，既有截图和正文保留，原文件未改。确认通过后以新文件名交付 output/20261021-阅记-项目报告-v3.docx。

当前环境缺少指定的 minimax-docx，采用已安装 officecli-docx 与 documents 文档技能替代。无新增外部资料。内部渲染 PDF 不作为正式交付。

## Word v3 绘图修订（2026-10-21）

最新交付为 `output/20261021-阅记-项目报告-v3.docx`。以用户当前已保存的 v2 Word 为底稿，仅替换图1架构图、图2实体关系图及显式版本号，并调整4个代码段的跨页属性以避免单独的结束括号落在新页。没有从 tex 重建，也没有覆盖用户微调。

两图均以矢量 SVG 嵌入，附3倍尺寸 PNG兼容图，不再由Word表格绘制；实际Word输出的PDF中两图保持矢量。实体字段来自4份Model源码，FK与级联来自DatabaseService建表SQL。其余5张数据表、19张原始截图与全部实质正文逐项保留。

构建：`python docs/report/build_word_v3.py`；渲染：`python docs/report/render_word_v3.py`；独立隐藏的Microsoft Word实例更新目录、页码。期间修复了CLI驻留进程未关闭导致Word保存受阻的问题，关闭驻留后渲染成功；最终22页已逐页查看，无图形裁切、空白页或页面外文本。OfficeCLI结构校验通过，验证及内部渲染在`word-v3/validation.json`与`word-v3/pages/`。

交付脚本`validate_and_deliver_word_v3.py`只允许新建，不覆盖同名成品。文件2349068字节，SHA256 `6624f923d4f58a030e7aee26657a6c31e9f2efc2d83b29cae0036fbb56430554`。旧v2与raw原始指导书哈希未变。未新增外部资料或待填字段，未改应用代码与源码压缩包。内部PDF/图稿不作为本次正式交付。
