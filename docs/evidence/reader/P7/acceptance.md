# P7 EPUB与图片验收（M2 / Linux）

依据reader-plan-v2/P7-01～06。上一阶段P6真实远端34f07e46c7ca56b159ea1e689604836fc1d1992a。实际实现/边界见docs/reader-p7-protocol.md。

| 用例 | 结果与可复核证据 |
|---|---|
| P7-01 | 11个支持/降级变体：basic、toc-order、duplicate-titles、nested-toc、image-styles、large-image、inline-style、unicode-path、epub2-ncx、long-chapter、many-images。实际spine/独立目录、同名章节唯一键、嵌套分组和非正文父目录保留子项；不同FileId导入canonical SHA稳定。见EpubImportTests及compatibility.json逐本字段。 |
| P7-02 | 正式原生布局完整解码/绘制、标题/粗斜体/中文/组合字符、图片独占页等比缩放；图片页文本片段/选择矩形为零，复制/书摘不含占位。EpubRenderingTests逐页、命中、百分比与最多4张缓存；真实ui-m2/07基本强调样式、08大图、12字号重排。 |
| P7-03 | 沿用同一Reader/会话/完成规则；原生测试重排/书摘/显式完成与保存，实际ui-m2/16重启恢复图片锚点偏移669/b-17，18在28字号下书摘回跳精确高亮。image-restart-state.json、ui-verification.json核对3次会话/原文/文件。 |
| P7-04 | 15类拒绝：缺图片、ZIP越界、远程图片/CSS、DTD/外部实体、脚本、固定布局、DRM、损坏XHTML、超像素、坏图片、编码父路径、重复条目、符号链接、20,000条目上限。全部零新增业务行，原件SHA不变，无越界文件。 |
| P7-05 | compatibility.json对11个支持样本逐本实际建立会话1275ms/定位书摘→完整ZIP→删除→恢复→同SHA/同inventory/原文精确回跳；包含源EPUB、canonical、图片。EpubRenderingTests另覆盖含图片/完成标记的完整往返，integrity=ok。 |
| P7-06 | 保留P6的243项，新增解析26项/原生3项，共272；最终工作区p7-accepted.trx与独立源码Debug p7-clean-source.trx及最终README同步后的Release p7-clean-source-release.trx。依赖/锁/许可证可核验，TXT及原手动回归通过。 |

真实桌面使用隔离书库，ui-m2/actions.json记录导入、原生拖选保存、翻页至2400×1800图片、字号22→28重排、正常关窗/重开/续读、书摘回跳。process.json两次正常关闭exit0、默认目录保持不存在；ui-verification.json确认源字节/完整受管清单/原句可定位/数据库integrity=ok。DBus/SESSION_MANAGER缺失与llvmpipe回退警告保留，未当作业务失败或隐藏。

首轮集成269项通过，后补ZIP重复/符号链接/条目限额三项共272。复核发现图片前文字结束游标不应携带图片亲和，已修复并在原生测试中断言，防止合法相邻选区被误拒绝；最终见p7-accepted。UI末次构建后仅同步导入标签/错误文本及该游标边界修正，核心实际图像/续读记录保留，不伪称重新生成截图。

样本声明：11本为确定性自建变体，15类为恶意/不支持输入；生成器连续生成两套SHA相同。真实商业/第三方书籍资料未提供，覆盖0本，未测市场兼容率；不能将11/11解释为任意EPUB都兼容。复杂CSS仅声明降级；固定版式/DRM、非UTF8 XML、SVG/公式/媒体不支持，GIF静态帧。实际图像样本为PNG，JPEG/GIF/WebP未逐类样本验收。Windows按用户要求保留未测，无真实内容API，raw/output/参考/冻结fixtures不改。

完整兼容输入、临时书库、往返ZIP和独立源码包仅在/workspace/.readjournal-env；最终源码白名单包摘要见source-package-final.json（此前Debug验证包见source-package.json），不作为新增正式交付。通过后按用户授权提交推送P7并回读；P8以后未授权。

最终收尾同步Code/README.md的当前P7能力与使用方式，将旧P4说明归为历史，并以186文件完整白名单重新封装/独立解包进行Release构建测试；正式说明不再把Reader/EPUB描述为未开放。

提交前仅清理许可证文件末尾多余空行，最终包的全部运行时代码与已通过272项Release测试的独立源码逐字节相同；该非执行文本整理不重复运行测试，源摘要另行更新。
