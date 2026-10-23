# P7 EPUB与图片契约补充

承接P6远端34f07e46c7ca56b159ea1e689604836fc1d1992a，依据reader-plan-v2/P7-01～06；不改变业务schema2/canonical v1或文档身份结构。

## 解析与资源边界

VersOne.Epub 3.3.6（Unlicense/netstandard2.0）通过EpubBookParser适配器使用OpenBookAsync/GetReadingOrderAsync/ReadContentAsync/GetNavigationAsync；AngleSharp1.8.5（MIT/net8.0）只在该适配器构建无网络加载器的DOM。实际API/许可证证据见P7/api-inspection.log、dependencies.json，MIT全文纳入Code/THIRD-PARTY-NOTICES.md，三个项目锁文件更新。

先检查ZIP的相对路径、重复/大小写冲突、符号链接、20,000条目、300MiB展开量、32MiB单条目，再安全读取XML；禁用DTD与外部实体，深度128、XML单文档16M字符。当前XML/XHTML必须可严格UTF8解码；其他编码明确拒绝。无执行脚本、网络资源、任意路径解压。固定版式、加密/DRM、脚本/嵌入媒体/SVG/公式、资源越界、缺失资源和损坏结构明确拒绝。

正文顺序以OPF线性spine为准并与供应商结果核对，不能按目录重排正文或静默少章。章节键取归档路径摘要，目录独立保留嵌套/HTML id锚点；非正文的目录分组保留全部子项并以首子项作为可读目标。规范化LF、段落、标题、粗斜体与基本内联强调；外部CSS不精确复刻，返回导入警告；CSS资源引用受检，@import不支持。总正文20M UTF16、单字素1024、块8192、块数100,000、章节2048；图片为单个U+FFFC块，受检PNG/JPEG/GIF/WebP资源仅使用安全摘要文件名，原件字节保持。

## 文档与原生布局

ReaderDocumentService对TXT/EPUB共用不可变canonical；ReadSlice仅返回相交块，样式坐标仍为章节坐标。Extract在图片处拆分纯文本片段，图片页返回零文本片段；复制去除占位，不生成伪原文。P6书摘首版仍拒绝跨图片的不连续范围。位置百分比按manifest排除图片占位的UTF16计量，不把图片当阅读字数。

OpenResource只接受manifest声明的键，验证摘要后返回只读流。原生图片独占页，保留图片块亲和锚点，等比缩到视口；解码前后25MP检查，按视口/DPI限制解码宽度。每布局最多4张解码图、8个文字排版块、全服务最多2个布局；释放布局/文档清理缓存。图片不参与文本选择矩形；图片前文本的结束游标不带图片亲和，避免合法相邻文本选区被误拒绝。样式同在Avalonia TextLayout中参与测量/分页/绘制/命中，按区间事件合并重叠字素样式。

Reader沿用P5首屏/会话/关闭重试、P6原文与回跳/完成重置；嵌套目录在同一侧栏展开显示，无第二个阅读器。导入按钮/文件选择器开放TXT/EPUB，编码选择仅用于TXT。

## 覆盖限制

兼容表逐本列出11个确定性自建支持/降级变体、15类拒绝输入，全部原件SHA保留，每个支持样本实际运行会话/书摘/完整备份→删除→恢复→精确回跳。真实书籍资料未提供，实际覆盖0本，不给出市场兼容百分比。复杂CSS、固定版式/DRM、非UTF8 XML、SVG/公式/媒体不在支持范围；GIF只显示静态帧，不复刻动画。实际图像样本为PNG，JPEG/GIF/WebP尚未逐类样本验收。Linux验证不替代保留的Windows字体/DPI验证，无真实内容API。
