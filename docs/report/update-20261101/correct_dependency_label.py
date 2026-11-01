from pathlib import Path
import zipfile
from lxml import etree

WORK=Path(__file__).resolve().parent
target=WORK/'20261101-阅记-项目报告-v1.docx'
temporary=WORK/'dependency-label-corrected.docx'
with zipfile.ZipFile(target) as source:
    tree=etree.fromstring(source.read('word/document.xml'))
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    nodes=tree.xpath('//w:t[text()="xUnit / Moq / Avalonia.Headless.XUnit"]',namespaces=ns)
    assert len(nodes)==1
    nodes[0].text='xUnit / Moq / Avalonia.Headless'
    data=etree.tostring(tree,xml_declaration=True,encoding='UTF-8',standalone=True)
    with zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED) as result:
        for member in source.infolist():
            result.writestr(member,data if member.filename=='word/document.xml' else source.read(member.filename))
temporary.replace(target)
print('Dependency label now matches ReadJournal.UnitTest.csproj: Avalonia.Headless 11.0.10.')
