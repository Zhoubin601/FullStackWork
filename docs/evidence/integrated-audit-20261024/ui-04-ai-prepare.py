assert next(r for r in rows('EbookProgress') if r['BookId']==2)['CompletedUtcTicks'] is not None
click('阅读统计'); shot('09-mixed-statistics')
click('我的书库'); click('导入 TXT / EPUB')
sample=ROOT/'docs/evidence/integrated-audit-20261024/samples/real-ai.txt'
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample)); click('打开(O)')
expect(lambda: len(rows('EbookFile'))==2,'真实AI合成短文导入')
click('设置与数据')
assert not [e for e in elements() if e.Current.Name=='使用本地模拟（Mock）']
consent=find('我了解 AI 会将确认范围正文发送至 DeepSeek','CheckBox').GetCurrentPattern(TogglePattern.Pattern)
if str(consent.Current.ToggleState)!='On': consent.Toggle()
click('保存 AI 配置')
snapshot('05-ai-ready-no-credential')
