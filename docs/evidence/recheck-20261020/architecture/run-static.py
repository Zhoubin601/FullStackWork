"""Re-run the existing read-only architecture checker into this recheck directory."""
from pathlib import Path

output_dir = Path(__file__).resolve().parent
root = output_dir.parents[3]
source = root / "docs/evidence/strict-architecture-20261020/StaticArchitectureCheck.py"
code = source.read_text()
code = code.replace('out = Path(__file__).resolve().parent / "static-checks.json"',
                    'out = Path(' + repr(str(output_dir / "static-checks.json")) + ')')
code = code.replace('本轮修复后的当前工作区源码', '0313917本轮只读复核的源码')
exec(compile(code, str(source), "exec"), {"__file__": str(source), "__name__": "__main__"})
