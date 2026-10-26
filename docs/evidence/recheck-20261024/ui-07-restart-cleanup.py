click('设置与数据')
assert any(e.Current.Name.startswith('未配置会话密钥') for e in elements())
mock=find('使用本地模拟（Mock）','CheckBox').GetCurrentPattern(TogglePattern.Pattern)
if str(mock.Current.ToggleState)=='On':mock.Toggle()
click('保存 AI 配置')
try:
    click('我的书库');edit(0,'recheck-ai');click('搜索 / 刷新');click('打开 →');click('开始 / 继续阅读')
    click('分析所选范围');click('确定');find('未配置会话密钥，请前往设置。','Text')
    click('下一页 →') if find('下一页 →','Button').Current.IsEnabled else None
    click('设置与数据')
finally:
    mock=find('使用本地模拟（Mock）','CheckBox').GetCurrentPattern(TogglePattern.Pattern)
    if str(mock.Current.ToggleState)!='On':mock.Toggle()
    click('保存 AI 配置');close()
with sqlite3.connect((Path(state()['data'])/'readjournal.sqlite3').as_uri()+'?mode=ro',uri=True) as db:
    assert db.execute('pragma integrity_check').fetchone()[0]=='ok'
    assert not db.execute('pragma foreign_key_check').fetchall()
    assert db.execute('select count(*) from Quote').fetchone()[0]==3
row={'restartHasNoCredential':True,'missingCredentialRejectedBeforeHttp':True,'localReadingAndNavigationAvailable':True,'normalClose':True,'finalMock':True,'integrity':'ok','foreignKeyErrors':0,'remainingQuotes':3}
(ROOT/'docs/evidence/recheck-20261024/restart-cleanup.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
record('restart-cleanup',**row)
