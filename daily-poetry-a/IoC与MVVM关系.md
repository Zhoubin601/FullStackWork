# IoC 容器与 MVVM 的关系

> 面向 Avalonia UI + C#/.NET + MVVM 架构的场景说明。
> **所有代码示例均取自本项目 DailyPoetryA 真实代码，标注 `文件:行号` 出处。**
> 核心三问：
> 1. IoC 容器怎么知道你想要啥？
> 2. 谁去请求了 ViewModel？
> 3. IoC 和 MVVM 到底怎么关联的？

---

## 一、IoC 怎么知道你想要啥？

IoC 容器靠**两件事**工作：**注册表** + **构造函数反射**。

### 1.1 注册阶段（你告诉容器：接口 → 实现）

本项目的注册在 `DailyPoetryA/ServiceLocator.cs:66-97`：

```csharp
// ServiceLocator.cs:66
var serviceCollection = new ServiceCollection();

// —— ViewModel 注册（全部 Singleton）——
serviceCollection.AddSingleton<MainWindowViewModel>();    // :69
serviceCollection.AddSingleton<MainViewModel>();          // :70
serviceCollection.AddSingleton<TodayViewModel>();         // :71
// ...其余 ViewModel 同理（:68,:72-76）

// —— Service 接口 → 实现 注册 ——
serviceCollection
    .AddSingleton<IMenuNavigationService, MenuNavigationService>();   // :80-81
serviceCollection.AddSingleton<IPoetryStorage, PoetryStorage>();      // :85
serviceCollection
    .AddSingleton<ITodayPoetryService, JinrishiciService>();          // :91-92
serviceCollection.AddSingleton<IFavoriteStorage, FavoriteStorage>();  // :93
// ...其余 Service 同理（:78-79,:82-84,:86-90）

_serviceProvider = serviceCollection.BuildServiceProvider();          // :97
```

### 1.2 解析阶段（容器按构造函数签名递归造对象）

当你请求 `MainViewModel` 时，容器做这件事：

1. 查注册表 → 找到 `MainViewModel`（`ServiceLocator.cs:70`）
2. **反射**它的构造函数：`MainViewModel(IMenuNavigationService menuNavigationService)`（`MainViewModel.cs:11`）
3. 发现需要 `IMenuNavigationService` → 递归查注册表 → 构造 `MenuNavigationService`（`ServiceLocator.cs:80-81`）
4. 把 `MenuNavigationService` 注入，返回 `MainViewModel`

### 1.3 关键结论

容器不是"读心术"，它是**看你构造函数参数类型，就递归给你造什么**。这叫**构造函数注入**。

> **"你想要啥" = 你构造函数参数列表声明了啥。**

---

## 二、谁去请求了 ViewModel？

在 Avalonia MVVM 里，通常有**三条路径**请求 ViewModel。本项目实际使用的是 **路径 A（ViewLocator）+ 路径 B（XAML 绑定 ServiceLocator）的组合**。

### 2.1 路径 A — ViewLocator（自动按命名约定匹配子 View）

本项目 `DailyPoetryA/ViewLocator.cs` 实现 `IDataTemplate`：

```csharp
// ViewLocator.cs:9
public class ViewLocator : IDataTemplate {
    private Dictionary<Type, Control> _cache = new();

    public Control Build(object data) {
        if (data is null) return null;
        var name = data.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal)
            .Replace("DailyPoetryA.Library.", "DailyPoetryA.");  // 命名空间替换
        var type = Type.GetType(name);
        if (type == null) return new TextBlock { Text = "Not Found: " + name };
        var control = _cache.TryGetValue(type, out var value)
            ? value : _cache[type] = (Control)Activator.CreateInstance(type)!;
        control.DataContext = data;
        return control;
    }

    public bool Match(object data) => data is ViewModelBase;  // :33
}
```

在 `App.axaml:13-15` 中注册到 `Application.DataTemplates`：

```xml
<!-- App.axaml:13 -->
<Application.DataTemplates>
    <local:ViewLocator />
</Application.DataTemplates>
```

当某处 `DataContext` 被设成 ViewModel 时，Avalonia 遍历 `DataTemplates`，调 `Match` 命中后调 `Build`，按 `XxxViewModel → XxxView` 命名约定找到对应 View。

### 2.2 路径 B — XAML 绑定 ServiceLocator（本项目 MainWindow 实际方式）

本项目 `MainWindow` 是**无参构造函数**（`MainWindow.axaml.cs:6`），不由容器创建。ViewModel 通过 XAML 绑定到 `ServiceLocator` 的属性获取：

```xml
<!-- MainWindow.axaml:13 -->
DataContext="{Binding MainWindowViewModel, Source={StaticResource ServiceLocator}}"
```

`ServiceLocator` 在 `App.axaml:7-11` 中作为 `Application.Resources` 注册：

```xml
<!-- App.axaml:9 -->
<local:ServiceLocator x:Key="ServiceLocator"></local:ServiceLocator>
```

`ServiceLocator` 的属性内部调用容器解析（`ServiceLocator.cs:37-38`）：

```csharp
// ServiceLocator.cs:37-38
public MainWindowViewModel MainWindowViewModel =>
    _serviceProvider.GetService<MainWindowViewModel>();
```

### 2.3 路径 C — View 构造函数注入（本项目未采用，作为对比说明）

另一种常见做法是让 View 接收 VM 作为构造参数，由容器一并创建：

```csharp
// 假设写法（本项目未采用）
public MainWindow(MainViewModel vm) {
    InitializeComponent();
    DataContext = vm;
}
```

本项目没有这样做，原因是 `MainWindow` 在 `App.axaml.cs:15` 中直接 `new`，不经过容器。

### 2.4 谁触发的？

`App.axaml.cs:13-19` 中创建主窗口：

```csharp
// App.axaml.cs:15
desktop.MainWindow = new MainWindow();
```

> 本项目的入口是 `new MainWindow()`，**不经过容器**。容器在 `ServiceLocator` 构造函数中初始化（`ServiceLocator.cs:65-98`），之后 ViewModel 的解析由 **XAML 绑定** 触发。

---

## 三、IoC 和 MVVM 怎么关联？

MVVM 三层：**View ↔ ViewModel ↔ Model(Service)**。IoC 容器就是**把后两层粘起来的装配线**；View 层通过 XAML 绑定获取 VM。

### 3.1 装配示意图

```
MainWindow (new，无参，App.axaml.cs:15)
  └─ DataContext = XAML 绑定 ServiceLocator.MainWindowViewModel (MainWindow.axaml:13)
       └─ MainWindowViewModel (容器解析，构造函数注入，MainWindowViewModel.cs:13)
            ├─ IPoetryStorage         → PoetryStorage         (ServiceLocator.cs:85)
            ├─ IRootNavigationService → RootNavigationService (ServiceLocator.cs:78-79)
            ├─ IMenuNavigationService → MenuNavigationService (ServiceLocator.cs:80-81)
            └─ IFavoriteStorage       → FavoriteStorage       (ServiceLocator.cs:93)
                 └─ 全部由 ServiceProvider 递归构造
```

`MainWindowViewModel` 的构造函数（`MainWindowViewModel.cs:13-16`）：

```csharp
public MainWindowViewModel(IPoetryStorage poetryStorage,
    IRootNavigationService rootNavigationService,
    IMenuNavigationService menuNavigationService,
    IFavoriteStorage favoriteStorage) {
```

### 3.2 没有 IoC 的 MVVM（手动 new，紧耦合）

如果不用 IoC，等价写法是：

```csharp
// 假设写法（本项目实际未这样写，仅作对比说明）
var vm = new MainWindowViewModel(
    new PoetryStorage(),
    new RootNavigationService(),
    new MenuNavigationService(),
    new FavoriteStorage());
```

每层都得知道下一层具体类型，改一个全要改。

### 3.3 有 IoC 的 MVVM（只依赖接口，容器装配）

`ServiceLocator.cs:66-97` 注册全部映射，之后只需：

```csharp
// ServiceLocator.cs:37-38
var vm = _serviceProvider.GetService<MainWindowViewModel>();
```

容器自动递归构造 `MainWindowViewModel` 及其全部依赖。

### 3.4 关联本质

| 关注点 | 谁负责 |
|--------|--------|
| 职责分离（View 只管 UI，VM 只管状态逻辑，Model 只管数据） | **MVVM** |
| 依赖方向（高层不依赖低层具体，只依赖接口） | **IoC** |
| 装配方式（容器负责造对象） | **IoC** |

二者交集在 **ViewModel**：
- VM 的依赖（Service/Storage）由容器构造函数注入
- View 通过 XAML 绑定从 `ServiceLocator` 获取 VM
- 写 VM 时只声明接口（如 `IMenuNavigationService`），**永远不 `new MenuNavigationService()`**

---

## 四、一句话总结

> **MVVM 是分层思想，IoC 是装配工具。**
> **IoC 让 ViewModel 只面向接口编程，由容器在 `ServiceLocator` 初始化时把整棵 VM → Service 树造出来；View 通过 XAML 绑定获取 VM。**

---

## 五、本项目真实启动流程

### 5.1 程序入口 — `DailyPoetryA/Program.cs`

```csharp
// Program.cs:13-14
[STAThread]
public static void Main(string[] args) =>
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

// Program.cs:17-22
public static AppBuilder BuildAvaloniaApp() {
    IconProvider.Current.Register<FontAwesomeIconProvider>();
    return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
        .LogToTrace();
}
```

### 5.2 App XAML — `DailyPoetryA/App.axaml`

```xml
<!-- App.axaml:7-15 -->
<Application.Resources>
    <ResourceDictionary>
        <local:ServiceLocator x:Key="ServiceLocator"></local:ServiceLocator>
    </ResourceDictionary>
</Application.Resources>

<Application.DataTemplates>
    <local:ViewLocator />
</Application.DataTemplates>
```

### 5.3 App 代码 — `DailyPoetryA/App.axaml.cs`

```csharp
// App.axaml.cs:13-19
public override void OnFrameworkInitializationCompleted() {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
        desktop.MainWindow = new MainWindow();
    }
    base.OnFrameworkInitializationCompleted();
}
```

### 5.4 IoC 注册 — `DailyPoetryA/ServiceLocator.cs`

```csharp
// ServiceLocator.cs:65-98
public ServiceLocator() {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSingleton<MainWindowViewModel>();    // :69
    serviceCollection.AddSingleton<MainViewModel>();          // :70
    serviceCollection.AddSingleton<TodayViewModel>();         // :71
    // ...全部 ViewModel + Service 注册...
    _serviceProvider = serviceCollection.BuildServiceProvider();  // :97
}
```

### 5.5 ViewLocator — `DailyPoetryA/ViewLocator.cs`

（见 2.1 节完整代码）

### 5.6 MainWindow 绑定 — `DailyPoetryA/Views/MainWindow.axaml`

```xml
<!-- MainWindow.axaml:13-14 -->
DataContext="{Binding MainWindowViewModel, Source={StaticResource ServiceLocator}}"
Content="{Binding Content}"
```

---

## 六、常见误区

1. **"IoC 是万能的"** — 不是。只有注册过的类型才能解析；没注册会抛异常。
2. **"ViewModel 必须用 DI"** — 不必须，但推荐。本项目所有 ViewModel 均通过容器构造（`ServiceLocator.cs:68-76`）。
3. **"容器知道运行时想要哪个实现"** — 容器只知道**编译期注册的映射**；运行时切换实现要靠工厂或条件注册。
4. **"Service Locator 是反模式"** — 纯业务代码中到处 `Resolve` 确实是反模式。但本项目 `ServiceLocator` 是**为 XAML 绑定提供入口**的适配器：VM 在业务代码中仍通过构造函数注入（如 `MainViewModel.cs:11`），`ServiceLocator` 仅在 XAML 层被引用（`MainWindow.axaml:13`），属于 Avalonia 下的常见折中方案。
