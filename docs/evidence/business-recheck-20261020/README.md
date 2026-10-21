# 受控恢复竞态证据

对应最新代码 `0313917`，详见 `../../business-recheck-20261020.md`。

- `Program.cs` / `Probe.csproj`：实际VM/SQLite探针；只引用已构建Library，不修改应用。
- `final-run-1.json` 到 `final-run-5.json`：五次最终受控运行的标准输出。
- `final-results.json`：五次结果与进程外 sqlite3 落盘读取。
- `AssertExpected.py`：正确行为断言；当前版本返回1；`expected-behavior-assertion.log` / `.exitcode` 保留执行结果。
- `validation.json`：代码哈希、适配器范围和结论；`build.log` / `restore.log` 为探针自身构建还原日志。
- `primary-confirmation.json`：主审在五次独立业务复现后，另执行一次相同探针的实际输出；同样发生覆盖。`primary-assertion.log` / `.exitcode`记录对该结果的正确行为断言失败，退出1。

非真实GUI点击复现。慢I/O门闩放在额外无修改读取的实际DB gate内；不暂停收藏Get返回，也不人工调用其Save。单线程同步上下文驱动实际命令。内存文件、提示与菜单服务为测试适配器。
