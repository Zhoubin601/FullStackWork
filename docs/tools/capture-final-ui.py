"""Run only against the live Release window populated by the UI demo flow."""
import re,subprocess,time
from ui_driver import *
def windowshot(name):
    time.sleep(.5)
    tree=subprocess.check_output(['xwininfo','-root','-tree'],env=ENV).decode()
    match=re.search(r'(0x[0-9a-f]+) "阅记 · 阅读与书摘"',tree)
    if not match: raise RuntimeError('Application window not found')
    subprocess.run(['import','-window',match.group(1),'docs/evidence/'+name],env=ENV,check=True)
click(100,189);windowshot('final-overview.png')
click(100,240);windowshot('final-library.png')
click(1060,330);windowshot('final-detail.png')
click(290,345);windowshot('final-book-edit.png')
click(286,47);click(100,291);windowshot('final-quotes.png')
click(300,403);windowshot('final-quote-edit.png')
click(287,47);click(100,343);windowshot('final-plans.png')
click(320,423);windowshot('final-plan-edit.png')
click(287,47);click(100,395);windowshot('final-statistics.png')
click(100,447);windowshot('final-settings.png')
print('Actual Release screenshots captured; inspect images and binding log.')
