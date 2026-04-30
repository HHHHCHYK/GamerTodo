# Bug 分析计划：任务完成按钮无法保持勾选状态

## 现象

点击任务左侧完成按钮后，按钮没有稳定保持勾选状态，用户看不到完成后的 `✓` 状态，或表现为点击无效。

## 当前实现观察

相关位置：

* `src/HeyeTodo.Client/Views/TaskPanelView.axaml`

  * 完成按钮绑定了 `ToggleTaskCompletedCommand`。

  * 完成按钮同时绑定了 `PointerPressed="OnTaskCheckPointerPressed"` 和 `PointerReleased="OnTaskCheckPointerReleased"`。

* `src/HeyeTodo.Client/Views/TaskPanelView.axaml.cs`

  * `OnTaskCheckPointerPressed` 直接设置 `e.Handled = true`。

  * `OnTaskCheckPointerReleased` 也直接设置 `e.Handled = true`。

* `src/HeyeTodo.Client/ViewModels/TaskPanelViewModel.cs`

  * `ToggleTaskCompleted(TaskItemViewModel task)` 会切换 `task.IsCompleted`。

* `src/HeyeTodo.Client/ViewModels/TaskItemViewModel.cs`

  * `CompletionMark => IsCompleted ? "✓" : string.Empty`。

  * `OnIsCompletedChanged` 会通知 `CompletionMark`、`CardOpacity`、`TitleTextDecorations` 更新。

## 根本原因分析

当前 Bug 的高概率根因不是 ViewModel 状态无法保持，而是完成按钮的指针事件处理破坏了按钮自己的点击流程。

Avalonia 的 `Button.Command` 通常依赖控件内部的指针按下/释放流程来触发。如果在按钮自己的 `PointerPressed` 或 `PointerReleased` 事件中提前设置：

```csharp
e.Handled = true;
```

就可能导致按钮内部无法继续完成正常点击识别，从而 `Command` 没有被触发，`ToggleTaskCompletedCommand` 没有执行，`IsCompleted` 没有变成 `true`，最终 `CompletionMark` 不会变成 `✓`。

也就是说，前一次为了阻止完成按钮点击冒泡到任务卡片，我把事件直接在按钮层吞掉了；这个处理阻止了父级卡片点击，但也可能同时阻止了按钮自身的 Command 执行。

## 为什么这和之前的卡片点击修复有关

任务卡目前是复合交互：

* 外层任务卡 `Border`：点击后打开详情面板。

* 内部完成按钮 `Button`：点击后切换完成状态。

之前为了避免点击完成按钮时也触发外层卡片详情，我在完成按钮的指针事件中设置了 `Handled = true`。这个思路的目标是对的：阻止事件冒泡。但具体位置不对：直接拦截按钮自己的 pointer 事件，会干扰按钮内部点击机制。

正确做法应该是：

* 让完成按钮正常执行自己的 `Command`；

* 外层卡片在处理点击时识别事件来源，如果事件来自完成按钮，则不打开详情；

* 或者只在不会影响 Button Click/Command 的阶段阻止冒泡。

## 解决方案

### 方案 A：推荐方案，外层卡片识别事件来源

调整思路：

1. 移除完成按钮上的 `PointerPressed` 和 `PointerReleased` 事件绑定。
2. 保留完成按钮的 `Command="{Binding DataContext.ToggleTaskCompletedCommand, ElementName=Root}"`。
3. 在任务卡片的 `OnTaskCardPointerReleased` 中判断本次点击是否来自完成按钮或按钮内部元素。
4. 如果来自完成按钮，则直接返回，不打开详情。
5. 如果不是来自完成按钮，则打开详情面板。

判断方式建议使用事件源向上查找父级控件，而不是使用坐标硬编码。例如：

* 从 `e.Source` 开始向上找；

* 如果找到带有 `TaskCheck` class 的 `Button`，说明这次点击来自完成按钮；

* 外层卡片不处理；

* 这样完成按钮的 Command 可以正常执行。

优点：

* 不干扰按钮自身 Command。

* 不需要 `position.X <= 54` 这种硬编码。

* 卡片行为仍然集中在卡片容器上。

* 子控件边界清晰。

### 方案 B：使用 Click 事件阻止冒泡

如果 Avalonia 的按钮 Click 事件适合当前项目，也可以：

1. 完成按钮保留 Command。
2. 给完成按钮添加 Click 事件。
3. 在 Click 事件中设置事件已处理。

但这种方式仍然可能和 Command 执行顺序有关，需要验证。相比之下，方案 A 更稳妥。

### 方案 C：拆出 TaskCardView

如果后续任务卡还要继续增加交互，例如拖拽、右键菜单、更多快捷按钮，可以拆出独立 `TaskCardView`。

优点：

* 卡片内部交互边界更清楚。

* 可以集中处理 hover、pressed、completed、selected 等状态。

缺点：

* 当前阶段会增加文件和结构复杂度。

* 对这个 Bug 来说不是必要修复。

## 推荐实施步骤

1. 修改 `TaskPanelView.axaml`：

   * 删除完成按钮上的 `PointerPressed="OnTaskCheckPointerPressed"`。

   * 删除完成按钮上的 `PointerReleased="OnTaskCheckPointerReleased"`。

   * 保留完成按钮的 `Command` 和 `CommandParameter`。

2. 修改 `TaskPanelView.axaml.cs`：

   * 删除 `OnTaskCheckPointerPressed`。

   * 删除 `OnTaskCheckPointerReleased`。

   * 在 `OnTaskCardPointerReleased` 开头新增事件来源判断。

   * 如果 `e.Source` 属于完成按钮内部，则直接 `return`，不打开详情。

3. 保持 `TaskPanelViewModel.ToggleTaskCompleted` 不变：

   * 当前 ViewModel 切换逻辑是正确的。

   * `TaskItemViewModel.OnIsCompletedChanged` 已经通知了 `CompletionMark` 更新。

4. 验证行为：

   * 点击完成按钮后出现 `✓`。

   * 再次点击完成按钮后 `✓` 消失。

   * 点击完成按钮不会打开详情面板。

   * 点击任务卡其他区域会打开详情面板。

   * 完成状态下标题仍显示删除线，卡片透明度仍降低。

5. 运行构建验证：

```bash
dotnet build HeyeTodo.sln -v minimal
```

## 验收标准

* 完成按钮可以稳定切换勾选状态。

* 勾选状态在当前内存任务对象中保持。

* 完成按钮点击不触发详情面板。

* 任务卡主体点击仍然打开详情面板。

* 构建通过，无错误。

