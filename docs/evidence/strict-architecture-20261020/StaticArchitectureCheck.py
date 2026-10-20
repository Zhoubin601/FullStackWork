"""Read-only architecture inventory and VM query-boundary check.

Run from any directory with Python 3. Output is next to this script.
Token checks are conservative source checks, not a C# semantic analyzer.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[3]
out = Path(__file__).resolve().parent / "static-checks.json"
locator_path = root / "Code/ReadJournal/ServiceLocator.cs"
locator = locator_path.read_text()
registrations = re.findall(r"serviceCollection\.AddSingleton<([^>]+)>", locator)
view_models = []
boundary_hits = []
database_dependencies = []
boundary_patterns = {
    "database_callback": r"\.\s*(?:ReadAsync|WriteAsync)\s*\(",
    "database_query": r"\.\s*(?:Table|Query|ExecuteScalar)\s*(?:<[^>]+>)?\s*\(",
    "database_connection_operation": r"\b(?:c|db|connection|_database)\s*\.\s*(?:Execute|Insert|Update|Delete)\s*(?:<[^>]+>)?\s*\(",
    "direct_io": r"\b(?:File|Directory|HttpClient|SQLiteConnection|SQLiteAsyncConnection)\b",
}
for source in sorted((root / "Code/ReadJournal.Library/ViewModels").glob("*.cs")):
    text = source.read_text()
    relative = str(source.relative_to(root))
    for lineno, line in enumerate(text.splitlines(), 1):
        for rule, pattern in boundary_patterns.items():
            if re.search(pattern, line):
                boundary_hits.append({"source": relative, "line": lineno, "rule": rule, "text": line.strip()})
        if "IDatabaseService" in line:
            database_dependencies.append({"source": relative, "line": lineno, "text": line.strip()})
    for name, base in re.findall(r"public\s+(?:partial\s+)?class\s+(\w+ViewModel)\s*:\s*(\w+)", text):
        direct_host = name == "MainWindowViewModel"
        view_name = "MainWindow" if direct_host else name.replace("ViewModel", "View")
        view = root / "Code/ReadJournal/Views" / (view_name + ".axaml")
        view_models.append({
            "view_model": name,
            "base": base,
            "source": relative,
            "singleton_registered": name in registrations,
            "locator_property": bool(re.search(r"public " + name + " " + name + r"\s*=>\s*_serviceProvider.GetService<" + name + ">", locator)),
            "view": str(view.relative_to(root)),
            "view_exists": view.exists(),
            "x_class_matches": view.exists() and ET.parse(view).getroot().attrib.get("{http://schemas.microsoft.com/winfx/2006/xaml}Class") == "ReadJournal.Views." + view_name,
            "rendering": "App直接创建MainWindow并从资源绑定VM" if direct_host else "ViewLocator按类型命名约定映射",
        })

interfaces = []
for source in sorted((root / "Code/ReadJournal.Library/Services").glob("I*.cs")):
    for name in re.findall(r"public interface (\w+)", source.read_text()):
        interfaces.append({"interface": name, "singleton_mapping": [item for item in registrations if item.split(",")[0].strip() == name]})
routes = []
for layer in ("Root", "Menu", "Content"):
    interface_file = root / "Code/ReadJournal.Library/Services" / ("I" + layer + "NavigationService.cs")
    implementation_file = root / "Code/ReadJournal/Services" / (layer + "NavigationService.cs")
    constants = re.findall(r"public const string (\w+) = nameof\((\w+)\)", interface_file.read_text())
    mappings = dict(re.findall(layer + r"NavigationConstant\.(\w+)\s*=>\s*ServiceLocator.Current\.(\w+)", implementation_file.read_text()))
    routes.append({"layer": layer, "interface_file": str(interface_file.relative_to(root)), "implementation_file": str(implementation_file.relative_to(root)), "constants": [x[0] for x in constants], "mappings": mappings, "complete": set(x[0] for x in constants) == set(mappings)})

key_files = [
    "Code/ReadJournal.Library/ViewModels/OverviewViewModel.cs",
    "Code/ReadJournal.Library/ViewModels/ViewModelBase.cs",
    "Code/ReadJournal.Library/ViewModels/BookDetailViewModel.cs",
    "Code/ReadJournal.Library/ViewModels/SettingsViewModel.cs",
    "Code/ReadJournal.Library/Services/IReadingRecordStorage.cs",
    "Code/ReadJournal.Library/Services/ReadingRecordStorage.cs",
    "Code/ReadJournal/ServiceLocator.cs",
    "Code/ReadJournal/ViewLocator.cs",
]
result = {
    "date": "2026-10-20",
    "base_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
    "scope": "本轮修复后的当前工作区源码",
    "raw_sha256": hashlib.sha256((root / "raw/期末项目与报告要求.docx").read_bytes()).hexdigest(),
    "concrete_view_model_count": len(view_models),
    "singleton_registration_count": len(registrations),
    "interface_count": len(interfaces),
    "reflection_view_mapping_count": len(view_models) - 1,
    "view_models": view_models,
    "interfaces": interfaces,
    "routes": routes,
    "all_concrete_view_models_registered_and_mapped": all(x["singleton_registered"] and x["locator_property"] and x["view_exists"] and x["x_class_matches"] for x in view_models),
    "all_interfaces_singleton_registered": all(len(x["singleton_mapping"]) == 1 for x in interfaces),
    "all_route_constants_mapped": all(x["complete"] for x in routes),
    "vm_query_or_direct_io_hits": boundary_hits,
    "no_vm_query_or_direct_io_hits": not boundary_hits,
    "remaining_database_service_dependencies": database_dependencies,
    "database_dependencies_only_bootstrap": all(Path(x["source"]).name in ("MainWindowViewModel.cs", "InitializationViewModel.cs") for x in database_dependencies),
    "inspected_source_sha256": {name: hashlib.sha256((root / name).read_bytes()).hexdigest() for name in key_files},
}
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({key: value for key, value in result.items() if isinstance(value, (bool, int))}, ensure_ascii=False, indent=2))
assert all(result[key] for key in ("all_concrete_view_models_registered_and_mapped", "all_interfaces_singleton_registered", "all_route_constants_mapped", "no_vm_query_or_direct_io_hits", "database_dependencies_only_bootstrap"))
