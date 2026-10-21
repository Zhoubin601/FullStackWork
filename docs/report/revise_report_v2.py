"""Update the editable report source from verified local evidence, preserving v1."""
from pathlib import Path
import json

here=Path(__file__).resolve().parent
source=(here/'report.tex').read_text(encoding='utf-8')
package=json.loads((here/'source-package-v5.json').read_text(encoding='utf-8'))
source=source.replace('2026年10月20日','2026年10月21日（修订版 v2）')
source=source.replace('本文依据实际源码、自动化测试及Linux桌面实测编写，包含本轮缺陷修复与回归验证。测试环境和结果均如实记录；Windows桌面尚未实测，不记为已通过。','本文依据实际源码、Linux历史验证及2026年10月21日Windows本机验收编写。当前代码93项自动化测试、71项独立架构断言均通过；本报告配套源码v5，应用与测试源码同已验收的v4，仅README更新。')
source=source.replace('因此测试环境的GTK选择器部分文字为英文。','Linux历史测试中的GTK选择器部分文字为英文；Windows本机使用实际系统文件选择器。')
source=source.replace('真实桌面验证另在Xvfb上启动Avalonia应用，未将Headless断言冒充桌面操作。','历史Linux桌面验证在Xvfb中启动Avalonia应用；本次另在Windows真实桌面完成业务闭环。Headless断言与真实桌面操作分别记录。')
source=source.replace('Linux x64实测正常，不据此宣称支持Alpine。','Linux x64与Windows x64实测正常，不据此宣称支持Alpine。')
source=source.replace('在阅记新增的重复单例保护之后统一注入参数并压栈','在阅记新增的忙碌页面和重复单例保护之后统一注入参数并压栈')
source=source.replace('firstline=6,lastline=21]{../../Code/ReadJournal/Services/ContentNavigationService.cs}','firstline=6,lastline=23]{../../Code/ReadJournal/Services/ContentNavigationService.cs}')
source=source.replace('以下十图均为最终Release应用在Linux真实X11窗口中的直接截屏，显示的是用户主动载入的虚构演示资料。','以下十图保留2026年10月20日Linux真实X11窗口的功能展示，显示的是用户主动载入的虚构演示资料；它们是历史平台截图，不替代本次Windows验收。随后补充当前Windows窗口截图。')
insert=r'''
\clearpage
本次Windows本机界面另行新建虚构测试书：120页，阅读10～20页、25分钟，摘录第15页并制定当天到期计划。累计活动为11页、25分钟，当前进度20/120页，剩余100页；与此前Linux演示库的191页等数据属于不同测试集，不混用统计。
\screen{local-windows-20261021/10-detail-complete.png}{Windows本机：阅读记录、书摘与当天到期计划的关联详情}
\screen{local-windows-20261021/11-favorite.png}{Windows本机：书摘收藏后显示星号，正文与感想保持}
\clearpage
\screen{local-windows-20261021/13-statistics.png}{Windows本机：实际统计11页、25分钟、0本完成}
\screen{local-windows-20261021/25-final-overview.png}{Windows本机：删除后恢复并正常重启，业务数据与深色主题保留}
'''
source=source.replace(r'\subsection{难点和解决方案}',insert+'\n'+r'\clearpage'+'\n'+r'\subsection{难点和解决方案}')
source=source.replace('重复目标拦截、参数版本、草稿保护、串行写入和事务均为阅记新增业务正确性措施。','后续修复还覆盖旧删除结果影响新导航、忙碌Loaded请求丢失、恢复期间导航，以及收藏与恢复交错。删除使用固定目标及参数版本检查；加载等待前次操作；所有页面忙碌时禁止菜单、返回、关闭和直接内容跳转。收藏由存储在同一事务内仅更新收藏位及时间，避免旧实体整体回写覆盖恢复数据。上述回归已纳入当前93项测试。\n\n重复目标拦截、参数版本、草稿保护、串行写入和事务均为阅记新增业务正确性措施。')
start=source.index(r'\section{系统测试}')
end=source.index(r'\section{总结与展望}')
tests=r'''\section{系统测试}
\subsection{测试环境}
2026年10月21日Windows本机实测使用Windows x64（系统版本10.0.26300）、.NET SDK 8.0.424、运行时8.0.30及Avalonia 11.0.10。业务测试使用隔离临时SQLite数据库，架构测试使用真实应用资源和Microsoft DI容器。真实桌面通过Windows UI Automation操作控件，并结合鼠标键盘与进程窗口截图；未用Headless运行冒充Windows桌面。

保留历史平台证据：2026年10月20日在Debian 13 / Linux x64、Xvfb 21.1.16 / 1440×1000环境完成原68项测试与桌面流程；后续收藏竞态修复后Linux同样完成93项测试。历史数字仅对应当时版本，当前自动化测试总数为93，不把多个平台的同一测试相加。

Windows工作区和v4源码包独立解包后，均执行锁定还原、Release构建和完整测试，结果各为93通过、0失败、0跳过。依赖还原使用本机现有NuGet配置与缓存，未隔离全部缓存；这不证明首次还原能够离线完成。两次构建均0错误，存在3条固定SQLite依赖旧Alpine RID的NETSDK1206提示。
\begin{Verbatim}[fontsize=\small,breaklines=true]
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
\end{Verbatim}

正式配套源码为\path{20261021-阅记-源码-v5.zip}，共PACKAGE_FILES个白名单文件、PACKAGE_BYTES字节，低于10MB；无bin、obj、编译结果或运行数据库。SHA256为\path{PACKAGE_HASH}。本轮打包验证CRC正常、清单一致、每个文件与当前Code逐字节一致。

与实际独立测试的v4逐文件比较，v5仅README变化；其余118个文件（包括应用、测试和构建配置）完全一致。因此v5沿用相同代码的Windows93项验收证据，不声称修改README后又独立重跑了一遍v5测试。此前v4解包应用已使用另一个空数据目录实际启动，初始化版本1数据库并正常关闭；构建结果未回填ZIP。

另对当前Windows Release程序集运行独立架构探针：71项断言全部通过，覆盖14个VM单例、14个服务接口、13个反射视图、资源Current、真实容器、12个路由常量、参数、重复目标及返回栈。该71项与93项自动化测试分别统计，不将其写为164项业务单元测试。
\subsection{测试用例}
下表按TRX中的测试类归组统计，每条数据行对应实际执行用例；总计93项，全部通过。
\begin{longtable}{p{36mm}p{88mm}p{15mm}}\toprule 用例组与数量 & 主要实际覆盖 & 结果\\\midrule
架构9 & 真实容器、单例、构造注入、三层导航、反射缓存、失败重试、A/B切换及4个忙态直接路由场景。 & 通过\\
数据库3 & 初始化与持久化、外键事务回滚、初始化失败后重试。 & 通过\\
阅读业务12 & 闭区间页数、重读、当前进度、末页完成、非法输入、搜索分页及关联删除。 & 通过\\
编辑2 & 保存、放弃、取消离开及保存失败保留。 & 通过\\
扩展业务16 & 计划边界、统计、演示、无效备份、JSON往返、自动备份与恢复事务失败。 & 通过\\
导航时序2 & 延迟旧请求不覆盖新参数；离开确认不并发重复。 & 通过\\
编辑回归20 & 加载中草稿、首次加载门禁、失败重试、保存快照及新建ID回填。 & 通过\\
计划一致性8 & 重复关联、历史多个完成计划的状态变化、原子拒绝、安全编辑及备份往返。 & 通过\\
删除导航竞态6 & 固定删除目标、旧操作不误删新目标、不弹走新的页面或草稿。 & 通过\\
忙碌加载2 & 单例重新打开时等待旧操作并加载最新参数。 & 通过\\
设置导航4 & 数据操作期间菜单、返回与关闭保护，完成后恢复。 & 通过\\
概览架构3 & 最近记录查询由存储提供，VM职责及结果正确。 & 通过\\
恢复并发6 & 收藏/恢复次序、忙态菜单返回关闭、原子收藏与失败释放保护。 & 通过\\\bottomrule
\end{longtable}

对应原始证据为\path{docs/evidence/local-windows-20261021/windows-full.trx}和\path{windows-clean.trx}，工作区执行约4秒、独立解包约3秒；耗时仅为本机这次测量，不作为性能保证。运行时架构结果为\path{architecture/runtime-checks.json}。

Windows真实窗口另行完成：新库初始化；录入120页书籍；0起始页被拒绝；10～20页/25分钟保存；第15页书摘及计划录入；收藏切换；计划、统计和概览核对；原生JSON导出、恢复；确认删除书籍后四表各0；再次恢复后四表各1；切深色并正常关闭、重启。恢复后四表逐字段与恢复前快照一致，收藏保留，PRAGMA integrity_check为ok，foreign_key_check为空。删除后恢复产生的自动备份确为四表全空，证明备份记录了替换前状态。

Windows三个工作区启动日志和独立包启动日志均未输出绑定诊断。历史Linux曾验证演示数据、编辑重试和计划状态变化；本轮没有重新遍历所有历史UI边界，也未执行Alpine、大数据压力及所有CPU/系统组合。原生文件选择通过实际对话框完成，不宣称已实现全部步骤无人值守重放。
\subsection{测试截图}
以下均为本次Windows实际进程窗口截图。93项通过的数量依据上节两份原始TRX及test.log，不使用旧68项终端截图冒充本次结果。
\screen{local-windows-20261021/08-record-saved.png}{Windows真实阅读记录保存：10～20页按11页累计，进度20/120页}
\screen{local-windows-20261021/17-restored.png}{Windows真实备份恢复成功，并显示原数据自动备份路径}
\clearpage
\screen{local-windows-20261021/24-theme-applied.png}{Windows实际应用深色主题，偏好保存成功}
\screen{local-windows-20261021/18-restart-overview.png}{Windows正常关闭后重启：11页、25分钟及关联数据保留}
\clearpage
\screen{local-windows-20261021/26-clean-package.png}{v4独立解包后的实际首次启动，隔离空库初始化成功}

截图对应业务数据来自明确标注的虚构测试书，不是实际用户阅读资料。测试摘要及逐字段核对结果存于\path{validation.json}、\path{ui-final-data.json}及\path{ui-deletion-counts.json}。本报告未对原始截图补画或拼接控件。
\clearpage
'''
tests=tests.replace('PACKAGE_FILES',str(package['files'])).replace('PACKAGE_BYTES',str(package['bytes'])).replace('PACKAGE_HASH',package['sha256'])
limits='Windows三个工作区启动日志和独立包启动日志均未输出绑定诊断。历史Linux曾验证演示数据、编辑重试和计划状态变化；本轮没有重新遍历所有历史UI边界，也未执行Alpine、大数据压力及所有CPU/系统组合。原生文件选择通过实际对话框完成，不宣称已实现全部步骤无人值守重放。'
tests=tests.replace(limits+'\n','')
tests=tests.replace(r'\subsection{测试用例}', '\n'+limits+'\n'+r'\clearpage'+'\n'+r'\subsection{测试用例}')
tests=tests.replace(r'\subsection{测试截图}', '\n'+r'\clearpage'+'\n'+r'\subsection{测试截图}')
source=source[:start]+tests+source[end:]
source=source.replace('68项自动化测试及Linux实际桌面流程通过；源码包满足白名单和大小约束，解压后可还原、构建、测试及启动。','当前93项自动化测试、71项独立架构断言以及Windows本机真实桌面闭环通过；同时保留Linux历史验证。源码v5满足白名单、大小及逐文件一致性要求，应用与测试源码同已完成独立还原、构建、测试及启动的v4。')
source=source.replace('报告身份资料已补齐，当前交付包含修复后的源码与正式PDF。规划默认的Windows桌面尚未实际验收，不能以Linux结果推定Windows通过。','本修订版已同步Windows环境、测试结果、真实截图、异步竞态修复及源码包信息，配套源码v5；旧PDF及旧源码包保留作为历史版本。Windows通过结论来自本机实际验证，而非Linux推断。')
source=source.replace('初次构建存在固定SQLite依赖的Alpine RID提示','本机构建仍存在固定SQLite依赖的Alpine RID提示')
summary=r'''
\begin{longtable}{p{38mm}p{101mm}}\toprule 交付与验证项目 & 本版状态\\\midrule
规模与架构 & 66类、14接口、10业务页，八项机制对应；71项独立运行时架构断言通过。\\
应用与业务测试 & Windows工作区及v4独立解包均93项通过；真实业务闭环和重启持久化通过。\\
数据安全与恢复 & 删除后四表全空；恢复后四表各1且逐字段一致；自动备份、完整性及外键核对正常。\\
源码与报告配对 & 本报告v2配套源码v5，119文件、113557字节；相比v4仅README修订，旧版保留。\\
继续改进的边界 & 后续可增加更大数据集测量、更多系统/CPU组合及输入可用性验证；目前不宣称全部场景穷尽通过。\\\bottomrule
\end{longtable}
'''
source=source.replace('本报告依据原始指导书模板、唯一开发规划、实际参考副本和最终实现编写。',summary+'\n本报告依据原始指导书模板、唯一开发规划、实际参考副本和最终实现编写。')
(here/'report-v2.tex').write_text(source,encoding='utf-8')
assert 'Windows桌面尚未实测' not in source
assert '共114个文件' not in source and '102816字节' not in source
print('Updated editable source: report-v2.tex')
