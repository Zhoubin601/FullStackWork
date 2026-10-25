## 最新课程报告 v2（2026-10-25，优先于全部历史记录）

用户要求根据新系统生成最新报告。本版以用户保存的 `output/v1/20261021-阅记-项目报告-v1.docx` 为编辑底稿，保留封面身份、原有样式和课程章节；重写已变化的需求、架构、schema2、原生阅读、AI、恢复与测试内容，没有从旧 tex 重建覆盖。

正式交付位于 `output/v2/`：`20261025-阅记-项目报告-v2.docx`、同名 PDF（29页）、`20261025-阅记-源码-v2.zip`。源码基线 `ba08e195e0c58072e9a1c13b2c28c02315f7847d` 与重新 fetch 后 origin/main 一致。新 ZIP 208 文件、344074 字节，白名单逐文件摘要一致。

报告含19项可点击目录、15张原生表格、11幅图（2张SVG矢量图和9张最新Windows实际窗口截图）、7段直接源码节选。全部29页渲染并查看，目录页码及链接、文本页界、原始资料和旧交付摘要验证通过。最新Windows证据为2026-10-24：353项中351通过、0失败、2权限跳过；209个留存源码摘要与当前Code完全一致。本轮没有重新运行应用测试或真实API，也没有声称新ZIP已独立构建。

过程、生成器与证据：`release-v2-20261025/`，入口 `build.py`，核验 `validate_delivery.py`，验证结果 `validation.json`。DOCX用python-docx编辑，PDF用LibreOffice导出；目录缓存根据实际PDF书签页面回填，原生Word目录字段保留，可在Word中更新。重新生成必须先在过程目录完成PDF导出及目录页码核对；交付脚本拒绝覆盖内容不同的既有成品。

后续应以用户最新保存的 output/v2 Word 为底稿，保留其微调，不直接重跑生成器覆盖用户编辑。原始资料与历史output原文件均保留；封面姓名、学号和班级请用户最终复核。

---

# 报告来源与生成

## 当前统一v1交付（2026-10-21，优先于下文）

正式文件在output/v1：项目报告v1.docx及PDF共23页，配套119文件114395字节源码v1.zip。以用户保存的v3 Word为底稿做定向更新，保留未改正文和两张SVG绘图；同步返回入口、94项测试、71项新探针与包元数据，替换独立启动图并新增返回截图。

过程与最终验证在docs/report/release-v1，独立源码测试在docs/evidence/release-v1-20261021。脚本prepare_release_v1.py生成定向OfficeCLI批处理，finish_release_v1.py增补图文，render_release_v1.py调用独立隐藏Word实例更新目录/导出PDF，publish_release_v1.py验证后发布。旧交付被归档至previous-deliveries，永久删除被自动审批拒绝；执行边界见docs/release-v1-20261021.md。

后续必须以output/v1中用户最新保存的Word继续编辑，不从旧tex或历史脚本重建覆盖。下文所有版本与路径均保留为历史。

当前正式报告为 `output/20261021-阅记-项目报告-v2.pdf`，配套源码 `output/20261021-阅记-源码-v5.zip`。

`report.tex` 和 `validation.json` 保留 v1 历史；本次没有覆盖它们。最新可编辑正文为 `report-v2.tex`，本机使用 ReportLab 排版，验证证据在 `revision-v2/validation.json`，最终逐页渲染在 `revision-v2/pages-verified/`。

在项目根目录运行：

```powershell
python docs/report/revise_report_v2.py
python docs/report/build_report_v2.py
pdftoppm -scale-to 1400 -png "docs/report/revision-v2/20261021-阅记-项目报告-v2.pdf" docs/report/revision-v2/pages-verified/page
```

依赖 Python 的 reportlab、Pillow、PyMuPDF、fontTools，以及 Windows 的宋体/黑体与 Poppler。`build_report_v2.py` 仅支持当前正文使用的有限 LaTeX 标记，遇到未知标记会报错，不作为通用 LaTeX 编译器。v2原正文可以继续编辑；自动修订脚本会从v1来源重建v2，手改后应直接运行构建脚本以保留手工内容。

人工逐页检查后再运行 `validate_and_deliver_v2.py`；脚本检查模板、身份、数字、字体、页界、原始资料哈希及新包与源码的对应，然后以不覆盖方式复制到 output。正式文件已存在时会拒绝再次交付同名版本，应按后续修改创建新版本。

来源：原始指导书 `raw/期末项目与报告要求.docx`；v1正文及真实Linux截图；`docs/local-audit-20261021.md`；`docs/evidence/local-windows-20261021/` 中两份93项TRX、71项架构探针、实际Windows截图和SQLite核对结果；v5包校验 `source-package-v5.json`。本次未新增外部研究资料。

## Word可编辑版

用户后续要求只交Word用于微调，已生成 `output/20261021-阅记-项目报告-v2.docx`。正文来源report-v2.tex，结构、表格和代码为原生Word内容；19张截图保留为独立图片，结构图为可编辑表格。

生成脚本build_word_v2.py，Word原生字段更新与渲染脚本render_word_native.py；过程文件和21页核验在word-v2目录。脚本操作的是过程report.docx，不覆盖正式Word。用户微调后应以微调后的DOCX为准，不从旧tex或脚本重建覆盖其修改。

## Word v3 绘图修订（2026-10-21）

最新交付为 `output/20261021-阅记-项目报告-v3.docx`。以用户当前已保存的 v2 Word 为底稿，仅替换图1架构图、图2实体关系图及显式版本号，并调整4个代码段的跨页属性以避免单独的结束括号落在新页。没有从 tex 重建，也没有覆盖用户微调。

两图均以矢量 SVG 嵌入，附3倍尺寸 PNG兼容图，不再由Word表格绘制；实际Word输出的PDF中两图保持矢量。实体字段来自4份Model源码，FK与级联来自DatabaseService建表SQL。其余5张数据表、19张原始截图与全部实质正文逐项保留。

构建：`python docs/report/build_word_v3.py`；渲染：`python docs/report/render_word_v3.py`；独立隐藏的Microsoft Word实例更新目录、页码。期间修复了CLI驻留进程未关闭导致Word保存受阻的问题，关闭驻留后渲染成功；最终22页已逐页查看，无图形裁切、空白页或页面外文本。OfficeCLI结构校验通过，验证及内部渲染在`word-v3/validation.json`与`word-v3/pages/`。

交付脚本`validate_and_deliver_word_v3.py`只允许新建，不覆盖同名成品。文件2349068字节，SHA256 `6624f923d4f58a030e7aee26657a6c31e9f2efc2d83b29cae0036fbb56430554`。旧v2与raw原始指导书哈希未变。未新增外部资料或待填字段，未改应用代码与源码压缩包。内部PDF/图稿不作为本次正式交付。
