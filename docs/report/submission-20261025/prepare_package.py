from pathlib import Path
import hashlib,json,subprocess,sys,zipfile

ROOT=Path(__file__).resolve().parents[3]; WORK=Path(__file__).resolve().parent
for name in ['README.md','CHANGELOG.md']:
    target=WORK/('original-'+name)
    if not target.exists():target.write_bytes((ROOT/'Code'/name).read_bytes())
path=ROOT/'Code/README.md'; text=path.read_text(encoding='utf-8')
paras=text.split('\n\n')
replacements={
'当前源码合并了':'应用提供阅读器折叠面板、统一 AI 范围、选区整段书摘、浅深主题和字体搜索预览。生产 AI 仅使用真实 DeepSeek 服务，无会话凭据时明确拒绝请求。配套项目报告说明需求、架构、实现、测试及已知边界。',
'合并后的全功能检查又修复了':'已修复缺失字体阻断排版及首屏视口重排导致会话未激活两处问题。2026-10-24 Windows 验收共353项：351通过、0失败、2项因符号链接权限跳过，另有5次真实AI调用及实际窗口完整备份恢复证据。本次提交前还对独立解包源码重新还原、构建和测试，结果见配套报告；两轮结果分别说明，不将历史窗口验收冒充当日重测。',
'字体与 AI 分支的合并前验收记录':'字体搜索、临时字体替代和真实 AI 的交互由自动化及 Windows 实际窗口验证；更改字体仍保留稳定正文位置。',
'P9最终独立源码334项':'Windows 实际窗口验收覆盖手动书籍、阅读记录、书摘收藏、阅读计划、混合统计、TXT/EPUB导入、分页、目录、原生选区、续读、完成与重置、AI原文核验和撤销，以及完整备份恢复。153项生产容器/视图/导航/性能检查、14项原生排版实验通过；恢复前后10张业务表内容及12个受管文件摘要一致。真实AI共5次成功请求，密钥完成后已删除。',
'`python pack.py':'`python pack.py <新ZIP路径>`按delivery-files.txt显式白名单打包，拒绝覆盖已有ZIP，核对全部文件内容与10,000,000字节上限。不要加入bin/obj、运行库/数据库、Key、大样本或验收证据。依赖及许可证见DEPENDENCIES.md和THIRD-PARTY-NOTICES.md。'
}
for i,para in enumerate(paras):
    for prefix,value in replacements.items():
        if para.startswith(prefix):paras[i]=value;break
path.write_text('\n\n'.join(paras),encoding='utf-8')
(ROOT/'Code/CHANGELOG.md').write_text('''# 实现说明

## 阅读管理与原生阅读

支持手动书籍、阅读记录、书摘收藏、计划和混合统计。TXT/EPUB 采用受管副本导入、收件区扫描及散列去重；原始来源文件保持不变。原生分页、章节目录、文字选择、字体搜索预览、全屏及可折叠面板共同服务阅读流程。

正文位置使用章节及稳定锚点，电子书不生成虚假纸质页数。首次实际呈现后激活阅读会话，正常离开幂等保存；缺失字体临时替代，首屏重排取消可继续呈现。图片位置可在重启后续读。

## AI 与数据一致性

生产 AI 仅调用真实 DeepSeek；解读不自动保存，整段选区原文由程序保留为一条书摘，其余候选逐字核验。支持去重、回跳、批次撤销及仅本地保存重试。凭据只保存在进程内存，不写源码、日志、配置和备份。

完整 ZIP 覆盖业务与受管资源；恢复前校验并自动备份，持久化操作日志与事务共同处理异常。保留旧数据迁移与旧 JSON 手动书恢复能力。真实容器、单例、统一注参、三层导航及主视图模型返回栈保持课程参考结构。

## 验证范围

2026-10-24 Windows Release 自动化353项：351通过、0失败、2项符号链接权限跳过；153项生产检查、14项原生排版实验、5次真实AI调用以及实际窗口完整备份恢复已留证。本次提交前另对独立解包源码重新还原、构建和测试，详细结果见配套报告。

已实测Windows 150%缩放，100%/200%与跨屏仍未验证；商业EPUB样本资料未提供，复杂CSS、固定版式、DRM及部分媒体内容存在限制。强制终止不补造未正常提交的阅读会话。
''',encoding='utf-8')
source=json.loads((ROOT/'docs/evidence/integrated-audit-20261024/source-hashes.json').read_text(encoding='utf-8-sig'))
mismatches=[r['path'] for r in source if hashlib.sha256((ROOT/r['path']).read_bytes()).hexdigest()!=r['sha256']]
assert set(mismatches)=={'Code/README.md','Code/CHANGELOG.md'},mismatches
archive=WORK/'阅记-源码.zip'
package=json.loads(subprocess.check_output([sys.executable,str(ROOT/'Code/pack.py'),str(archive)],encoding='utf-8'))
with zipfile.ZipFile(archive) as z:z.extractall(WORK/'source-unpacked')
(WORK/'package.json').write_text(json.dumps(package|{'changes_from_window_evidence':mismatches,'runtime_source_unchanged':True},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(package,ensure_ascii=False,indent=2))
